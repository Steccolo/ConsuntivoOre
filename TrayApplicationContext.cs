namespace ConsuntivoOre;

/// <summary>
/// Vive nell'area di notifica: menu, scheduler di promemoria/report e gestione delle finestre.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int TickMilliseconds = 20_000;
    private const int BalloonMilliseconds = 10_000;

    private readonly DataStore _store;
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 5_000 };
    private readonly Control _invoker = new();
    private readonly RegisteredWaitHandle _showWait;
    private readonly Queue<(string Title, string Text, Action OnClick)> _notifications = new();

    private EntryForm? _entryForm;
    private ReportForm? _reportForm;
    private SettingsForm? _settingsForm;
    private Action? _balloonAction;
    private DateTime? _balloonShownAt;

    public TrayApplicationContext(DataStore store, EventWaitHandle showEvent)
    {
        _store = store;
        _ = _invoker.Handle; // crea l'handle per poter fare marshalling sul thread UI

        var menu = new ContextMenuStrip();
        var entryItem = new ToolStripMenuItem("Consuntiva ore", null, (_, _) => ShowEntry());
        entryItem.Font = new Font(entryItem.Font, FontStyle.Bold);
        menu.Items.AddRange(
        [
            entryItem,
            new ToolStripMenuItem("Todo", null, (_, _) => ShowMain(MainTab.Todo)),
            new ToolStripMenuItem("Report settimanale", null, (_, _) => ShowReport()),
            new ToolStripMenuItem("Non ricordarmelo oggi", null, (_, _) => _store.SkipReminder(Today)),
            new ToolStripMenuItem("Impostazioni…", null, (_, _) => ShowSettings()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Esci", null, (_, _) => ExitThread()),
        ]);

        _tray = new NotifyIcon
        {
            Icon = AppIcon.Tray,
            Text = "Consuntivo Ore",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ShowMain();
        };
        _tray.BalloonTipClicked += (_, _) =>
        {
            var action = _balloonAction;
            ShowNextNotification(force: true);
            action?.Invoke();
        };
        _tray.BalloonTipClosed += (_, _) =>
        {
            // La chiusura di una notifica sostituita arriva subito dopo la nuova: non deve toglierle l'azione.
            if (_balloonShownAt is { } shown && DateTime.Now - shown > TimeSpan.FromSeconds(2))
                ShowNextNotification(force: true);
        };

        _store.EntriesChanged += OnEntriesChanged;
        _store.TodosChanged += OnTodosChanged;

        // Primo controllo dopo pochi secondi dall'avvio, poi ogni 20 secondi.
        _timer.Tick += (_, _) =>
        {
            _timer.Interval = TickMilliseconds;
            CheckSchedule();
            ShowNextNotification(force: false);
        };
        _timer.Start();

        // Un secondo avvio dell'exe segnala questo evento per far riaprire la finestra.
        _showWait = ThreadPool.RegisterWaitForSingleObject(showEvent,
            (_, _) => _invoker.BeginInvoke((Action)(() => ShowMain())),
            null, Timeout.Infinite, executeOnlyOnce: false);

        UpdateTooltip();
        // All'avvio (anche con Windows) la finestra si apre sulla scheda dei todo.
        _invoker.BeginInvoke((Action)(() => ShowMain(MainTab.Todo)));
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    // ------------------------------------------------------------ finestre

    /// <summary>Mostra la finestra principale; senza scheda indicata resta su quella attiva.</summary>
    private void ShowMain(MainTab? tab = null)
    {
        if (_entryForm is null || _entryForm.IsDisposed)
        {
            _entryForm = new EntryForm(_store, Today, tab ?? MainTab.Todo);
            _entryForm.FormClosed += (_, _) => _entryForm = null;
        }
        else
        {
            if (!_entryForm.HasUnsavedEntry) _entryForm.MoveToTodayIfStale();
            if (tab is { } t) _entryForm.ShowTab(t);
        }
        Ui.BringToFront(_entryForm);
    }

    private void ShowEntry(DateOnly? day = null)
    {
        if (_entryForm is null || _entryForm.IsDisposed)
        {
            _entryForm = new EntryForm(_store, day ?? Today, MainTab.Entry);
            _entryForm.FormClosed += (_, _) => _entryForm = null;
        }
        else
        {
            // Con dati in corso di inserimento il giorno non cambia: si azzererebbe il form.
            if (!_entryForm.HasUnsavedEntry)
            {
                if (day is { } d) _entryForm.SetDay(d);
                else _entryForm.MoveToTodayIfStale();
            }
            // Non porta via dalla scheda dei todo chi sta scrivendo un todo.
            if (!_entryForm.HasUnsavedInput || _entryForm.HasUnsavedEntry) _entryForm.ShowTab(MainTab.Entry);
        }
        Ui.BringToFront(_entryForm);
    }

    private void ShowReport(DateOnly? week = null)
    {
        if (_reportForm is null || _reportForm.IsDisposed)
        {
            _reportForm = new ReportForm(_store, week ?? Today);
            _reportForm.FormClosed += (_, _) => _reportForm = null;
        }
        else if (week is { } w)
        {
            _reportForm.ShowWeek(w);
        }
        Ui.BringToFront(_reportForm);
    }

    private void ShowSettings()
    {
        if (_settingsForm is null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(_store,
                () => Notify("Consuntivo Ore", "Le notifiche funzionano correttamente.", () => ShowEntry()));
            _settingsForm.Saved += OnEntriesChanged;
            _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        }
        Ui.BringToFront(_settingsForm);
    }

    private void OnTodosChanged()
    {
        if (_entryForm is { IsDisposed: false }) _entryForm.RefreshTodos();
    }

    private void OnEntriesChanged()
    {
        UpdateTooltip();
        if (_entryForm is { IsDisposed: false })
        {
            _entryForm.RefreshList();
            _entryForm.RefreshTodos(); // colonna delle ore fatte
        }
        if (_reportForm is { IsDisposed: false }) _reportForm.RefreshData();
    }

    // ------------------------------------------------------------ scheduler

    private void CheckSchedule()
    {
        try
        {
            var now = DateTime.Now;
            CheckReminder(now);
            CheckWeeklyReport(now);
            UpdateTooltip();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private void CheckReminder(DateTime now)
    {
        var s = _store.Settings;
        var today = DateOnly.FromDateTime(now);
        decimal done = _store.HoursOn(today);
        if (!Scheduler.IsReminderDue(s, _store.State, now, done)) return;

        _store.State.LastReminder = now;
        _store.SaveState();

        string message = s.DailyTargetHours > 0
            ? $"Oggi hai registrato {Fmt.Hours(done)} h su {Fmt.Hours(s.DailyTargetHours)} h. Clicca per consuntivare."
            : $"Oggi hai registrato {Fmt.Hours(done)} h. Clicca per consuntivare.";
        Notify("È ora di consuntivare le ore", message, () => ShowEntry(today));

        // Se si sta già scrivendo non ruba il focus: basta la notifica.
        if (s.OpenWindowWithNotification && _entryForm is not { IsDisposed: false, HasUnsavedInput: true })
            ShowEntry(today);
    }

    private void CheckWeeklyReport(DateTime now)
    {
        var s = _store.Settings;
        if (Scheduler.DueReportWeek(s, _store.State, now) is not { } weekStart) return;

        _store.State.LastReportWeek = weekStart;
        _store.SaveState();

        decimal total = _store.HoursBetween(weekStart, weekStart.AddDays(6));
        Notify("Report settimanale",
            $"Questa settimana hai consuntivato {Fmt.Hours(total)} h. Clicca per vedere il dettaglio.",
            () => ShowReport(weekStart));
        if (s.OpenWindowWithNotification) ShowReport(weekStart);
    }

    // Una notifica alla volta: una nuova sostituirebbe quella visibile e la sua azione al clic.
    private void Notify(string title, string text, Action onClick)
    {
        _notifications.Enqueue((title, text, onClick));
        ShowNextNotification(force: false);
    }

    private void ShowNextNotification(bool force)
    {
        // Il tick copre i casi in cui Windows non segnala la chiusura della notifica.
        bool busy = _balloonShownAt is { } shown && DateTime.Now - shown < TimeSpan.FromMilliseconds(BalloonMilliseconds);
        if (busy && !force) return;

        _balloonAction = null;
        _balloonShownAt = null;
        if (!_notifications.TryDequeue(out var next)) return;

        _balloonAction = next.OnClick;
        _balloonShownAt = DateTime.Now;
        _tray.ShowBalloonTip(BalloonMilliseconds, next.Title, next.Text, ToolTipIcon.Info);
    }

    private void UpdateTooltip() =>
        _tray.Text = $"Consuntivo Ore — oggi {Fmt.Hours(_store.HoursOn(Today))} h";

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _showWait.Unregister(null);
        foreach (var form in new Form?[] { _entryForm, _reportForm, _settingsForm })
            form?.Close();
        _tray.Visible = false;
        _tray.Dispose();
        base.ExitThreadCore();
    }
}
