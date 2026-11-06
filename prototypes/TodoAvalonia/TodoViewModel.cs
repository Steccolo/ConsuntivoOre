using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConsuntivoOre.Core;

namespace TodoAvalonia;

/// <summary>Una riga dell'elenco, già pronta da mostrare (testi e colori).</summary>
public sealed class TodoRow(TodoItem todo, decimal logged, DateOnly today)
{
    private static readonly IBrush DoneBack = new SolidColorBrush(Color.FromRgb(220, 252, 231));
    private static readonly IBrush DoneFore = new SolidColorBrush(Color.FromRgb(20, 83, 45));
    private static readonly IBrush DueBack = new SolidColorBrush(Color.FromRgb(254, 249, 195));
    private static readonly IBrush DueTodayFore = Brushes.Black;
    private static readonly IBrush NormalFore = new SolidColorBrush(Color.FromRgb(31, 41, 55));
    private static readonly IBrush OverdueFore = new SolidColorBrush(Color.FromRgb(220, 38, 38));

    public TodoItem Todo { get; } = todo;
    public string Name => Todo.Name;
    public string Planned => Todo.PlannedHours > 0 ? Fmt.Hours(Todo.PlannedHours) : "";
    public string Logged => logged > 0 ? $"{Fmt.Hours(logged)} h" : "";
    public string Due => Todo.DueDate is { } d ? Fmt.Date(d) : "";
    public string Description => Todo.Description.ReplaceLineEndings(" ").Trim();

    private bool Overdue => !Todo.IsDone && Todo.DueDate is { } d && d < today;
    private bool DueToday => !Todo.IsDone && Todo.DueDate == today;
    private bool Highlighted => Todo.IsDone || Overdue || DueToday;

    // Un pennello nullo nasconderebbe il testo: le righe normali usano colori espliciti (tema chiaro).
    public IBrush Background => Todo.IsDone ? DoneBack : Overdue || DueToday ? DueBack : Brushes.Transparent;
    public IBrush Foreground => Todo.IsDone ? DoneFore : Overdue ? OverdueFore : DueToday ? DueTodayFore : NormalFore;
    public FontWeight Weight => Highlighted ? FontWeight.Bold : FontWeight.Normal;
}

public sealed partial class TodoViewModel : ObservableObject
{
    private readonly DataStore _store;

    public TodoViewModel(DataStore store)
    {
        _store = store;
        _store.TodosChanged += Refresh;
        _store.EntriesChanged += Refresh;
        Refresh();
    }

    public ObservableCollection<TodoRow> Rows { get; } = [];

    // --- form

    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Description { get; set; } = "";
    [ObservableProperty] public partial decimal? PlannedHours { get; set; }
    [ObservableProperty] public partial DateTime? DueDate { get; set; }
    [ObservableProperty] public partial string? Error { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveText), nameof(IsEditing))]
    public partial Guid? EditingId { get; set; }

    public bool IsEditing => EditingId is not null;
    public string SaveText => IsEditing ? "Salva modifiche" : "Aggiungi";

    // --- elenco

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(CompleteText))]
    [NotifyCanExecuteChangedFor(nameof(LogCommand), nameof(EditCommand), nameof(ToggleCompletedCommand), nameof(DeleteCommand))]
    public partial TodoRow? Selected { get; set; }

    public bool HasSelection => Selected is not null;
    public string CompleteText => Selected?.Todo.IsDone == true ? "Riapri" : "Completato";

    [ObservableProperty] public partial string Summary { get; set; } = "";

    /// <summary>Impostati dalla finestra: aprono i dialoghi.</summary>
    public Func<TodoItem, decimal, Task<(DateOnly Day, decimal Hours)?>>? AskLog { get; set; }
    public Func<string, Task<bool>>? AskConfirm { get; set; }

    public void SelectRow(int index) => Selected = index < Rows.Count ? Rows[index] : null;

    private void Refresh()
    {
        var selectedId = Selected?.Todo.Id;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todos = _store.Todos();

        Rows.Clear();
        foreach (var todo in todos) Rows.Add(new TodoRow(todo, _store.HoursForTodo(todo.Id), today));
        Selected = Rows.FirstOrDefault(r => r.Todo.Id == selectedId);

        int open = todos.Count(t => !t.IsDone);
        int completed = todos.Count - open;
        int overdue = todos.Count(t => !t.IsDone && t.DueDate is { } d && d < today);
        Summary = todos.Count == 0 ? "Nessun todo" : string.Join(", ", new[]
        {
            $"{open} da fare",
            overdue > 0 ? $"{overdue} scaduti" : null,
            completed > 0 ? $"{completed} completati" : null,
        }.Where(p => p is not null));
    }

    [RelayCommand]
    private void Save()
    {
        string name = Name.Trim(), description = Description.Trim();
        if (name.Length == 0) { Error = "Indica il nome del todo."; return; }
        if (description.Length == 0) { Error = "Indica la descrizione del todo."; return; }
        decimal hours = PlannedHours ?? 0;
        DateOnly? due = DueDate is { } d ? DateOnly.FromDateTime(d) : null;

        if (!TrySave(() =>
            {
                if (EditingId is { } id) _store.UpdateTodo(id, name, hours, description, due);
                else _store.AddTodo(new TodoItem { Name = name, PlannedHours = hours, Description = description, DueDate = due });
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
        Error = null;
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
        Error = null;
    }

    /// <summary>Registra le ore nel giorno scelto (di default oggi); il todo diventa completato.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task Log()
    {
        if (Selected?.Todo is not { } todo || AskLog is null) return;
        if (await AskLog(todo, _store.HoursForTodo(todo.Id)) is not { } answer) return;

        TrySave(() =>
        {
            _store.Add(new TimeEntry
            {
                Date = answer.Day, Hours = answer.Hours,
                Client = todo.Name, Note = todo.Description, TodoId = todo.Id,
            });
            if (!todo.IsDone) _store.CompleteTodo(todo.Id);
        });
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
            Error = null;
            return true;
        }
        catch (Exception ex) when (DataStore.IsFileError(ex))
        {
            Error = $"Impossibile salvare: {ex.Message}";
            return false;
        }
    }
}
