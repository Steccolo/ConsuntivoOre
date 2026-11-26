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

public sealed record ImportResult(int Added, int Updated, int Removed)
{
    public bool Changed => Added + Updated + Removed > 0;

    public string Summary()
    {
        var parts = new List<string>();
        if (Added > 0) parts.Add(Added == 1 ? "1 voce importata da Jira" : $"{Added} voci importate da Jira");
        if (Updated > 0) parts.Add(Updated == 1 ? "1 aggiornata" : $"{Updated} aggiornate");
        if (Removed > 0) parts.Add(Removed == 1 ? "1 tolta perché cancellata su Tempo" : $"{Removed} tolte perché cancellate su Tempo");
        return parts.Count == 0 ? "Già allineato con Jira." : string.Join(", ", parts) + ".";
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

    /// <summary>Il token di Tempo c'è: si possono importare e inviare le ore.</summary>
    public bool CanUseTempo => IsEnabled && Credentials.TempoToken.Trim().Length > 0;

    /// <summary>
    /// Porta in locale i miei worklog di Tempo dei giorni indicati. Il web vince sulle voci già inviate e non
    /// modificate: si aggiornano, o si tolgono se su Tempo non ci sono più. Le voci con modifiche locali non ancora
    /// inviate restano come sono. I worklog che in locale mancano diventano voci nuove, già "inviate".
    /// </summary>
    public async Task<ImportResult> ImportAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var client = Client;
            var remote = await client.MyWorklogsAsync(from, to, ct);
            var local = store.EntriesBetween(from, to);
            var byWorklog = store.EntriesBetween(DateOnly.MinValue, DateOnly.MaxValue)
                .Where(e => e.Tempo is not null)
                .GroupBy(e => e.Tempo!.WorklogId)
                .ToDictionary(g => g.Key, g => g.First());

            // Dati delle issue solo per quelle che servono (voci nuove o spostate su un'altra issue).
            var needed = remote.Where(w => !byWorklog.TryGetValue(w.WorklogId, out var e) || e.Jira?.Id != w.IssueId)
                               .Select(w => w.IssueId).Distinct().ToList();
            var issues = needed.Count == 0
                ? new Dictionary<long, JiraIssueRef>()
                : (await client.IssuesByIdAsync(needed, ct)).ToDictionary(i => i.Id);
            JiraIssueRef IssueFor(long id) =>
                issues.TryGetValue(id, out var issue) ? issue : new JiraIssueRef { Id = id, Key = $"#{id}" };

            int added = 0, updated = 0, removed = 0;
            var now = DateTime.Now;
            foreach (var w in remote)
            {
                if (byWorklog.TryGetValue(w.WorklogId, out var entry))
                {
                    if (entry.JiraStatus != JiraStatus.Sent || SameAsRemote(entry, w)) continue;
                    var jira = entry.Jira?.Id == w.IssueId ? entry.Jira! : IssueFor(w.IssueId);
                    decimal hours = TempoWorklog.ToSeconds(entry.Hours) == w.Seconds ? entry.Hours : HoursOf(w.Seconds);
                    string note = TempoWorklog.DescriptionFor(entry) == w.Description ? entry.Note : w.Description;
                    var sync = new TempoSync { WorklogId = w.WorklogId, IssueId = jira.Id, Date = w.Date, Hours = hours, Note = note, SentAt = now };
                    store.ApplyRemote(entry.Id, w.Date, hours, note, jira, sync);
                    updated++;
                }
                else
                {
                    var jira = IssueFor(w.IssueId);
                    var created = new TimeEntry
                    {
                        Date = w.Date,
                        Hours = HoursOf(w.Seconds),
                        Client = jira.ClientName,
                        Note = w.Description,
                        Jira = jira,
                        // Nell'elenco del giorno compare nell'ordine in cui è su Tempo.
                        CreatedAt = w.Date.ToDateTime(w.StartTime ?? TimeOnly.MinValue),
                    };
                    created.Tempo = TempoSync.Of(created, w.WorklogId, now);
                    store.Add(created);
                    added++;
                }
            }

            var remoteIds = remote.Select(w => w.WorklogId).ToHashSet();
            foreach (var entry in local.Where(e => e.Tempo is { } t && !remoteIds.Contains(t.WorklogId) && e.JiraStatus == JiraStatus.Sent))
            {
                store.Delete(entry.Id);
                removed++;
            }
            return new ImportResult(added, updated, removed);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static decimal HoursOf(int seconds) => Math.Round(seconds / 3600m, 2);

    private static bool SameAsRemote(TimeEntry e, TempoRemoteWorklog w) =>
        e.Date == w.Date && e.Jira?.Id == w.IssueId && TempoWorklog.ToSeconds(e.Hours) == w.Seconds &&
        TempoWorklog.DescriptionFor(e) == w.Description;

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
            StartTime(store.EntriesBetween(entry.Date, entry.Date), entry, store.Settings.DayStart),
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
