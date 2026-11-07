using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace ConsuntivoOre;

/// <summary>
/// Vive nell'area di notifica: menu, controllo di promemoria e report, notifiche e gestione delle finestre.
/// </summary>
internal sealed class AppController
{
    private const string ReminderTag = "reminder";
    private const string ReportTag = "report";

    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly DataStore _store;
    private readonly JiraService _jira;
    private readonly TrayIcon _tray;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };

    private MainWindow? _main;
    private ReportWindow? _report;
    private SettingsWindow? _settings;

    public AppController(Application app, IClassicDesktopStyleApplicationLifetime desktop, DataStore store)
    {
        _desktop = desktop;
        _store = store;
        _jira = new JiraService(store, Secrets.For(store));

        var menu = new NativeMenu();
        AddItem(menu, "Consuntiva ore", () => ShowEntry());
        AddItem(menu, "Todo", () => ShowMain(MainTab.Todo));
        AddItem(menu, "Report settimanale", () => ShowReport());
        AddItem(menu, "Non ricordarmelo oggi", () => SkipReminder(Today));
        AddItem(menu, "Impostazioni…", ShowSettings);
        menu.Items.Add(new NativeMenuItemSeparator());
        AddItem(menu, "Esci", Exit);

        _tray = new TrayIcon { Icon = AppAssets.Icon, ToolTipText = "Consuntivo Ore", Menu = menu, IsVisible = true };
        _tray.Clicked += (_, _) => ShowMain();
        TrayIcon.SetIcons(app, [_tray]);

        _store.EntriesChanged += OnEntriesChanged;

        // Primo controllo dopo pochi secondi dall'avvio, poi ogni 20 secondi.
        _timer.Tick += (_, _) =>
        {
            _timer.Interval = TimeSpan.FromSeconds(20);
            CheckSchedule();
        };
        _timer.Start();

        // Un secondo avvio dell'app chiede di riaprire la finestra.
        Program.Instance?.Listen(() => Dispatcher.UIThread.Post(() => ShowMain()));

        UpdateTooltip();

        // Se l'app è stata avviata cliccando una notifica, ci pensa l'azione della notifica ad aprire la finestra giusta.
        bool launchedByToast = Toast.WasLaunchedByClick;
        Toast.Initialize(command => Dispatcher.UIThread.Post(() => OnToastClicked(command)));

        // All'avvio (anche con il sistema) la finestra si apre sulla scheda dei todo.
        if (!launchedByToast) ShowMain(MainTab.Todo);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    private static void AddItem(NativeMenu menu, string header, Action action)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    // ------------------------------------------------------------ finestre

    /// <summary>Mostra la finestra principale; senza scheda indicata resta su quella attiva.</summary>
    private void ShowMain(MainTab? tab = null)
    {
        if (_main is null)
        {
            CreateMain(tab ?? MainTab.Todo);
        }
        else
        {
            if (!_main.ViewModel.Entry.HasUnsavedInput) _main.ViewModel.Entry.MoveToTodayIfStale();
            if (tab is { } t) _main.ShowTab(t);
        }
        BringToFront(_main!);
    }

    private void ShowEntry(DateOnly? day = null)
    {
        if (_main is null)
        {
            CreateMain(MainTab.Entry);
            if (day is { } d) _main!.ViewModel.Entry.SetDay(d);
        }
        else
        {
            var entry = _main.ViewModel.Entry;
            // Con dati in corso di inserimento il giorno non cambia: si azzererebbe il form.
            if (!entry.HasUnsavedInput)
            {
                if (day is { } d) entry.SetDay(d);
                else entry.MoveToTodayIfStale();
            }
            // Non porta via dalla scheda dei todo chi sta scrivendo un todo.
            if (!_main.ViewModel.HasUnsavedInput || entry.HasUnsavedInput) _main.ShowTab(MainTab.Entry);
        }
        BringToFront(_main!);
    }

    private void CreateMain(MainTab tab)
    {
        var vm = new MainViewModel(_store, _jira);
        vm.Entry.SkipRequested = () => SkipReminder(Today);
        _main = new MainWindow(vm);
        _main.ShowTab(tab);
        _main.Closed += (_, _) =>
        {
            vm.Detach();
            _main = null;
        };
    }

    private void ShowReport(DateOnly? week = null)
    {
        if (_report is null)
        {
            _report = new ReportWindow(new ReportViewModel(_store, week ?? Today, _jira));
            _report.Closed += (_, _) => _report = null;
        }
        else if (week is { } w)
        {
            _report.ViewModel.ShowWeek(w);
        }
        BringToFront(_report);
    }

    private void ShowSettings()
    {
        if (_settings is null)
        {
            _settings = new SettingsWindow(_store, _jira, () => Notify("test", "Consuntivo Ore",
                "Le notifiche funzionano correttamente.", new ToastCommand(ToastAction.Entry), () => ShowEntry(),
                ("Apri", new ToastCommand(ToastAction.Entry))));
            _settings.Saved += OnEntriesChanged;
            _settings.Closed += (_, _) => _settings = null;
        }
        BringToFront(_settings);
    }

    private static void BringToFront(Window window)
    {
        if (!window.IsVisible) window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Topmost = true;
        window.Activate();
        window.Topmost = false;
    }

    private void OnEntriesChanged()
    {
        UpdateTooltip();
        // Raggiunte le ore di oggi, il promemoria rimasto tra le notifiche non serve più.
        decimal target = _store.Settings.DailyTargetHours;
        if (target > 0 && _store.HoursOn(Today) >= target) Toast.Remove(ReminderTag);

        _main?.ViewModel.Entry.Reload(); // il totale dipende anche dalle ore previste
        _report?.ViewModel.Refresh();
    }

    private void Exit()
    {
        _timer.Stop();
        foreach (var window in new Window?[] { _main, _report, _settings }) window?.Close();
        _tray.IsVisible = false;
        _tray.Dispose();
        _jira.Dispose();
        _desktop.Shutdown();
    }

    // ------------------------------------------------------------ promemoria e report

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
            ? $"Oggi hai registrato {Fmt.Hours(done)} h su {Fmt.Hours(s.DailyTargetHours)} h."
            : $"Oggi hai registrato {Fmt.Hours(done)} h.";
        Notify(ReminderTag, "È ora di consuntivare le ore", message,
            new ToastCommand(ToastAction.Entry, today), () => ShowEntry(today),
            ("Consuntiva", new ToastCommand(ToastAction.Entry, today)),
            ($"Tra {Scheduler.SnoozeMinutes} minuti", new ToastCommand(ToastAction.Snooze, today)),
            ("Non oggi", new ToastCommand(ToastAction.Skip, today)));

        // Se si sta già scrivendo non ruba il focus: basta la notifica.
        if (s.OpenWindowWithNotification && _main?.ViewModel.HasUnsavedInput != true)
            ShowEntry(today);
    }

    private void CheckWeeklyReport(DateTime now)
    {
        var s = _store.Settings;
        if (Scheduler.DueReportWeek(s, _store.State, now) is not { } weekStart) return;

        _store.State.LastReportWeek = weekStart;
        _store.SaveState();

        decimal total = _store.HoursBetween(weekStart, weekStart.AddDays(6));
        Notify(ReportTag, "Report settimanale",
            $"Questa settimana hai consuntivato {Fmt.Hours(total)} h.",
            new ToastCommand(ToastAction.Report, weekStart), () => ShowReport(weekStart),
            ("Apri il report", new ToastCommand(ToastAction.Report, weekStart)));
        if (s.OpenWindowWithNotification) ShowReport(weekStart);
    }

    /// <summary>
    /// Notifica di sistema con pulsanti; se il sistema non la accetta apre direttamente la finestra (<paramref name="fallback"/>).
    /// </summary>
    private static void Notify(string tag, string title, string text, ToastCommand onClick, Action fallback,
        params (string Label, ToastCommand Command)[] buttons)
    {
        if (!Toast.TryShow(tag, title, text, onClick, buttons)) fallback();
    }

    private void OnToastClicked(ToastCommand command)
    {
        switch (command.Action)
        {
            case ToastAction.Entry:
                ShowEntry(command.Day);
                break;
            case ToastAction.Snooze:
                // Un promemoria di un altro giorno, rimasto tra le notifiche, non si rimanda.
                if (command.Day is null || command.Day == Today)
                    _store.SnoozeReminder(DateTime.Now.AddMinutes(Scheduler.SnoozeMinutes));
                break;
            case ToastAction.Skip:
                SkipReminder(command.Day ?? Today);
                break;
            case ToastAction.Report:
                ShowReport(command.Day);
                break;
            default:
                ShowMain();
                break;
        }
    }

    private void SkipReminder(DateOnly day)
    {
        _store.SkipReminder(day);
        if (day == Today) Toast.Remove(ReminderTag);
    }

    private void UpdateTooltip() =>
        _tray.ToolTipText = $"Consuntivo Ore — oggi {Fmt.Hours(_store.HoursOn(Today))} h";
}
