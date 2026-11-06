using System.Runtime.InteropServices;
using System.Text;

namespace ConsuntivoOre.UI;

/// <summary>Report settimanale: dettaglio per giorno, totali per cliente, esportazione.</summary>
internal sealed class ReportForm : BaseForm
{
    private readonly DataStore _store;
    private readonly Label _title = new() { AutoSize = true, Margin = new Padding(10, 7, 10, 3) };
    // Griglia a celle singole: ore, cliente e nota si copiano uno alla volta (es. per Jira).
    private readonly DataGridView _details = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
        MultiSelect = false,
        RowHeadersVisible = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AllowUserToOrderColumns = false,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.None,
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
        StandardTab = true,
    };
    private readonly Label _copied = new()
    {
        AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 4, 3, 0),
        Text = "Clicca un campo e premi Ctrl+C (o doppio clic) per copiarlo.",
    };
    private readonly System.Windows.Forms.Timer _copiedTimer = new() { Interval = 3000 };
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

        SetupDetails();

        _byClient.Columns.Add("Cliente", LogicalToDeviceUnits(250));
        _byClient.Columns.Add("Ore", LogicalToDeviceUnits(80), HorizontalAlignment.Right);
        _byClient.Columns.Add("%", LogicalToDeviceUnits(80), HorizontalAlignment.Right);
        _byClient.Resize += (_, _) => FitLastColumn(_byClient);

        var detailsPanel = Ui.Grid(SizeType.Percent);
        detailsPanel.AutoSize = false;
        detailsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsPanel.Controls.Add(_details, 0, 0);
        detailsPanel.Controls.Add(_copied, 0, 1);
        var detailsGroup = Ui.Group("Dettaglio per giorno", detailsPanel);
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

        _details.SuspendLayout();
        _details.Rows.Clear();
        for (int i = 0; i < 7; i++)
        {
            var day = _weekStart.AddDays(i);
            var dayEntries = _entries.Where(e => e.Date == day).ToList();
            if (dayEntries.Count == 0 && !_store.Settings.WorkDays.Contains(day.DayOfWeek)) continue;

            AddDayHeader(Fmt.DayLong(day), $"{Fmt.Hours(dayEntries.Sum(e => e.Hours))} h",
                dayEntries.Count == 0 ? "nessuna attività" : "");
            foreach (var e in dayEntries)
            {
                int index = _details.Rows.Add(e.Client, Fmt.Hours(e.Hours), Ui.OneLine(e.Note));
                // Nella copia le ore vanno nel formato di Jira e la nota resta su più righe, come è stata scritta.
                _details.Rows[index].Cells[1].Tag = Fmt.Duration(e.Hours);
                _details.Rows[index].Cells[2].Tag = e.Note;
            }
        }
        _details.ClearSelection();
        _details.ResumeLayout();

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
        decimal expected = s.DailyTargetHours * s.WorkDays.Count;
        _total.Text = expected > 0
            ? $"Totale settimana: {Fmt.Hours(total)} h su {Fmt.Hours(expected)} h previste"
            : $"Totale settimana: {Fmt.Hours(total)} h";

        FitLastColumn(_byClient);
    }

    private void SetupDetails()
    {
        _details.Columns.Add("Cliente", "Cliente");
        _details.Columns.Add("Ore", "Ore");
        _details.Columns.Add("Nota", "Nota");
        _details.Columns[0].Width = LogicalToDeviceUnits(190);
        _details.Columns[1].Width = LogicalToDeviceUnits(70);
        _details.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _details.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        foreach (DataGridViewColumn column in _details.Columns)
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
        _details.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
        _details.DefaultCellStyle.Padding = new Padding(2, 3, 2, 3);
        _details.EnableHeadersVisualStyles = true;

        _details.KeyDown += (_, e) =>
        {
            if (e.KeyData is (Keys.Control | Keys.C) or (Keys.Control | Keys.Insert))
            {
                CopyCell(_details.CurrentCell);
                e.SuppressKeyPress = e.Handled = true;
            }
        };
        _details.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0) CopyCell(_details.Rows[e.RowIndex].Cells[e.ColumnIndex]);
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Copia", null, (_, _) => CopyCell(_details.CurrentCell));
        _details.CellMouseDown += (_, e) =>
        {
            // Il clic destro seleziona la cella su cui si apre il menu.
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
                _details.CurrentCell = _details.Rows[e.RowIndex].Cells[e.ColumnIndex];
        };
        _details.CellContextMenuStripNeeded += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0) e.ContextMenuStrip = menu;
        };

        _copiedTimer.Tick += (_, _) =>
        {
            _copiedTimer.Stop();
            _copied.Text = "Clicca un campo e premi Ctrl+C (o doppio clic) per copiarlo.";
        };
        FormClosed += (_, _) => _copiedTimer.Dispose();
    }

    /// <summary>Riga di intestazione del giorno: non è una voce, quindi non si copia.</summary>
    private void AddDayHeader(string day, string hours, string note)
    {
        int index = _details.Rows.Add(day, hours, note);
        var row = _details.Rows[index];
        row.Tag = "giorno";
        var style = row.DefaultCellStyle;
        style.Font = new Font(Font, FontStyle.Bold);
        style.BackColor = style.SelectionBackColor = SystemColors.Control;
        style.ForeColor = style.SelectionForeColor = SystemColors.ControlText;
        row.Cells[2].Style.ForeColor = row.Cells[2].Style.SelectionForeColor = SystemColors.GrayText;
    }

    private void CopyCell(DataGridViewCell? cell)
    {
        if (cell is null || cell.OwningRow?.Tag is "giorno") return;
        string text = cell.Tag as string ?? cell.FormattedValue?.ToString() ?? "";
        if (text.Length == 0) return;
        try
        {
            Clipboard.SetText(text);
        }
        catch (ExternalException ex)
        {
            Log.Error(ex);
            Ui.ShowWarning(this, "Gli appunti sono occupati da un'altra applicazione: riprova.");
            return;
        }
        string preview = Ui.OneLine(text);
        if (preview.Length > 60) preview = preview[..57] + "…";
        _copied.Text = $"Copiato: {preview}";
        _copiedTimer.Stop();
        _copiedTimer.Start();
    }

    private void CopyText()
    {
        try
        {
            Clipboard.SetText(ReportBuilder.Text(_entries, _weekStart));
        }
        catch (ExternalException ex)
        {
            Log.Error(ex);
            Ui.ShowWarning(this, "Gli appunti sono occupati da un'altra applicazione: riprova.");
            return;
        }
        MessageBox.Show(this, "Report copiato negli appunti.", "Consuntivo Ore", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportCsv()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "File CSV (*.csv)|*.csv",
            FileName = $"consuntivo_{Fmt.IsoYear(_weekStart)}_settimana{Fmt.IsoWeek(_weekStart):00}.csv",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, ReportBuilder.Csv(_entries), new UTF8Encoding(true));
        }
        catch (Exception ex) when (DataStore.IsFileError(ex))
        {
            Log.Error(ex);
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
