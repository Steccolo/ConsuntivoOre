using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConsuntivoOre.Core;

/// <summary>Persistenza su file JSON in %APPDATA%\ConsuntivoOre.</summary>
public sealed class DataStore
{
    public static readonly string DataDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ConsuntivoOre");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _entriesPath = Path.Combine(DataDirectory, "entries.json");
    private readonly string _settingsPath = Path.Combine(DataDirectory, "settings.json");
    private readonly List<TimeEntry> _entries;

    public AppSettings Settings { get; }

    /// <summary>Sollevato dopo ogni aggiunta, modifica o eliminazione.</summary>
    public event Action? EntriesChanged;

    public DataStore()
    {
        Directory.CreateDirectory(DataDirectory);
        _entries = Load<List<TimeEntry>>(_entriesPath) ?? [];
        Settings = Load<AppSettings>(_settingsPath) ?? new AppSettings();
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
                .Select(g => g.OrderByDescending(e => e.CreatedAt).First().Client)
                .ToList();

    public void Add(TimeEntry entry)
    {
        _entries.Add(entry);
        SaveEntries();
    }

    public void Update(Guid id, decimal hours, string client, string note)
    {
        var entry = Find(id) ?? throw new InvalidOperationException("Voce non trovata.");
        entry.Hours = hours;
        entry.Client = client;
        entry.Note = note;
        SaveEntries();
    }

    public void Delete(Guid id)
    {
        _entries.RemoveAll(e => e.Id == id);
        SaveEntries();
    }

    public void SaveSettings() => Save(_settingsPath, Settings);

    private void SaveEntries()
    {
        Save(_entriesPath, _entries);
        EntriesChanged?.Invoke();
    }

    private static T? Load<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Non sovrascrivere dati illeggibili: li mette da parte e riparte.
            Log.Error(ex);
            File.Copy(path, $"{path}.corrotto-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
            return null;
        }
    }

    private static void Save<T>(string path, T value)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, JsonOptions));
        File.Move(tmp, path, overwrite: true);
    }
}
