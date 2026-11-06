using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConsuntivoOre.Core;

/// <summary>I dati esistono ma non si possono usare: l'app non deve partire né sovrascriverli.</summary>
public sealed class DataStoreException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Persistenza su file JSON in %APPDATA%\ConsuntivoOre.</summary>
public sealed class DataStore
{
    public const int CurrentSchemaVersion = 1;
    private const int BackupsToKeep = 7;

    public static readonly string DefaultDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ConsuntivoOre");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _entriesPath;
    private readonly string _settingsPath;
    private readonly string _statePath;
    private readonly string _todosPath;
    private readonly List<TimeEntry> _entries;
    private readonly List<TodoItem> _todos;
    private readonly List<string> _loadProblems = [];

    public string DataDirectory { get; }
    public string BackupDirectory { get; }
    public AppSettings Settings { get; }
    public SchedulerState State { get; }

    /// <summary>File illeggibili trovati all'avvio e messi da parte: l'utente va avvisato.</summary>
    public IReadOnlyList<string> LoadProblems => _loadProblems;

    /// <summary>Sollevato dopo ogni aggiunta, modifica o eliminazione.</summary>
    public event Action? EntriesChanged;

    /// <summary>Sollevato dopo ogni modifica ai todo.</summary>
    public event Action? TodosChanged;

    public DataStore(string? directory = null)
    {
        DataDirectory = directory ?? DefaultDirectory;
        BackupDirectory = Path.Combine(DataDirectory, "backup");
        _entriesPath = Path.Combine(DataDirectory, "entries.json");
        _settingsPath = Path.Combine(DataDirectory, "settings.json");
        _statePath = Path.Combine(DataDirectory, "state.json");
        _todosPath = Path.Combine(DataDirectory, "todos.json");
        Directory.CreateDirectory(DataDirectory);

        var entries = Load(_entriesPath, ParseEntries);
        _entries = entries ?? [];
        if (entries is not null) Backup(_entriesPath, "entries");

        var todos = Load(_todosPath, ParseTodos);
        _todos = todos ?? [];
        if (todos is not null) Backup(_todosPath, "todos");

        var settings = Load(_settingsPath, ParseSettings);
        Settings = settings ?? new AppSettings();
        Settings.Normalize();

        // Le versioni precedenti tenevano lo stato dello scheduler dentro settings.json.
        string? stateSource = File.Exists(_statePath) ? _statePath : settings is not null ? _settingsPath : null;
        State = (stateSource is null ? null : Load(stateSource, Deserialize<SchedulerState>)) ?? new SchedulerState();
        if (stateSource == _settingsPath) MigrateState();
    }

    public IReadOnlyList<TimeEntry> EntriesBetween(DateOnly from, DateOnly to) =>
        _entries.Where(e => e.Date >= from && e.Date <= to)
                .OrderBy(e => e.Date).ThenBy(e => e.CreatedAt)
                .ToList();

    public decimal HoursBetween(DateOnly from, DateOnly to) =>
        _entries.Where(e => e.Date >= from && e.Date <= to).Sum(e => e.Hours);

    public decimal HoursOn(DateOnly day) => HoursBetween(day, day);

    public TimeEntry? Find(Guid id) => _entries.FirstOrDefault(e => e.Id == id);

    /// <summary>Clienti già usati, dal più recente.</summary>
    public IReadOnlyList<string> Clients() =>
        _entries.GroupBy(e => e.Client, StringComparer.CurrentCultureIgnoreCase)
                .OrderByDescending(g => g.Max(e => e.CreatedAt))
                .Select(g => g.MaxBy(e => e.CreatedAt)!.Client)
                .ToList();

    public void Add(TimeEntry entry)
    {
        _entries.Add(entry);
        CommitEntries(undo: () => _entries.Remove(entry));
    }

    public void Update(Guid id, decimal hours, string client, string note)
    {
        var entry = Find(id) ?? throw new InvalidOperationException("Voce non trovata.");
        var (oldHours, oldClient, oldNote) = (entry.Hours, entry.Client, entry.Note);
        entry.Hours = hours;
        entry.Client = client;
        entry.Note = note;
        CommitEntries(undo: () => (entry.Hours, entry.Client, entry.Note) = (oldHours, oldClient, oldNote));
    }

    public void Delete(Guid id)
    {
        int index = _entries.FindIndex(e => e.Id == id);
        if (index < 0) return;
        var entry = _entries[index];
        _entries.RemoveAt(index);
        CommitEntries(undo: () => _entries.Insert(index, entry));
    }

    // ------------------------------------------------------------ todo

    /// <summary>Todo da completare: prima quelli con scadenza (la più vicina in alto), poi gli altri.</summary>
    public IReadOnlyList<TodoItem> OpenTodos() =>
        _todos.Where(t => !t.IsDone)
              .OrderBy(t => t.DueDate is null)
              .ThenBy(t => t.DueDate)
              .ThenBy(t => t.CreatedAt)
              .ToList();

    /// <summary>Tutti i todo: prima quelli aperti (come <see cref="OpenTodos"/>), poi i completati dal più recente.</summary>
    public IReadOnlyList<TodoItem> Todos() =>
        [.. OpenTodos(), .. _todos.Where(t => t.IsDone).OrderByDescending(t => t.CompletedAt)];

    public TodoItem? FindTodo(Guid id) => _todos.FirstOrDefault(t => t.Id == id);

    /// <summary>Ore già consuntivate a partire dal todo.</summary>
    public decimal HoursForTodo(Guid todoId) => _entries.Where(e => e.TodoId == todoId).Sum(e => e.Hours);

    public void AddTodo(TodoItem todo)
    {
        _todos.Add(todo);
        CommitTodos(undo: () => _todos.Remove(todo));
    }

    public void UpdateTodo(Guid id, string name, decimal plannedHours, string description, DateOnly? dueDate)
    {
        var todo = FindTodo(id) ?? throw new InvalidOperationException("Todo non trovato.");
        var old = (todo.Name, todo.PlannedHours, todo.Description, todo.DueDate);
        (todo.Name, todo.PlannedHours, todo.Description, todo.DueDate) = (name, plannedHours, description, dueDate);
        CommitTodos(undo: () => (todo.Name, todo.PlannedHours, todo.Description, todo.DueDate) = old);
    }

    public void CompleteTodo(Guid id)
    {
        var todo = FindTodo(id) ?? throw new InvalidOperationException("Todo non trovato.");
        if (todo.IsDone) return;
        todo.CompletedAt = DateTime.Now;
        CommitTodos(undo: () => todo.CompletedAt = null);
    }

    /// <summary>Riporta un todo completato tra quelli da fare.</summary>
    public void ReopenTodo(Guid id)
    {
        var todo = FindTodo(id) ?? throw new InvalidOperationException("Todo non trovato.");
        if (todo.CompletedAt is not { } completedAt) return;
        todo.CompletedAt = null;
        CommitTodos(undo: () => todo.CompletedAt = completedAt);
    }

    public void DeleteTodo(Guid id)
    {
        int index = _todos.FindIndex(t => t.Id == id);
        if (index < 0) return;
        var todo = _todos[index];
        _todos.RemoveAt(index);
        CommitTodos(undo: () => _todos.Insert(index, todo));
    }

    public void SaveSettings() => Save(_settingsPath, Settings);

    public void SaveState() => Save(_statePath, State);

    /// <summary>Sospende il promemoria per il giorno indicato (ferie, malattia...).</summary>
    public void SkipReminder(DateOnly day)
    {
        State.ReminderSkippedOn = day;
        TrySaveState();
    }

    /// <summary>Rimanda il promemoria di oggi fino all'ora indicata.</summary>
    public void SnoozeReminder(DateTime until)
    {
        State.ReminderSnoozedUntil = until;
        TrySaveState();
    }

    private void TrySaveState()
    {
        try
        {
            SaveState();
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            // Resta valido in memoria fino alla chiusura dell'app.
            Log.Error(ex);
        }
    }

    public static bool IsFileError(Exception ex) => ex is IOException or UnauthorizedAccessException;

    /// <summary>In caso di errore la memoria torna allineata al file, così un nuovo tentativo non duplica la voce.</summary>
    private void CommitEntries(Action undo)
    {
        try
        {
            Save(_entriesPath, new EntriesFile { SchemaVersion = CurrentSchemaVersion, Entries = _entries });
        }
        catch
        {
            undo();
            throw;
        }
        EntriesChanged?.Invoke();
    }

    private void CommitTodos(Action undo)
    {
        try
        {
            Save(_todosPath, new TodosFile { SchemaVersion = CurrentSchemaVersion, Todos = _todos });
        }
        catch
        {
            undo();
            throw;
        }
        TodosChanged?.Invoke();
    }

    // Va scritto subito: il prossimo salvataggio delle impostazioni toglierà quei campi da settings.json.
    private void MigrateState()
    {
        try
        {
            SaveState();
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            Log.Error(ex);
        }
    }

    private void Backup(string path, string prefix)
    {
        try
        {
            if (!File.Exists(path)) return;
            Directory.CreateDirectory(BackupDirectory);
            string today = Path.Combine(BackupDirectory, $"{prefix}-{DateTime.Now:yyyyMMdd}.json");
            if (!File.Exists(today)) File.Copy(path, today);

            foreach (string old in Directory.GetFiles(BackupDirectory, $"{prefix}-*.json")
                         .OrderDescending(StringComparer.Ordinal).Skip(BackupsToKeep))
                File.Delete(old);
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            // Un backup mancato non deve impedire l'uso dell'app.
            Log.Error(ex);
        }
    }

    private static List<TimeEntry>? ParseEntries(string json)
    {
        // Le versioni precedenti salvavano direttamente l'elenco, senza numero di versione.
        var entries = json.TrimStart().StartsWith('[')
            ? Deserialize<List<TimeEntry>>(json)
            : CheckVersion(Deserialize<EntriesFile>(json))?.Entries;
        if (entries is null) return null;

        entries.RemoveAll(e => e is null);
        foreach (var e in entries)
        {
            e.Client ??= "";
            e.Note ??= "";
        }
        return entries;
    }

    private static List<TodoItem>? ParseTodos(string json)
    {
        var todos = CheckVersion(Deserialize<TodosFile>(json))?.Todos;
        if (todos is null) return null;

        todos.RemoveAll(t => t is null);
        foreach (var t in todos)
        {
            t.Name ??= "";
            t.Description ??= "";
        }
        return todos;
    }

    private static AppSettings? ParseSettings(string json)
    {
        var settings = Deserialize<AppSettings>(json);
        if (settings is not null) CheckSchema(settings.SchemaVersion);
        return settings;
    }

    private static T? CheckVersion<T>(T? file) where T : VersionedFile
    {
        if (file is not null) CheckSchema(file.SchemaVersion);
        return file;
    }

    private static void CheckSchema(int version)
    {
        if (version > CurrentSchemaVersion)
            throw new DataStoreException("I dati sono stati creati da una versione più recente dell'app: aggiornala per continuare.");
    }

    private static T? Deserialize<T>(string json) where T : class =>
        JsonSerializer.Deserialize<T>(json, JsonOptions);

    private T? Load<T>(string path, Func<string, T?> parse) where T : class
    {
        if (!File.Exists(path)) return null;

        string text = ReadWithRetry(path);
        try
        {
            if (parse(text) is { } value) return value;
        }
        catch (JsonException ex)
        {
            Log.Error(ex);
        }

        // Non sovrascrivere dati illeggibili: li mette da parte e riparte.
        string copy = $"{path}.corrotto-{DateTime.Now:yyyyMMdd-HHmmss}";
        try
        {
            File.Copy(path, copy, overwrite: true);
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            throw new DataStoreException($"Il file {path} è illeggibile e non è stato possibile metterlo da parte.", ex);
        }
        _loadProblems.Add($"{Path.GetFileName(path)} è illeggibile. Copia conservata in: {copy}");
        return null;
    }

    /// <summary>Un blocco temporaneo (antivirus, backup, OneDrive) non significa che il file sia corrotto.</summary>
    private static string ReadWithRetry(string path)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                if (attempt == 5)
                    throw new DataStoreException($"Impossibile leggere il file {path}: {ex.Message}", ex);
                Thread.Sleep(500);
            }
        }
    }

    private static void Save<T>(string path, T value)
    {
        string tmp = path + ".tmp";
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, value, JsonOptions);
            stream.Flush(flushToDisk: true);
        }
        File.Move(tmp, path, overwrite: true);
    }

    private abstract class VersionedFile
    {
        public int SchemaVersion { get; set; }
    }

    private sealed class EntriesFile : VersionedFile
    {
        public List<TimeEntry>? Entries { get; set; }
    }

    private sealed class TodosFile : VersionedFile
    {
        public List<TodoItem>? Todos { get; set; }
    }
}
