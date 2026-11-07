using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace ConsuntivoOre.ViewModels;

/// <summary>Testo della colonna "Jira": chiave della issue e stato dell'invio a Tempo.</summary>
public static class JiraLabel
{
    public static string For(TimeEntry e) => e.JiraStatus switch
    {
        JiraStatus.None => "",
        JiraStatus.Sent => $"{e.Jira!.Key} ✓",
        JiraStatus.ToSend => $"{e.Jira!.Key} · da inviare",
        _ when e.Jira is null => "da togliere da Tempo",
        _ => $"{e.Jira!.Key} · da aggiornare",
    };
}

/// <summary>Una voce del giorno, già pronta da mostrare.</summary>
public sealed class EntryRow(TimeEntry entry)
{
    public TimeEntry Entry { get; } = entry;
    public string Hours => Fmt.Hours(Entry.Hours);
    public string Client => Entry.Client;
    public string Jira => JiraLabel.For(Entry);
    public string Note => Entry.Note.ReplaceLineEndings(" ").Trim();
}

/// <summary>Scheda di consuntivazione: ore, cliente e nota per il giorno scelto.</summary>
public sealed partial class EntryViewModel : ObservableObject
{
    private static readonly IBrush Normal = new SolidColorBrush(Color.FromRgb(31, 41, 55));
    private static readonly IBrush Good = new SolidColorBrush(Color.FromRgb(21, 128, 61));
    private static readonly IBrush Warn = new SolidColorBrush(Color.FromRgb(180, 83, 9));

    private readonly DataStore _store;
    private readonly JiraService? _jira;

    public EntryViewModel(DataStore store, JiraService? jira = null)
    {
        _store = store;
        _jira = jira;
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

    // --- Jira

    /// <summary>Il collegamento è attivo nelle impostazioni: si mostrano le parti Jira.</summary>
    [ObservableProperty] public partial bool JiraEnabled { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IssueText), nameof(HasIssue), nameof(IssueWeight))]
    public partial JiraIssueRef? Issue { get; set; }

    public bool HasIssue => Issue is not null;
    public FontWeight IssueWeight => HasIssue ? FontWeight.SemiBold : FontWeight.Normal;
    public string IssueText => Issue?.Display ?? "nessuna (solo in locale)";

    public bool SendImmediately
    {
        get => _store.Settings.Jira.SendImmediately;
        set
        {
            if (_store.Settings.Jira.SendImmediately == value) return;
            _store.Settings.Jira.SendImmediately = value;
            OnPropertyChanged();
            try
            {
                _store.SaveSettings();
            }
            catch (Exception ex) when (DataStore.IsFileError(ex))
            {
                Log.Error(ex); // resta valida fino alla chiusura dell'app
            }
        }
    }

    /// <summary>Impostato dalla finestra: apre la ricerca delle issue.</summary>
    public Func<Task<JiraIssueRef?>>? AskIssue { get; set; }

    /// <summary>Impostato dalla finestra: domanda con più risposte (indice del pulsante, -1 = chiusa).</summary>
    public Func<string, string[], Task<int>>? AskChoice { get; set; }

    [ObservableProperty] public partial bool IsSending { get; set; }

    [RelayCommand]
    private async Task ChooseIssue()
    {
        if (AskIssue is null || await AskIssue() is not { } issue) return;
        Issue = issue;
        // Il cliente è obbligatorio: se manca si propone il progetto Jira.
        if (string.IsNullOrWhiteSpace(Client) && issue.ProjectName.Length > 0) Client = issue.ProjectName;
    }

    [RelayCommand] private void ClearIssue() => Issue = null;

    private bool CanSendSelected => JiraEnabled && Selected?.Entry.NeedsTempoSync == true;

    [RelayCommand(CanExecute = nameof(CanSendSelected))]
    private async Task SendSelected()
    {
        if (Selected?.Entry is { } entry) await SendAsync(entry.Id, null);
    }

    /// <summary>Invia la voce a Tempo e lo dice nel messaggio sotto il form.</summary>
    private async Task SendAsync(Guid id, string? savedPrefix)
    {
        if (_jira is null) return;
        IsSending = true;
        Message = (savedPrefix ?? "") + "Invio a Tempo in corso…";
        MessageIsError = false;
        try
        {
            var result = await _jira.SyncAsync([id]);
            Message = result.Ok
                ? (savedPrefix ?? "") + result.Summary()
                : (savedPrefix ?? "") + "Invio a Tempo non riuscito: " + string.Join("\n", result.Errors) +
                  "\nPuoi riprovare con «Invia a Tempo» o dal report.";
            MessageIsError = !result.Ok;
        }
        catch (JiraException ex)
        {
            Message = (savedPrefix ?? "") + "Invio a Tempo non riuscito: " + ex.Message;
            MessageIsError = true;
        }
        finally
        {
            IsSending = false;
        }
    }

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
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(DeleteCommand), nameof(SendSelectedCommand))]
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

        JiraEnabled = _jira?.IsEnabled == true;
        OnPropertyChanged(nameof(SendImmediately));
        SendSelectedCommand.NotifyCanExecuteChanged();
    }

    // Mentre una voce va a Tempo si può già inserire la successiva.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task Save()
    {
        decimal hours = Hours ?? 0;
        if (hours <= 0) { ShowError("Indica quante ore hai lavorato."); return; }
        string client = (Client ?? "").Trim();
        if (client.Length == 0) { ShowError("Indica il cliente."); return; }
        string note = Note.Trim();
        // A Jira disattivato il collegamento già presente sulla voce non si tocca.
        var issue = JiraEnabled ? Issue : EditingId is { } e ? _store.Find(e)?.Jira : null;

        Guid savedId;
        try
        {
            if (EditingId is { } id)
            {
                _store.Update(id, hours, client, note, issue);
                savedId = id;
            }
            else
            {
                var entry = new TimeEntry { Date = SelectedDay, Hours = hours, Client = client, Note = note, Jira = issue };
                _store.Add(entry);
                savedId = entry.Id;
            }
        }
        catch (Exception ex) when (DataStore.IsFileError(ex))
        {
            ShowError($"Impossibile salvare: {ex.Message}");
            return;
        }
        ResetForm();

        if (JiraEnabled && SendImmediately && _store.Find(savedId) is { NeedsTempoSync: true })
            await SendAsync(savedId, "Salvata. ");
    }

    [RelayCommand]
    private void ResetForm()
    {
        // Come il cliente, la issue resta per la voce successiva (spesso è la stessa), tranne dopo una modifica.
        if (EditingId is not null)
        {
            Client = "";
            Issue = null;
        }
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
        Issue = entry.Jira;
        Message = null;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task Delete()
    {
        if (Selected?.Entry is not { } entry) return;
        if (entry.Tempo is not null)
        {
            if (!await AskDeleteRemote(entry)) return;
        }
        else if (AskConfirm is not null && !await AskConfirm("Eliminare la voce selezionata?")) return;
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

    /// <summary>Voce già su Tempo: si sceglie se toglierla anche da lì. Falso = non eliminare.</summary>
    private async Task<bool> AskDeleteRemote(TimeEntry entry)
    {
        if (AskChoice is null) return false;
        if (_jira is not { IsEnabled: true })
        {
            return await AskChoice("La voce è registrata anche su Tempo, ma il collegamento a Jira è disattivato: " +
                                   "verrà eliminata solo in locale.", ["Elimina in locale", "Annulla"]) == 0;
        }

        int choice = await AskChoice("La voce è registrata anche su Tempo. Vuoi eliminarla anche da lì?",
            ["Elimina anche da Tempo", "Solo in locale", "Annulla"]);
        if (choice == 1) return true;
        if (choice != 0) return false;
        try
        {
            IsSending = true;
            await _jira.DeleteRemoteAsync(entry);
            return true;
        }
        catch (JiraException ex)
        {
            ShowError($"Non eliminata: Tempo ha dato un errore. {ex.Message}");
            return false;
        }
        finally
        {
            IsSending = false;
        }
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

