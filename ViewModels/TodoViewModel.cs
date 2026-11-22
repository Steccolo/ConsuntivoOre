using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace ConsuntivoOre.ViewModels;

/// <summary>
/// Una riga della tabella: intestazione di un gruppo (priorità o completati) oppure un todo, già pronta da mostrare.
/// </summary>
public sealed class TodoRow
{
    private static readonly IBrush Text = new SolidColorBrush(Color.FromRgb(31, 41, 55));
    private static readonly IBrush Muted = new SolidColorBrush(Color.FromRgb(107, 114, 128));
    private static readonly IBrush Red = new SolidColorBrush(Color.FromRgb(220, 38, 38));
    private static readonly IBrush Orange = new SolidColorBrush(Color.FromRgb(217, 119, 6));
    private static readonly IBrush Green = new SolidColorBrush(Color.FromRgb(21, 128, 61));
    private static readonly IBrush GroupBack = new SolidColorBrush(Color.FromRgb(249, 250, 251));
    private static readonly IBrush DoneBack = new SolidColorBrush(Color.FromRgb(240, 253, 244));
    private static readonly IBrush RedBadge = new SolidColorBrush(Color.FromRgb(254, 226, 226));
    private static readonly IBrush YellowBadge = new SolidColorBrush(Color.FromRgb(254, 243, 199));
    private static readonly IBrush YellowBadgeText = new SolidColorBrush(Color.FromRgb(180, 83, 9));
    private static readonly IBrush GreenBadge = new SolidColorBrush(Color.FromRgb(220, 252, 231));

    private readonly decimal _logged;
    private readonly DateOnly _today;

    private TodoRow(TodoItem? todo, string header, decimal logged, DateOnly today)
    {
        Todo = todo;
        HeaderText = header;
        _logged = logged;
        _today = today;
    }

    public static TodoRow Group(string text) => new(null, text, 0, default);
    public static TodoRow Item(TodoItem todo, decimal logged, DateOnly today) => new(todo, "", logged, today);

    /// <summary>Null sulle intestazioni dei gruppi.</summary>
    public TodoItem? Todo { get; }
    public bool IsHeader => Todo is null;
    public string HeaderText { get; }

    public string Name => Todo?.Name ?? HeaderText;
    public string Planned => Todo is { PlannedHours: > 0 } t ? Fmt.Hours(t.PlannedHours) : "";
    public string Logged => _logged > 0 ? $"{Fmt.Hours(_logged)} h" : "";
    public string Due => Todo?.DueDate is { } d ? Fmt.Date(d) : "";
    public string JiraKey => Todo?.Jira?.Key ?? "";
    public string JiraTip => Todo?.Jira?.Display ?? "";
    /// <summary>Come è stata scritta: righe ed elenchi puntati compresi.</summary>
    public string Description => Todo?.Description.Trim() ?? "";

    private bool Done => Todo?.IsDone == true;
    private bool Overdue => Todo is { IsDone: false, DueDate: { } d } && d < _today;
    private bool DueToday => Todo is { IsDone: false } t && t.DueDate == _today;

    public string? Badge => Done ? "Completato" : Overdue ? "Scaduto" : DueToday ? "Oggi" : null;
    public bool HasBadge => Badge is not null;
    public IBrush BadgeBack => Done ? GreenBadge : Overdue ? RedBadge : YellowBadge;
    public IBrush BadgeFore => Done ? Green : Overdue ? Red : YellowBadgeText;

    // Un pennello nullo nasconderebbe il testo: tutti i colori sono espliciti (tema chiaro).
    public IBrush Background => IsHeader ? GroupBack : Done ? DoneBack : Brushes.Transparent;
    public IBrush Foreground => IsHeader ? Muted : Done ? Green : Overdue ? Red : Text;
    public IBrush DueForeground => Overdue ? Red : DueToday ? Orange : Foreground;
    public IBrush DescriptionForeground => IsHeader || Done || Overdue ? Foreground : Muted;
    public FontWeight NameWeight => IsHeader || Overdue || DueToday || Done ? FontWeight.SemiBold : FontWeight.Normal;
    public FontWeight DueWeight => Overdue || DueToday ? FontWeight.SemiBold : FontWeight.Normal;
    public double NameSize => IsHeader ? 12 : 14;
}

public sealed partial class TodoViewModel : ObservableObject
{
    public static readonly IReadOnlyList<string> Priorities = ["Alta", "Media", "Bassa"];

    private readonly DataStore _store;
    private readonly JiraService? _jira;

    public TodoViewModel(DataStore store, JiraService? jira = null)
    {
        _store = store;
        _jira = jira;
        _store.TodosChanged += Refresh;
        _store.EntriesChanged += Refresh;
        Refresh();
    }

    /// <summary>Alla chiusura della finestra: smette di seguire i dati.</summary>
    public void Detach()
    {
        _store.TodosChanged -= Refresh;
        _store.EntriesChanged -= Refresh;
    }

    /// <summary>Dopo un cambio delle impostazioni (Jira attivato o no).</summary>
    public void Reload() => Refresh();

    public ObservableCollection<TodoRow> Rows { get; } = [];
    public IReadOnlyList<string> PriorityNames => Priorities;

    // --- form

    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Description { get; set; } = "";
    [ObservableProperty] public partial decimal? PlannedHours { get; set; }
    [ObservableProperty] public partial DateTime? DueDate { get; set; }
    /// <summary>0 alta, 1 media, 2 bassa (ordine del menu).</summary>
    [ObservableProperty] public partial int PriorityIndex { get; set; } = 1;
    [ObservableProperty] public partial string? Message { get; set; }
    [ObservableProperty] public partial bool MessageIsError { get; set; }
    [ObservableProperty] public partial bool JiraEnabled { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IssueText), nameof(HasIssue))]
    public partial JiraIssueRef? Issue { get; set; }

    public bool HasIssue => Issue is not null;
    public string IssueText => Issue?.Display ?? "nessuna";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveText), nameof(IsEditing))]
    public partial Guid? EditingId { get; set; }

    public bool IsEditing => EditingId is not null;

    /// <summary>Si sta scrivendo un todo: non va interrotto.</summary>
    public bool HasUnsavedInput =>
        IsEditing || Name.Length > 0 || Description.Length > 0 || PlannedHours is > 0 || DueDate is not null;
    public string SaveText => IsEditing ? "Salva modifiche" : "Aggiungi";

    private static TodoPriority PriorityOf(int index) => index switch { 0 => TodoPriority.High, 2 => TodoPriority.Low, _ => TodoPriority.Medium };
    private static int IndexOf(TodoPriority p) => p switch { TodoPriority.High => 0, TodoPriority.Low => 2, _ => 1 };

    // --- elenco

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(CompleteText))]
    [NotifyCanExecuteChangedFor(nameof(LogCommand), nameof(EditCommand), nameof(ToggleCompletedCommand), nameof(DeleteCommand))]
    public partial TodoRow? Selected { get; set; }

    /// <summary>Le intestazioni dei gruppi non contano come selezione.</summary>
    public bool HasSelection => Selected?.Todo is not null;
    public string CompleteText => Selected?.Todo?.IsDone == true ? "Riapri" : "Completato";

    [ObservableProperty] public partial string Summary { get; set; } = "";

    /// <summary>Impostati dalla finestra: aprono i dialoghi.</summary>
    public Func<TodoItem, decimal, Task<(DateOnly Day, decimal Hours)?>>? AskLog { get; set; }
    public Func<string, Task<bool>>? AskConfirm { get; set; }
    public Func<Task<JiraIssueRef?>>? AskIssue { get; set; }

    /// <summary>Seleziona l'n-esimo todo (le intestazioni non si contano).</summary>
    public void SelectRow(int index) => Selected = Rows.Where(r => !r.IsHeader).ElementAtOrDefault(index);

    private void Refresh()
    {
        var selectedId = Selected?.Todo?.Id;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todos = _store.Todos();

        Rows.Clear();
        // Gruppi per priorità: un gruppo compare solo se ha almeno un todo da fare.
        foreach (var group in todos.Where(t => !t.IsDone).GroupBy(t => t.Priority).OrderByDescending(g => g.Key))
        {
            Rows.Add(TodoRow.Group($"PRIORITÀ {Fmt.Priority(group.Key).ToUpperInvariant()} · {group.Count()}"));
            foreach (var todo in group) Rows.Add(TodoRow.Item(todo, _store.HoursForTodo(todo.Id), today));
        }
        var done = todos.Where(t => t.IsDone).ToList();
        if (done.Count > 0)
        {
            Rows.Add(TodoRow.Group($"COMPLETATI · {done.Count}"));
            foreach (var todo in done) Rows.Add(TodoRow.Item(todo, _store.HoursForTodo(todo.Id), today));
        }
        Selected = Rows.FirstOrDefault(r => r.Todo?.Id == selectedId && selectedId is not null);

        int open = todos.Count(t => !t.IsDone);
        int overdue = todos.Count(t => !t.IsDone && t.DueDate is { } d && d < today);
        Summary = todos.Count == 0 ? "Nessun todo" : string.Join(" · ", new[]
        {
            $"{open} da fare",
            overdue > 0 ? (overdue == 1 ? "1 scaduto" : $"{overdue} scaduti") : null,
            done.Count > 0 ? (done.Count == 1 ? "1 completato" : $"{done.Count} completati") : null,
        }.Where(p => p is not null));

        JiraEnabled = _jira?.IsEnabled == true;
    }

    [RelayCommand]
    private async Task ChooseIssue()
    {
        if (AskIssue is null || await AskIssue() is not { } issue) return;
        Issue = issue;
        // Nome e descrizione sono obbligatori: se mancano si propongono quelli della issue.
        if (Name.Trim().Length == 0) Name = issue.Key;
        if (Description.Trim().Length == 0) Description = issue.Summary;
    }

    [RelayCommand] private void ClearIssue() => Issue = null;

    [RelayCommand]
    private void Save()
    {
        string name = Name.Trim(), description = Description.Trim();
        if (name.Length == 0) { ShowError("Indica il nome del todo."); return; }
        if (description.Length == 0) { ShowError("Indica la descrizione del todo."); return; }
        decimal hours = PlannedHours ?? 0;
        DateOnly? due = DueDate is { } d ? DateOnly.FromDateTime(d) : null;
        var priority = PriorityOf(PriorityIndex);
        // A Jira disattivato il collegamento già presente sul todo non si tocca.
        var issue = JiraEnabled ? Issue : EditingId is { } e ? _store.FindTodo(e)?.Jira : null;

        if (!TrySave(() =>
            {
                if (EditingId is { } id) _store.UpdateTodo(id, name, hours, description, due, priority, issue);
                else _store.AddTodo(new TodoItem
                {
                    Name = name, PlannedHours = hours, Description = description, DueDate = due, Priority = priority, Jira = issue,
                });
            })) return;
        ResetForm();
    }

    [RelayCommand]
    private void ResetForm()
    {
        EditingId = null;
        Name = Description = "";
        PlannedHours = null;
        DueDate = null;
        PriorityIndex = 1;
        Issue = null;
        Message = null;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Edit()
    {
        if (Selected?.Todo is not { } todo) return;
        EditingId = todo.Id;
        Name = todo.Name;
        Description = todo.Description;
        PlannedHours = todo.PlannedHours > 0 ? todo.PlannedHours : null;
        DueDate = todo.DueDate?.ToDateTime(TimeOnly.MinValue);
        PriorityIndex = IndexOf(todo.Priority);
        Issue = todo.Jira;
        Message = null;
    }

    /// <summary>
    /// Registra le ore nel giorno scelto (di default oggi); il todo diventa completato.
    /// Se il todo ha una issue Jira, la voce le viene collegata e inviata subito a Tempo.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task Log()
    {
        if (Selected?.Todo is not { } todo || AskLog is null) return;
        if (await AskLog(todo, _store.HoursForTodo(todo.Id)) is not { } answer) return;

        bool toJira = todo.Jira is not null && _jira is { IsEnabled: true };
        var entry = new TimeEntry
        {
            Date = answer.Day, Hours = answer.Hours,
            Client = todo.Name, Note = todo.Description, TodoId = todo.Id,
            Jira = toJira ? todo.Jira : null,
        };
        if (!TrySave(() =>
            {
                _store.Add(entry);
                if (!todo.IsDone) _store.CompleteTodo(todo.Id);
            })) return;

        if (!toJira)
        {
            ShowInfo($"Consuntivate {Fmt.Hours(answer.Hours)} h il {Fmt.Date(answer.Day)}.");
            return;
        }
        ShowInfo("Consuntivata. Invio a Tempo in corso…");
        try
        {
            var result = await _jira!.SyncAsync([entry.Id]);
            if (result.Ok) ShowInfo($"Consuntivata e inviata a Tempo su {todo.Jira!.Key}.");
            else ShowError("Consuntivata, ma l'invio a Tempo non è riuscito: " + string.Join("\n", result.Errors) +
                           "\nPuoi riprovare dalla scheda Consuntiva ore.");
        }
        catch (JiraException ex)
        {
            ShowError("Consuntivata, ma l'invio a Tempo non è riuscito: " + ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ToggleCompleted()
    {
        if (Selected?.Todo is not { } todo) return;
        TrySave(() =>
        {
            if (todo.IsDone) _store.ReopenTodo(todo.Id);
            else _store.CompleteTodo(todo.Id);
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task Delete()
    {
        if (Selected?.Todo is not { } todo) return;
        if (AskConfirm is not null &&
            !await AskConfirm($"Eliminare «{todo.Name}»?\nLe ore già consuntivate restano registrate.")) return;
        if (TrySave(() => _store.DeleteTodo(todo.Id)) && EditingId == todo.Id) ResetForm();
    }

    private bool TrySave(Action save)
    {
        try
        {
            save();
            Message = null;
            return true;
        }
        catch (Exception ex) when (DataStore.IsFileError(ex))
        {
            ShowError($"Impossibile salvare: {ex.Message}");
            return false;
        }
    }

    private void ShowError(string message)
    {
        Message = message;
        MessageIsError = true;
    }

    private void ShowInfo(string message)
    {
        Message = message;
        MessageIsError = false;
    }
}
