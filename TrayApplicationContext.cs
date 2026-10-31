namespace ConsuntivoOre;

/// <summary>
/// Vive nell'area di notifica: menu, scheduler di promemoria/report e gestione delle finestre.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int TickMilliseconds = 20_000;

    private readonly DataStore _store = new();
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 5_000 };
    private readonly Control _invoker = new();
    private readonly RegisteredWaitHandle _showWait;

    private EntryForm? _entryForm;
    private ReportForm? _reportForm;
    private SettingsForm? _settingsForm;
    private Action? _balloonAction;

    public TrayApplicationContext(EventWaitHandle showEvent, bool openEntryOnStart)
    {
        _ = _invoker.Handle; // crea l'handle per poter fare marshalling sul thread UI

        var menu = new ContextMenuStrip();
        var entryItem = new ToolStripMenuItem("Consuntiva ore", null, (_, _) => ShowEntry());
        entryItem.Font = new Font(entryItem.Font, FontStyle.Bold);
        menu.Items.AddRange(
        [
            entryItem,
            new ToolStripMenuItem("Report settimanale", null, (_, _) => ShowReport()),
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
            if (e.Button == MouseButtons.Left) ShowEntry();
        };
        _tray.BalloonTipClicked += (_, _) =>
        {
            var action = _balloonAction;
            _balloonAction = null;
            action?.Invoke();
        };

        _store.EntriesChanged += OnEntriesChanged;

        // Primo controllo dopo pochi secondi dall'avvio, poi ogni 20 secondi.
        _timer.Tick += (_, _) =>
        {
            _timer.Interval = TickMilliseconds;
            CheckSchedule();
        };
        _timer.Start();

        // Un secondo avvio dell'exe segnala questo evento per far riaprire la finestra.
        _showWait = ThreadPool.RegisterWaitForSingleObject(showEvent,
            (_, _) => _invoker.BeginInvoke((Action)(() => ShowEntry())),
            null, Timeout.Infinite, executeOnlyOnce: false);

        UpdateTooltip();
        if (openEntryOnStart)
            _invoker.BeginInvoke((Action)(() => ShowEntry()));
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    // ------------------------------------------------------------ finestre

    private void ShowEntry(DateOnly? day = null)
    {
        if (_entryForm is null || _entryForm.IsDisposed)
        {
            _entryForm = new EntryForm(_store, day ?? Today);
            _entryForm.FormClosed += (_, _) => _entryForm = null;
        }
        else if (day is { } d)
        {
            _entryForm.SetDay(d);
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

    private void OnEntriesChanged()
    {
        UpdateTooltip();
        if (_entryForm is { IsDisposed: false }) _entryForm.RefreshList();
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
        if (!s.ReminderEnabled || !s.ReminderDays.Contains(now.DayOfWeek)) return;

        var today = DateOnly.FromDateTime(now);
        var due = today.ToDateTime(s.ReminderTime);
        if (now < due) return;

        if (s.LastReminder is { } last && last >= due)
        {
            // Già avvisato oggi: ripete solo se configurato e se è passato l'intervallo.
            if (s.ReminderRepeatMinutes <= 0 || now - last < TimeSpan.FromMinutes(s.ReminderRepeatMinutes))
                return;
        }

        decimal done = _store.HoursOn(today);
        if (s.DailyTargetHours > 0 && done >= s.DailyTargetHours) return;

        s.LastReminder = now;
        _store.SaveSettings();

        string message = s.DailyTargetHours > 0
            ? $"Oggi hai registrato {Fmt.Hours(done)} h su {Fmt.Hours(s.DailyTargetHours)} h. Clicca per consuntivare."
            : $"Oggi hai registrato {Fmt.Hours(done)} h. Clicca per consuntivare.";
        Notify("È ora di consuntivare le ore", message, () => ShowEntry(today));
        if (s.OpenWindowWithNotification) ShowEntry(today);
    }

    private void CheckWeeklyReport(DateTime now)
    {
        var s = _store.Settings;
        if (!s.ReportEnabled) return;

        var weekStart = Fmt.WeekStart(DateOnly.FromDateTime(now));
        var due = weekStart.AddDays(Fmt.MondayIndex(s.ReportDay)).ToDateTime(s.ReportTime);
        // Se il PC era spento all'orario previsto, il report arriva al primo avvio successivo della stessa settimana.
        if (now < due || s.LastReportWeek == weekStart) return;

        s.LastReportWeek = weekStart;
        _store.SaveSettings();

        decimal total = _store.HoursBetween(weekStart, weekStart.AddDays(6));
        Notify("Report settimanale",
            $"Questa settimana hai consuntivato {Fmt.Hours(total)} h. Clicca per vedere il dettaglio.",
            () => ShowReport(weekStart));
        if (s.OpenWindowWithNotification) ShowReport(weekStart);
    }

    private void Notify(string title, string text, Action onClick)
    {
        _balloonAction = onClick;
        _tray.ShowBalloonTip(10_000, title, text, ToolTipIcon.Info);
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
