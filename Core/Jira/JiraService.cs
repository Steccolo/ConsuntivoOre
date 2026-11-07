namespace ConsuntivoOre.Core;

public sealed record SyncResult(int Sent, int Removed, IReadOnlyList<string> Errors)
{
    public bool Ok => Errors.Count == 0;

    public string Summary()
    {
        var parts = new List<string>();
        if (Sent > 0) parts.Add(Sent == 1 ? "1 voce inviata a Tempo" : $"{Sent} voci inviate a Tempo");
        if (Removed > 0) parts.Add(Removed == 1 ? "1 voce tolta da Tempo" : $"{Removed} voci tolte da Tempo");
        if (Errors.Count > 0) parts.Add(Errors.Count == 1 ? "1 errore" : $"{Errors.Count} errori");
        return parts.Count == 0 ? "Niente da inviare a Tempo." : string.Join(", ", parts) + ".";
    }
}

/// <summary>
/// Collegamento con Jira/Tempo usato dall'interfaccia: tiene credenziali e client, invia le voci
/// e registra l'esito nel <see cref="DataStore"/>. Va usato dal thread dell'interfaccia (come il DataStore).
/// </summary>
public sealed class JiraService(DataStore store, CredentialStore credentials, HttpMessageHandler? handler = null) : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private JiraClient? _client;

    public JiraCredentials Credentials { get; private set; } = credentials.Load();
    public JiraSettings Settings => store.Settings.Jira;
    public bool IsEnabled => Settings.Enabled;

    /// <summary>Sollevato quando cambiano impostazioni o credenziali (per mostrare o nascondere le parti Jira).</summary>
    public event Action? ConfigurationChanged;

    public void SaveCredentials(JiraCredentials value)
    {
        credentials.Save(value);
        Credentials = value;
        Reset();
    }

    /// <summary>Da chiamare dopo aver cambiato le impostazioni: il client si ricrea alla prossima chiamata.</summary>
    public void Reset()
    {
        _client?.Dispose();
        _client = null;
        ConfigurationChanged?.Invoke();
    }

    public void Dispose() => _client?.Dispose();

    private JiraClient Client
    {
        get
        {
            if (!IsEnabled) throw new JiraException("Il collegamento a Jira è disattivato nelle impostazioni.");
            return _client ??= new JiraClient(Settings, Credentials, handler);
        }
    }

    public Task<IReadOnlyList<JiraIssueRef>> SearchAsync(string query, CancellationToken ct = default) =>
        Client.SearchIssuesAsync(query, ct);

    /// <summary>Prova Jira e Tempo con valori non ancora salvati; restituisce un riepilogo leggibile.</summary>
    public static async Task<string> TestAsync(JiraSettings settings, JiraCredentials creds, HttpMessageHandler? handler = null,
        CancellationToken ct = default)
    {
        using var client = new JiraClient(settings, creds, handler);
        var me = await client.MyselfAsync(ct);
        var projects = await client.LoggableProjectsAsync(ct);
        string text = $"Jira: connesso come {me.DisplayName}, {projects.Count} progetti in cui puoi registrare ore.";
        if (!client.HasTempoToken) return text + "\nTempo: manca il token.";
        await client.TestTempoAsync(ct);
        return text + "\nTempo: token valido.";
    }

    /// <summary>Invia (o aggiorna, o toglie) su Tempo le voci indicate che ne hanno bisogno.</summary>
    public async Task<SyncResult> SyncAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        int sent = 0, removed = 0;
        var errors = new List<string>();
        await _lock.WaitAsync(ct);
        try
        {
            foreach (var id in ids.Distinct().ToList())
            {
                // Riletta dopo l'attesa: un altro invio potrebbe averla già sistemata.
                if (store.Find(id) is not { NeedsTempoSync: true } entry) continue;
                try
                {
                    if (await SyncOneAsync(entry, ct)) sent++;
                    else removed++;
                }
                catch (JiraException ex)
                {
                    errors.Add($"{Label(entry)}: {ex.Message}");
                }
                catch (Exception ex) when (DataStore.IsFileError(ex))
                {
                    // Su Tempo è andata, ma non si è riusciti a ricordarlo: un nuovo invio la duplicherebbe.
                    Log.Error(ex);
                    errors.Add($"{Label(entry)}: inviata a Tempo ma non salvata in locale ({ex.Message}). " +
                               "Controlla su Tempo prima di inviarla di nuovo.");
                }
            }
        }
        finally
        {
            _lock.Release();
        }
        return new SyncResult(sent, removed, errors);
    }

    /// <summary>Prima di eliminare una voce in locale: la toglie da Tempo.</summary>
    public async Task DeleteRemoteAsync(TimeEntry entry, CancellationToken ct = default)
    {
        if (entry.Tempo is not { } sync) return;
        await _lock.WaitAsync(ct);
        try
        {
            await Client.DeleteWorklogAsync(sync.WorklogId, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Vero se la voce è stata inviata, falso se è stata tolta da Tempo.</summary>
    private async Task<bool> SyncOneAsync(TimeEntry entry, CancellationToken ct)
    {
        var client = Client;
        if (entry.Jira is not { } issue)
        {
            await client.DeleteWorklogAsync(entry.Tempo!.WorklogId, ct);
            store.SetTempoSync(entry.Id, null);
            return false;
        }

        // I dati inviati sono questi: se l'utente modifica la voce durante l'invio, resterà "da aggiornare".
        var snapshot = TempoSync.Of(entry, 0, DateTime.Now);
        int seconds = TempoWorklog.ToSeconds(entry.Hours);
        if (seconds <= 0) throw new JiraException("ore non valide.");
        var me = await client.MyselfAsync(ct);
        var worklog = new TempoWorklog(me.AccountId, issue.Id, entry.Date,
            StartTime(store.EntriesBetween(entry.Date, entry.Date), entry, Settings.DayStart),
            seconds, TempoWorklog.DescriptionFor(entry));

        snapshot.WorklogId = entry.Tempo is { } old
            ? await client.UpdateWorklogAsync(old.WorklogId, worklog, ct)
            : await client.CreateWorklogAsync(worklog, ct);
        store.SetTempoSync(entry.Id, snapshot);
        return true;
    }

    private static string Label(TimeEntry e) =>
        $"{Fmt.Date(e.Date)} {e.Jira?.Key ?? e.Client} ({Fmt.Hours(e.Hours)} h)";

    /// <summary>
    /// Le voci del giorno su Tempo vanno in fila dall'ora di inizio, nell'ordine in cui sono state inserite:
    /// così il timesheet non mostra tutto sovrapposto alla stessa ora.
    /// </summary>
    public static TimeOnly StartTime(IEnumerable<TimeEntry> dayEntries, TimeEntry entry, TimeOnly dayStart)
    {
        decimal before = dayEntries
            .Where(e => e.Date == entry.Date && e.Id != entry.Id &&
                        (e.CreatedAt < entry.CreatedAt || (e.CreatedAt == entry.CreatedAt && e.Id.CompareTo(entry.Id) < 0)))
            .Sum(e => e.Hours);
        int minutes = dayStart.Hour * 60 + dayStart.Minute + (int)Math.Round(before * 60, MidpointRounding.AwayFromZero);
        minutes = Math.Clamp(minutes, 0, 23 * 60 + 59);
        return new TimeOnly(minutes / 60, minutes % 60);
    }
}
