using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace ConsuntivoOre.ViewModels;

/// <summary>Una voce del giorno, già pronta da mostrare.</summary>
public sealed class EntryRow(TimeEntry entry)
{
    public TimeEntry Entry { get; } = entry;
    public string Hours => Fmt.Hours(Entry.Hours);
    public string Client => Entry.Client;
    public string Note => Entry.Note.ReplaceLineEndings(" ").Trim();
}

/// <summary>Scheda di consuntivazione: ore, cliente e nota per il giorno scelto.</summary>
public sealed partial class EntryViewModel : ObservableObject
{
    private static readonly IBrush Normal = new SolidColorBrush(Color.FromRgb(31, 41, 55));
    private static readonly IBrush Good = new SolidColorBrush(Color.FromRgb(21, 128, 61));
    private static readonly IBrush Warn = new SolidColorBrush(Color.FromRgb(180, 83, 9));

    private readonly DataStore _store;

    public EntryViewModel(DataStore store)
    {
        _store = store;
        _store.EntriesChanged += Refresh;
        Refresh();
    }

    /// <summary>Alla chiusura della finestra: smette di seguire i dati.</summary>
    public void Detach() => _store.EntriesChanged -= Refresh;

    /// <summary>Ricarica (es. dopo un cambio delle ore previste nelle impostazioni).</summary>
    public void Reload() => Refresh();

    public ObservableCollection<EntryRow> Rows { get; } = [];
    public ObservableCollection<string> Clients { get; } = [];

    // --- giorno

    [ObservableProperty] public partial DateTime? Day { get; set; } = DateTime.Today;

    private DateOnly _lastSelectedOn = DateOnly.FromDateTime(DateTime.Today);

    partial void OnDayChanged(DateTime? value)
    {
        // Svuotare la data dal calendario non ha senso qui: si torna a oggi.
        if (value is null) { Day = DateTime.Today; return; }
        _lastSelectedOn = DateOnly.FromDateTime(DateTime.Today);
        ResetForm();
        Refresh();
    }

    private DateOnly SelectedDay => DateOnly.FromDateTime(Day ?? DateTime.Today);

    public void SetDay(DateOnly day) => Day = day.ToDateTime(TimeOnly.MinValue);

    /// <summary>Una finestra rimasta aperta da un giorno precedente torna a oggi.</summary>
    public void MoveToTodayIfStale()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (_lastSelectedOn == today) return;
        _lastSelectedOn = today;
        SetDay(today);
    }

    [RelayCommand] private void PreviousDay() => Day = SelectedDay.AddDays(-1).ToDateTime(TimeOnly.MinValue);
    [RelayCommand] private void NextDay() => Day = SelectedDay.AddDays(1).ToDateTime(TimeOnly.MinValue);
    [RelayCommand] private void Today() => Day = DateTime.Today;

    // --- form

    [ObservableProperty] public partial decimal? Hours { get; set; }
    [ObservableProperty] public partial string? Client { get; set; } = "";
    [ObservableProperty] public partial string Note { get; set; } = "";
    [ObservableProperty] public partial string? Message { get; set; }
    [ObservableProperty] public partial bool MessageIsError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveText), nameof(IsEditing))]
    public partial Guid? EditingId { get; set; }

    public bool IsEditing => EditingId is not null;

    /// <summary>Ci sono dati che andrebbero persi azzerando il form.</summary>
    public bool HasUnsavedInput => IsEditing || Hours is > 0 || Note.Length > 0;
    public string SaveText => IsEditing ? "Salva modifiche" : "Aggiungi";

    // --- elenco

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(DeleteCommand))]
    public partial EntryRow? Selected { get; set; }

    public bool HasSelection => Selected is not null;

    [ObservableProperty] public partial string Total { get; set; } = "";
    [ObservableProperty] public partial IBrush TotalBrush { get; set; } = Normal;

    /// <summary>Impostato dalla finestra: chiede conferma prima di eliminare.</summary>
    public Func<string, Task<bool>>? AskConfirm { get; set; }

    /// <summary>"Non ricordarmelo oggi": lo gestisce l'app, che toglie anche la notifica.</summary>
    public Action? SkipRequested { get; set; }

    private void Refresh()
    {
        var day = SelectedDay;
        var selectedId = Selected?.Entry.Id;
        Rows.Clear();
        foreach (var entry in _store.EntriesBetween(day, day)) Rows.Add(new EntryRow(entry));
        Selected = Rows.FirstOrDefault(r => r.Entry.Id == selectedId);

        decimal total = _store.HoursOn(day);
        decimal target = _store.Settings.DailyTargetHours;
        Total = target > 0 ? $"Totale: {Fmt.Hours(total)} h su {Fmt.Hours(target)} h" : $"Totale: {Fmt.Hours(total)} h";
        TotalBrush = total == 0 ? Normal : target > 0 && total < target ? Warn : Good;

        Clients.Clear();
        foreach (var c in _store.Clients()) Clients.Add(c);
    }

    [RelayCommand]
    private void Save()
    {
        decimal hours = Hours ?? 0;
        if (hours <= 0) { ShowError("Indica quante ore hai lavorato."); return; }
        string client = (Client ?? "").Trim();
        if (client.Length == 0) { ShowError("Indica il cliente."); return; }
        string note = Note.Trim();

        try
        {
            if (EditingId is { } id) _store.Update(id, hours, client, note);
            else _store.Add(new TimeEntry { Date = SelectedDay, Hours = hours, Client = client, Note = note });
        }
        catch (Exception ex) when (DataStore.IsFileError(ex))
        {
            ShowError($"Impossibile salvare: {ex.Message}");
            return;
        }
        ResetForm();
    }

    [RelayCommand]
    private void ResetForm()
    {
        if (EditingId is not null) Client = "";
        EditingId = null;
        Hours = null;
        Note = "";
        Message = null;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Edit()
    {
        if (Selected?.Entry is not { } entry) return;
        EditingId = entry.Id;
        Hours = entry.Hours;
        Client = entry.Client;
        Note = entry.Note;
        Message = null;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task Delete()
    {
        if (Selected?.Entry is not { } entry) return;
        if (AskConfirm is not null && !await AskConfirm("Eliminare la voce selezionata?")) return;
        try
        {
            _store.Delete(entry.Id);
        }
        catch (Exception ex) when (DataStore.IsFileError(ex))
        {
            ShowError($"Impossibile salvare: {ex.Message}");
            return;
        }
        if (EditingId == entry.Id) ResetForm();
    }

    [RelayCommand]
    private void SkipToday()
    {
        SkipRequested?.Invoke();
        Message = "Per oggi non riceverai altri promemoria.";
        MessageIsError = false;
    }

    private void ShowError(string message)
    {
        Message = message;
        MessageIsError = true;
    }
}

