using System.Diagnostics;

namespace ConsuntivoOre.UI;

internal sealed class SettingsForm : BaseForm
{
    private readonly DataStore _store;

    private readonly CheckBox _reminderEnabled = new() { Text = "Ricordami di consuntivare le ore", AutoSize = true };
    private readonly DateTimePicker _reminderTime = TimePicker();
    private readonly CheckBox[] _days = Fmt.WeekDays
        .Select(d => new CheckBox { Text = Fmt.DayName(d)[..3], AutoSize = true, Tag = d })
        .ToArray();
    private readonly NumericUpDown _repeat = new() { Minimum = 0, Maximum = 240, Increment = 5, Width = 70 };
    private readonly NumericUpDown _target = new() { Minimum = 0, Maximum = 24, Increment = 0.5m, DecimalPlaces = 1, Width = 70 };

    private readonly CheckBox _reportEnabled = new() { Text = "Inviami il report settimanale", AutoSize = true };
    private readonly ComboBox _reportDay = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly DateTimePicker _reportTime = TimePicker();

    private readonly CheckBox _openWindow = new() { Text = "Apri la finestra insieme alla notifica", AutoSize = true };
    private readonly CheckBox _autostart = new() { Text = "Avvia automaticamente all'accensione del PC", AutoSize = true };

    public event Action? Saved;

    public SettingsForm(DataStore store, Action sendTestNotification)
    {
        _store = store;
        Text = "Impostazioni — Consuntivo Ore";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        // --- promemoria
        var reminder = Ui.Grid(SizeType.AutoSize, SizeType.AutoSize);
        reminder.Controls.Add(_reminderEnabled, 0, 0);
        reminder.SetColumnSpan(_reminderEnabled, 2);
        reminder.Controls.Add(Ui.Label("Orario"), 0, 1);
        reminder.Controls.Add(_reminderTime, 1, 1);
        reminder.Controls.Add(Ui.Label("Giorni"), 0, 2);
        reminder.Controls.Add(Ui.Flow(_days), 1, 2);
        reminder.Controls.Add(Ui.Label("Ore giornaliere previste"), 0, 3);
        reminder.Controls.Add(Ui.Flow(_target, Hint("h  (se le raggiungi non ricevi il promemoria)")), 1, 3);
        reminder.Controls.Add(Ui.Label("Ripeti ogni"), 0, 4);
        reminder.Controls.Add(Ui.Flow(_repeat, Hint("minuti finché non le raggiungi  (0 = non ripetere)")), 1, 4);

        // --- report
        _reportDay.Items.AddRange(Fmt.WeekDays.Select(Fmt.DayName).ToArray<object>());
        var report = Ui.Grid(SizeType.AutoSize, SizeType.AutoSize);
        report.Controls.Add(_reportEnabled, 0, 0);
        report.SetColumnSpan(_reportEnabled, 2);
        report.Controls.Add(Ui.Label("Giorno"), 0, 1);
        report.Controls.Add(_reportDay, 1, 1);
        report.Controls.Add(Ui.Label("Orario"), 0, 2);
        report.Controls.Add(_reportTime, 1, 2);

        // --- generale
        var general = Ui.Grid(SizeType.AutoSize);
        general.Controls.Add(_openWindow, 0, 0);
        general.Controls.Add(_autostart, 0, 1);
        general.Controls.Add(Ui.Flow(
            Ui.Button("Apri cartella dati", () => Process.Start("explorer.exe", DataStore.DataDirectory), 140),
            Ui.Button("Invia notifica di prova", sendTestNotification, 160)), 0, 2);

        // --- pulsanti
        var buttons = Ui.Flow(Ui.Button("Annulla", Close), Ui.Button("Salva", Save));
        buttons.FlowDirection = FlowDirection.RightToLeft;
        buttons.Anchor = AnchorStyles.Right;
        buttons.Margin = new Padding(0, 6, 0, 0);

        var root = Ui.Grid(SizeType.AutoSize);
        root.Padding = new Padding(12);
        root.Controls.Add(Ui.Group("Promemoria consuntivazione", reminder), 0, 0);
        root.Controls.Add(Ui.Group("Report settimanale", report), 0, 1);
        root.Controls.Add(Ui.Group("Generale", general), 0, 2);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);

        _reminderEnabled.CheckedChanged += (_, _) => UpdateEnabled();
        _reportEnabled.CheckedChanged += (_, _) => UpdateEnabled();
        LoadValues();
    }

    private void LoadValues()
    {
        var s = _store.Settings;
        _reminderEnabled.Checked = s.ReminderEnabled;
        _reminderTime.Value = DateTime.Today.Add(s.ReminderTime.ToTimeSpan());
        foreach (var box in _days) box.Checked = s.ReminderDays.Contains((DayOfWeek)box.Tag!);
        _repeat.Value = Math.Clamp(s.ReminderRepeatMinutes, 0, 240);
        _target.Value = Math.Clamp(s.DailyTargetHours, 0, 24);
        _reportEnabled.Checked = s.ReportEnabled;
        _reportDay.SelectedIndex = Fmt.MondayIndex(s.ReportDay);
        _reportTime.Value = DateTime.Today.Add(s.ReportTime.ToTimeSpan());
        _openWindow.Checked = s.OpenWindowWithNotification;
        _autostart.Checked = Autostart.IsEnabled;
        UpdateEnabled();
    }

    private void Save()
    {
        var days = _days.Where(b => b.Checked).Select(b => (DayOfWeek)b.Tag!).ToList();
        if (_reminderEnabled.Checked && days.Count == 0)
        {
            Ui.ShowWarning(this, "Seleziona almeno un giorno per il promemoria.");
            return;
        }

        var s = _store.Settings;
        s.ReminderEnabled = _reminderEnabled.Checked;
        s.ReminderTime = ToTime(_reminderTime.Value);
        s.ReminderDays = days;
        s.ReminderRepeatMinutes = (int)_repeat.Value;
        s.DailyTargetHours = _target.Value;
        s.ReportEnabled = _reportEnabled.Checked;
        s.ReportDay = Fmt.WeekDays[_reportDay.SelectedIndex];
        s.ReportTime = ToTime(_reportTime.Value);
        s.OpenWindowWithNotification = _openWindow.Checked;
        _store.SaveSettings();

        try
        {
            Autostart.SetEnabled(_autostart.Checked);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            Ui.ShowWarning(this, $"Impossibile modificare l'avvio automatico:\n{ex.Message}");
        }

        Saved?.Invoke();
        Close();
    }

    private void UpdateEnabled()
    {
        foreach (Control c in new Control[] { _reminderTime, _repeat, _target }.Concat(_days))
            c.Enabled = _reminderEnabled.Checked;
        _reportDay.Enabled = _reportTime.Enabled = _reportEnabled.Checked;
    }

    private static TimeOnly ToTime(DateTime value) => new(value.Hour, value.Minute);

    private static DateTimePicker TimePicker() => new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "HH:mm",
        ShowUpDown = true,
        Width = 80,
    };

    private static Label Hint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Margin = new Padding(6, 7, 3, 3),
    };

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
