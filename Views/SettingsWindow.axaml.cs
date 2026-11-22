using System.Diagnostics;
using Avalonia.Controls;

namespace ConsuntivoOre.Views;

public partial class SettingsWindow : Window
{
    private const string JiraTokenPage = "https://id.atlassian.com/manage-profile/security/api-tokens";

    private readonly DataStore _store;
    private readonly JiraService? _jira;
    private readonly CheckBox[] _reminderDays;
    private readonly CheckBox[] _workDays;

    /// <summary>Sollevato dopo il salvataggio, per aggiornare tooltip e finestre aperte.</summary>
    public event Action? Saved;

    // Richiesto dal caricatore XAML in fase di progettazione.
    public SettingsWindow() : this(new DataStore(Path.GetTempPath()), null, () => { }) { }

    public SettingsWindow(DataStore store, JiraService? jira, Action sendTestNotification)
    {
        InitializeComponent();
        _store = store;
        _jira = jira;

        JiraEnabled.IsCheckedChanged += (_, _) => UpdateEnabled();
        OpenJiraTokenPage.Click += (_, _) => OpenUrl(JiraTokenPage);
        TestJira.Click += async (_, _) => await TestJiraConnection();

        _reminderDays = DayBoxes(ReminderDays);
        _workDays = DayBoxes(WorkDays);
        ReportDay.ItemsSource = Fmt.WeekDays.Select(Fmt.DayName).ToList();

        ReminderEnabled.IsCheckedChanged += (_, _) => UpdateEnabled();
        ReportEnabled.IsCheckedChanged += (_, _) => UpdateEnabled();
        WorkStart.SelectedTimeChanged += (_, _) => UpdateWorkDaySummary();
        WorkEnd.SelectedTimeChanged += (_, _) => UpdateWorkDaySummary();
        BreakMinutes.ValueChanged += (_, _) => UpdateWorkDaySummary();
        OpenFolder.Click += (_, _) => OpenDataFolder();
        TestNotification.Click += (_, _) => sendTestNotification();
        CancelButton.Click += (_, _) => Close();
        SaveButton.Click += async (_, _) => await Save();

        LoadValues();
    }

    private static CheckBox[] DayBoxes(WrapPanel panel)
    {
        var boxes = Fmt.WeekDays
            .Select(d => new CheckBox { Content = Fmt.DayName(d)[..3], Tag = d, Margin = new Avalonia.Thickness(0, 0, 10, 0) })
            .ToArray();
        panel.Children.AddRange(boxes);
        return boxes;
    }

    private void LoadValues()
    {
        var s = _store.Settings;
        WorkStart.SelectedTime = s.DayStart.ToTimeSpan();
        WorkEnd.SelectedTime = s.DayEnd.ToTimeSpan();
        BreakMinutes.Value = Math.Clamp(s.BreakMinutes ?? 0, 0, 480);
        ReminderEnabled.IsChecked = s.ReminderEnabled;
        foreach (var box in _reminderDays) box.IsChecked = s.ReminderDays.Contains((DayOfWeek)box.Tag!);
        Repeat.Value = Math.Clamp(s.ReminderRepeatMinutes, 0, 240);
        foreach (var box in _workDays) box.IsChecked = s.WorkDays.Contains((DayOfWeek)box.Tag!);
        ReportEnabled.IsChecked = s.ReportEnabled;
        ReportDay.SelectedIndex = Fmt.MondayIndex(s.ReportDay);
        ReportTime.SelectedTime = s.ReportTime.ToTimeSpan();
        OpenWindow.IsChecked = s.OpenWindowWithNotification;
        AutostartBox.IsChecked = SafeAutostartEnabled();

        var j = s.Jira;
        JiraEnabled.IsChecked = j.Enabled;
        JiraSite.Text = j.SiteUrl;
        JiraEmail.Text = j.Email;
        TempoUrl.Text = j.TempoApiUrl;
        JiraToken.Text = _jira?.Credentials.JiraToken ?? "";
        TempoToken.Text = _jira?.Credentials.TempoToken ?? "";
        UpdateEnabled();
        UpdateWorkDaySummary();
    }

    private (TimeOnly Start, TimeOnly End, int Break) WorkDayFromForm()
    {
        var s = _store.Settings;
        return (ToTime(WorkStart.SelectedTime, s.DayStart), ToTime(WorkEnd.SelectedTime, s.DayEnd), (int)(BreakMinutes.Value ?? 0));
    }

    /// <summary>Mostra subito cosa ne deriva: ore previste al giorno e orario del promemoria.</summary>
    private void UpdateWorkDaySummary()
    {
        var (start, end, pause) = WorkDayFromForm();
        WorkDaySummary.Text = end <= start
            ? "La fine deve venire dopo l'inizio."
            : $"Ore previste: {Fmt.Hours(AppSettings.TargetHours(start, end, pause))} h al giorno · " +
              $"promemoria alle {end:HH\\:mm} · su Tempo la prima voce del giorno parte alle {start:HH\\:mm}";
    }

    /// <summary>Impostazioni Jira come sono nel form (non ancora salvate).</summary>
    private (JiraSettings Settings, JiraCredentials Credentials) JiraFromForm()
    {
        var current = _store.Settings.Jira;
        var settings = new JiraSettings
        {
            Enabled = JiraEnabled.IsChecked == true,
            SiteUrl = JiraSite.Text ?? "",
            Email = JiraEmail.Text ?? "",
            TempoApiUrl = TempoUrl.Text ?? "",
            SendImmediately = current.SendImmediately,
            DayStart = WorkDayFromForm().Start,
        };
        settings.Normalize();
        var creds = new JiraCredentials { JiraToken = (JiraToken.Text ?? "").Trim(), TempoToken = (TempoToken.Text ?? "").Trim() };
        return (settings, creds);
    }

    private async Task TestJiraConnection()
    {
        var (settings, creds) = JiraFromForm();
        TestJira.IsEnabled = false;
        JiraTestResult.IsVisible = true;
        JiraTestResult.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(107, 114, 128));
        JiraTestResult.Text = "Verifica in corso…";
        try
        {
            JiraTestResult.Text = await JiraService.TestAsync(settings, creds);
            JiraTestResult.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(21, 128, 61));
        }
        catch (JiraException ex)
        {
            JiraTestResult.Text = ex.Message;
            JiraTestResult.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(220, 38, 38));
        }
        finally
        {
            TestJira.IsEnabled = true;
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private async Task Save()
    {
        var days = Checked(_reminderDays);
        if (ReminderEnabled.IsChecked == true && days.Count == 0)
        {
            await MessageDialog.Show(this, "Seleziona almeno un giorno per il promemoria.");
            return;
        }

        var (start, end, pause) = WorkDayFromForm();
        if (end <= start)
        {
            await MessageDialog.Show(this, "La fine della giornata lavorativa deve venire dopo l'inizio.");
            return;
        }

        var (jiraSettings, jiraCreds) = JiraFromForm();
        if (jiraSettings.Enabled && (jiraSettings.SiteUrl.Length == 0 || jiraSettings.Email.Length == 0 || jiraCreds.JiraToken.Length == 0))
        {
            await MessageDialog.Show(this, "Per usare Jira indica indirizzo, email e token API (il token Tempo serve per inviare le ore).");
            return;
        }

        var s = _store.Settings;
        s.Jira = jiraSettings;
        // Orario del promemoria e ore previste li ricalcola Normalize da inizio, fine e pausa.
        (s.WorkStart, s.WorkEnd, s.BreakMinutes) = (start, end, pause);
        s.ReminderEnabled = ReminderEnabled.IsChecked == true;
        s.ReminderDays = days;
        s.ReminderRepeatMinutes = (int)(Repeat.Value ?? 0);
        s.WorkDays = Checked(_workDays);
        s.ReportEnabled = ReportEnabled.IsChecked == true;
        s.ReportDay = Fmt.WeekDays[Math.Max(0, ReportDay.SelectedIndex)];
        s.ReportTime = ToTime(ReportTime.SelectedTime, s.ReportTime);
        s.OpenWindowWithNotification = OpenWindow.IsChecked == true;
        s.Normalize();
        try
        {
            _store.SaveSettings();
        }
        catch (Exception ex) when (DataStore.IsFileError(ex))
        {
            Log.Error(ex);
            await MessageDialog.Show(this, $"Impossibile salvare le impostazioni:\n{ex.Message}");
            return;
        }

        try
        {
            bool wanted = AutostartBox.IsChecked == true;
            if (wanted != Autostart.IsEnabled) Autostart.SetEnabled(wanted);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            await MessageDialog.Show(this, $"Impossibile modificare l'avvio automatico:\n{ex.Message}");
        }

        if (_jira is not null)
        {
            try
            {
                var old = _jira.Credentials;
                if (old.JiraToken != jiraCreds.JiraToken || old.TempoToken != jiraCreds.TempoToken)
                    _jira.SaveCredentials(jiraCreds);
                else
                    _jira.Reset();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                _jira.Reset();
                await MessageDialog.Show(this, $"Impossibile salvare i token di Jira e Tempo:\n{ex.Message}");
            }
        }

        Saved?.Invoke();
        Close();
    }

    private void UpdateEnabled()
    {
        ReminderPanel.IsEnabled = ReminderEnabled.IsChecked == true;
        ReportPanel.IsEnabled = ReportEnabled.IsChecked == true;
        JiraPanel.IsEnabled = TestJira.IsEnabled = JiraEnabled.IsChecked == true;
    }

    private void OpenDataFolder()
    {
        try
        {
            string opener = OperatingSystem.IsWindows() ? "explorer.exe" : "xdg-open";
            Process.Start(new ProcessStartInfo(opener, $"\"{_store.DataDirectory}\"") { UseShellExecute = false })?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private static bool SafeAutostartEnabled()
    {
        try
        {
            return Autostart.IsEnabled;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return false;
        }
    }

    private static List<DayOfWeek> Checked(IEnumerable<CheckBox> boxes) =>
        boxes.Where(b => b.IsChecked == true).Select(b => (DayOfWeek)b.Tag!).ToList();

    private static TimeOnly ToTime(TimeSpan? value, TimeOnly fallback) =>
        value is { } t ? new TimeOnly(t.Hours, t.Minutes) : fallback;

    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
