using System.Text;

namespace ConsuntivoOre.UI;

/// <summary>Report settimanale: dettaglio per giorno, totali per cliente, esportazione.</summary>
internal sealed class ReportForm : BaseForm
{
    private readonly DataStore _store;
    private readonly Label _title = new() { AutoSize = true, Margin = new Padding(10, 7, 10, 3) };
    private readonly ListView _details = new()
    {
        View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill, ShowGroups = true,
    };
    private readonly ListView _byClient = new()
    {
        View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill,
    };
    private readonly Label _total = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) };

    private DateOnly _weekStart;
    private IReadOnlyList<TimeEntry> _entries = [];

    public ReportForm(DataStore store, DateOnly anyDayOfWeek)
    {
        _store = store;
        _weekStart = Fmt.WeekStart(anyDayOfWeek);
        Text = "Report settimanale";
        ClientSize = new Size(720, 620);
        MinimumSize = new Size(600, 500);
        KeyPreview = true;

        _title.Font = new Font(Font.FontFamily, 11.5F, FontStyle.Bold);
        _total.Font = new Font(Font, FontStyle.Bold);

        var nav = Ui.Flow(
            Ui.Button("◀", () => ShowWeek(_weekStart.AddDays(-7)), 36),
            _title,
            Ui.Button("▶", () => ShowWeek(_weekStart.AddDays(7)), 36),
            Ui.Button("Questa settimana", () => ShowWeek(DateOnly.FromDateTime(DateTime.Today)), 130));

        _details.Columns.Add("Cliente", LogicalToDeviceUnits(170));
        _details.Columns.Add("Ore", LogicalToDeviceUnits(60), HorizontalAlignment.Right);
        _details.Columns.Add("Nota", LogicalToDeviceUnits(400));
        _details.Resize += (_, _) => FitLastColumn(_details);

        _byClient.Columns.Add("Cliente", LogicalToDeviceUnits(250));
        _byClient.Columns.Add("Ore", LogicalToDeviceUnits(80), HorizontalAlignment.Right);
        _byClient.Columns.Add("%", LogicalToDeviceUnits(80), HorizontalAlignment.Right);
        _byClient.Resize += (_, _) => FitLastColumn(_byClient);

        var detailsGroup = Ui.Group("Dettaglio per giorno", _details);
        detailsGroup.AutoSize = false;
        var clientsGroup = Ui.Group("Totale per cliente", _byClient);
        clientsGroup.AutoSize = false;

        var bottom = Ui.Grid(SizeType.Percent, SizeType.AutoSize);
        bottom.Controls.Add(_total, 0, 0);
        bottom.Controls.Add(Ui.Flow(
            Ui.Button("Copia testo", CopyText, 110),
            Ui.Button("Esporta CSV…", ExportCsv, 110),
            Ui.Button("Chiudi", Close)), 1, 0);

        var root = Ui.Grid(SizeType.Percent);
        root.AutoSize = false;
        root.Padding = new Padding(12);
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(nav, 0, 0);
        root.Controls.Add(detailsGroup, 0, 1);
        root.Controls.Add(clientsGroup, 0, 2);
        root.Controls.Add(bottom, 0, 3);
        Controls.Add(root);

        RefreshData();
    }

    public void ShowWeek(DateOnly anyDay)
    {
        _weekStart = Fmt.WeekStart(anyDay);
        RefreshData();
    }

    public void RefreshData()
    {
        var weekEnd = _weekStart.AddDays(6);
        _entries = _store.EntriesBetween(_weekStart, weekEnd);
        _title.Text = ReportBuilder.Title(_weekStart);

        _details.BeginUpdate();
        _details.Items.Clear();
        _details.Groups.Clear();
        for (int i = 0; i < 7; i++)
        {
            var day = _weekStart.AddDays(i);
            var dayEntries = _entries.Where(e => e.Date == day).ToList();
            bool weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (dayEntries.Count == 0 && weekend) continue;

            var group = new ListViewGroup($"{Fmt.DayLong(day)}  —  {Fmt.Hours(dayEntries.Sum(e => e.Hours))} h")
            {
                CollapsedState = ListViewGroupCollapsedState.Expanded,
            };
            _details.Groups.Add(group);

            if (dayEntries.Count == 0)
            {
                _details.Items.Add(new ListViewItem("nessuna attività", group) { ForeColor = SystemColors.GrayText });
                continue;
            }
            foreach (var e in dayEntries)
            {
                var item = new ListViewItem(e.Client, group);
                item.SubItems.Add(Fmt.Hours(e.Hours));
                item.SubItems.Add(Ui.OneLine(e.Note));
                _details.Items.Add(item);
            }
        }
        _details.EndUpdate();

        decimal total = _entries.Sum(e => e.Hours);
        _byClient.BeginUpdate();
        _byClient.Items.Clear();
        foreach (var (client, hours) in ReportBuilder.ByClient(_entries))
        {
            var item = new ListViewItem(client);
            item.SubItems.Add(Fmt.Hours(hours));
            item.SubItems.Add(total > 0 ? (hours / total).ToString("P0", Fmt.It) : "");
            _byClient.Items.Add(item);
        }
        _byClient.EndUpdate();

        var s = _store.Settings;
        decimal expected = s.DailyTargetHours * s.ReminderDays.Count;
        _total.Text = expected > 0
            ? $"Totale settimana: {Fmt.Hours(total)} h su {Fmt.Hours(expected)} h previste"
            : $"Totale settimana: {Fmt.Hours(total)} h";

        FitLastColumn(_details);
        FitLastColumn(_byClient);
    }

    private void CopyText()
    {
        Clipboard.SetText(ReportBuilder.Text(_entries, _weekStart));
        MessageBox.Show(this, "Report copiato negli appunti.", "Consuntivo Ore", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportCsv()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "File CSV (*.csv)|*.csv",
            FileName = $"consuntivo_{_weekStart:yyyy}_settimana{Fmt.IsoWeek(_weekStart):00}.csv",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, ReportBuilder.Csv(_entries), new UTF8Encoding(true));
        }
        catch (IOException ex)
        {
            Ui.ShowWarning(this, $"Impossibile salvare il file:\n{ex.Message}");
        }
    }

    private void FitLastColumn(ListView list)
    {
        int used = 0;
        for (int i = 0; i < list.Columns.Count - 1; i++) used += list.Columns[i].Width;
        var last = list.Columns[^1];
        last.Width = Math.Max(LogicalToDeviceUnits(60), list.ClientSize.Width - used);
    }

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
