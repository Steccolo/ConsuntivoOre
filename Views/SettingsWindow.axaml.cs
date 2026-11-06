using System.Diagnostics;
using Avalonia.Controls;

namespace ConsuntivoOre.Views;

public partial class SettingsWindow : Window
{
    private readonly DataStore _store;
    private readonly CheckBox[] _reminderDays;
    private readonly CheckBox[] _workDays;

    /// <summary>Sollevato dopo il salvataggio, per aggiornare tooltip e finestre aperte.</summary>
    public event Action? Saved;

    // Richiesto dal caricatore XAML in fase di progettazione.
    public SettingsWindow() : this(new DataStore(Path.GetTempPath()), () => { }) { }

    public SettingsWindow(DataStore store, Action sendTestNotification)
    {
        InitializeComponent();
        _store = store;

        _reminderDays = DayBoxes(ReminderDays);
        _workDays = DayBoxes(WorkDays);
        ReportDay.ItemsSource = Fmt.WeekDays.Select(Fmt.DayName).ToList();

        ReminderEnabled.IsCheckedChanged += (_, _) => UpdateEnabled();
        ReportEnabled.IsCheckedChanged += (_, _) => UpdateEnabled();
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
        ReminderEnabled.IsChecked = s.ReminderEnabled;
        ReminderTime.SelectedTime = s.ReminderTime.ToTimeSpan();
        foreach (var box in _reminderDays) box.IsChecked = s.ReminderDays.Contains((DayOfWeek)box.Tag!);
        Repeat.Value = Math.Clamp(s.ReminderRepeatMinutes, 0, 240);
        Target.Value = Math.Clamp(s.DailyTargetHours, 0, 24);
        foreach (var box in _workDays) box.IsChecked = s.WorkDays.Contains((DayOfWeek)box.Tag!);
        ReportEnabled.IsChecked = s.ReportEnabled;
        ReportDay.SelectedIndex = Fmt.MondayIndex(s.ReportDay);
        ReportTime.SelectedTime = s.ReportTime.ToTimeSpan();
        OpenWindow.IsChecked = s.OpenWindowWithNotification;
        AutostartBox.IsChecked = SafeAutostartEnabled();
        UpdateEnabled();
    }

    private async Task Save()
    {
        var days = Checked(_reminderDays);
        if (ReminderEnabled.IsChecked == true && days.Count == 0)
        {
            await MessageDialog.Show(this, "Seleziona almeno un giorno per il promemoria.");
            return;
        }

        var s = _store.Settings;
        s.ReminderEnabled = ReminderEnabled.IsChecked == true;
        s.ReminderTime = ToTime(ReminderTime.SelectedTime, s.ReminderTime);
        s.ReminderDays = days;
        s.ReminderRepeatMinutes = (int)(Repeat.Value ?? 0);
        s.DailyTargetHours = Target.Value ?? 0;
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

        Saved?.Invoke();
        Close();
    }

    private void UpdateEnabled()
    {
        ReminderPanel.IsEnabled = ReminderEnabled.IsChecked == true;
        ReportPanel.IsEnabled = ReportEnabled.IsChecked == true;
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
