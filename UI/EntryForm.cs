namespace ConsuntivoOre.UI;

public enum MainTab { Todo, Entry }

/// <summary>Finestra principale: scheda dei todo e scheda di consuntivazione (ore, cliente e nota per il giorno scelto).</summary>
internal sealed class EntryForm : BaseForm
{
    private readonly DataStore _store;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly TabPage _todoTab = new("Todo");
    private readonly TabPage _entryTab = new("Consuntiva ore");
    private readonly TodoPanel _todo;

    private readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Long, Width = 240 };
    private readonly NumericUpDown _hours = new()
    {
        DecimalPlaces = 2, Increment = 0.5m, Minimum = 0, Maximum = 24, Width = 90,
        TextAlign = HorizontalAlignment.Right,
    };
    private readonly ComboBox _client = new()
    {
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
        AutoCompleteMode = AutoCompleteMode.SuggestAppend,
        AutoCompleteSource = AutoCompleteSource.ListItems,
    };
    private readonly TextBox _note = new()
    {
        Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical,
        Anchor = AnchorStyles.Left | AnchorStyles.Right, Height = 80,
    };
    private readonly ListView _list = new()
    {
        View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false,
        Dock = DockStyle.Fill, GridLines = false,
    };
    private readonly Label _total = new() { AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(3, 8, 3, 3) };
    private readonly Button _save;
    private readonly Button _cancelEdit;
    private readonly Button _edit;
    private readonly Button _delete;

    private Guid? _editingId;
    private DateOnly _lastSelectedOn = DateOnly.FromDateTime(DateTime.Today);

    public EntryForm(DataStore store, DateOnly day, MainTab tab)
    {
        _store = store;
        Text = "Consuntivo Ore";
        ClientSize = new Size(700, 600);
        MinimumSize = new Size(600, 540);
        KeyPreview = true;

        _save = Ui.Button("Aggiungi", Save, 110);
        _cancelEdit = Ui.Button("Annulla modifica", ResetForm, 110);
        _cancelEdit.Visible = false;
        var skipToday = Ui.Button("Non ricordarmelo oggi", () => _store.SkipReminder(DateOnly.FromDateTime(DateTime.Today)), 160);
        _edit = Ui.Button("Modifica", EditSelected);
        _delete = Ui.Button("Elimina", DeleteSelected);
        _total.Font = new Font(Font, FontStyle.Bold);

        // --- barra data
        var nav = Ui.Flow(
            Ui.Button("◀", () => _date.Value = _date.Value.AddDays(-1), 36),
            _date,
            Ui.Button("▶", () => _date.Value = _date.Value.AddDays(1), 36),
            Ui.Button("Oggi", () => _date.Value = DateTime.Today, 70));
        _date.Margin = new Padding(3, 5, 3, 3);
        _date.Value = day.ToDateTime(TimeOnly.MinValue);
        _date.ValueChanged += (_, _) =>
        {
            _lastSelectedOn = DateOnly.FromDateTime(DateTime.Today);
            ResetForm();
            RefreshList();
        };

        // --- form attività
        var form = Ui.Grid(SizeType.AutoSize, SizeType.Percent);
        form.Controls.Add(Ui.Label("Ore"), 0, 0);
        form.Controls.Add(Ui.Flow(_hours, new Label { Text = "es. 1,5 = un'ora e mezza", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(8, 7, 3, 3) }), 1, 0);
        form.Controls.Add(Ui.Label("Cliente"), 0, 1);
        form.Controls.Add(_client, 1, 1);
        var noteLabel = Ui.Label("Nota");
        noteLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        form.Controls.Add(noteLabel, 0, 2);
        form.Controls.Add(_note, 1, 2);
        var formButtons = Ui.Flow(_save, _cancelEdit, skipToday);
        formButtons.FlowDirection = FlowDirection.RightToLeft;
        formButtons.Anchor = AnchorStyles.Right;
        form.Controls.Add(formButtons, 1, 3);

        // --- elenco del giorno
        _list.Columns.Add("Ore", LogicalToDeviceUnits(60), HorizontalAlignment.Right);
        _list.Columns.Add("Cliente", LogicalToDeviceUnits(150));
        _list.Columns.Add("Nota", LogicalToDeviceUnits(300));
        _list.Resize += (_, _) => FitLastColumn();
        _list.DoubleClick += (_, _) => EditSelected();
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) DeleteSelected();
        };

        var listPanel = Ui.Grid(SizeType.Percent);
        listPanel.AutoSize = false;
        listPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        listPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listPanel.Controls.Add(_list, 0, 0);
        var listBottom = Ui.Grid(SizeType.AutoSize, SizeType.AutoSize, SizeType.Percent);
        listBottom.Controls.Add(_edit, 0, 0);
        listBottom.Controls.Add(_delete, 1, 0);
        listBottom.Controls.Add(_total, 2, 0);
        listPanel.Controls.Add(listBottom, 0, 1);
        var listGroup = Ui.Group("Ore registrate nel giorno", listPanel);
        listGroup.AutoSize = false;

        // --- layout principale
        var root = Ui.Grid(SizeType.Percent);
        root.AutoSize = false;
        root.Padding = new Padding(12);
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(nav, 0, 0);
        root.Controls.Add(Ui.Group("Attività", form), 0, 1);
        root.Controls.Add(listGroup, 0, 2);
        _entryTab.Controls.Add(root);

        _todo = new TodoPanel(store);
        _todoTab.Controls.Add(_todo);
        _tabs.TabPages.AddRange([_todoTab, _entryTab]);
        _tabs.SelectedIndexChanged += (_, _) => OnTabChanged();
        Controls.Add(_tabs);

        RefreshList();
        ShowTab(tab);
        Shown += (_, _) => FocusActiveTab();
    }

    private bool TodoActive => _tabs.SelectedTab == _todoTab;

    public void ShowTab(MainTab tab)
    {
        _tabs.SelectedTab = tab == MainTab.Todo ? _todoTab : _entryTab;
        OnTabChanged();
    }

    public void RefreshTodos() => _todo.RefreshList();

    private void OnTabChanged()
    {
        AcceptButton = TodoActive ? _todo.SaveButton : _save;
        if (Visible) FocusActiveTab();
    }

    private void FocusActiveTab()
    {
        if (TodoActive) _todo.FocusInput();
        else FocusHours();
    }

    private DateOnly SelectedDay => DateOnly.FromDateTime(_date.Value);

    /// <summary>Ci sono dati della consuntivazione che andrebbero persi azzerando il form.</summary>
    public bool HasUnsavedEntry => _editingId is not null || _hours.Value != 0 || _note.TextLength > 0;

    /// <summary>Si sta scrivendo in una delle due schede.</summary>
    public bool HasUnsavedInput => HasUnsavedEntry || _todo.HasUnsavedInput;

    public void SetDay(DateOnly day) => _date.Value = day.ToDateTime(TimeOnly.MinValue);

    /// <summary>Una finestra rimasta aperta da un giorno precedente torna a oggi.</summary>
    public void MoveToTodayIfStale()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (_lastSelectedOn == today) return;
        _lastSelectedOn = today;
        SetDay(today);
    }

    public void RefreshList()
    {
        var day = SelectedDay;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var entry in _store.EntriesBetween(day, day))
        {
            var item = new ListViewItem(Fmt.Hours(entry.Hours)) { Tag = entry.Id };
            item.SubItems.Add(entry.Client);
            item.SubItems.Add(Ui.OneLine(entry.Note));
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        FitLastColumn();

        decimal total = _store.HoursOn(day);
        decimal target = _store.Settings.DailyTargetHours;
        _total.Text = target > 0
            ? $"Totale: {Fmt.Hours(total)} h su {Fmt.Hours(target)} h"
            : $"Totale: {Fmt.Hours(total)} h";
        _total.ForeColor = total == 0 ? SystemColors.ControlText
            : target > 0 && total < target ? Ui.Warn : Ui.Good;

        string typed = _client.Text;
        _client.Items.Clear();
        _client.Items.AddRange([.. _store.Clients()]);
        _client.Text = typed;

        UpdateButtons();
    }

    private void Save()
    {
        decimal hours = _hours.Value; // il getter convalida anche il testo digitato
        if (hours <= 0)
        {
            Ui.ShowWarning(this, "Indica quante ore hai lavorato.");
            FocusHours();
            return;
        }
        string client = _client.Text.Trim();
        if (client.Length == 0)
        {
            Ui.ShowWarning(this, "Indica il cliente.");
            _client.Focus();
            return;
        }
        string note = _note.Text.Trim();

        try
        {
            // Il salvataggio aggiorna anche l'elenco, tramite DataStore.EntriesChanged.
            if (_editingId is { } id)
                _store.Update(id, hours, client, note);
            else
                _store.Add(new TimeEntry { Date = SelectedDay, Hours = hours, Client = client, Note = note });
        }
        catch (Exception ex) when (DataStore.IsFileError(ex))
        {
            ShowSaveError(ex);
            return;
        }

        ResetForm();
        FocusHours();
    }

    private void EditSelected()
    {
        if (SelectedId() is not { } id || _store.Find(id) is not { } entry) return;
        _editingId = id;
        _hours.Value = entry.Hours;
        _client.Text = entry.Client;
        _note.Text = entry.Note;
        _save.Text = "Salva modifiche";
        _cancelEdit.Visible = true;
        FocusHours();
    }

    private void DeleteSelected()
    {
        if (SelectedId() is not { } id) return;
        var answer = MessageBox.Show(this, "Eliminare la voce selezionata?", "Consuntivo Ore",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;
        try
        {
            _store.Delete(id);
        }
        catch (Exception ex) when (DataStore.IsFileError(ex))
        {
            ShowSaveError(ex);
            return;
        }
        if (_editingId == id) ResetForm();
    }

    private void ShowSaveError(Exception ex)
    {
        Log.Error(ex);
        Ui.ShowWarning(this, $"Impossibile salvare: la modifica non è stata registrata.\n{ex.Message}");
    }

    private void ResetForm()
    {
        if (_editingId is not null) _client.Text = "";
        _editingId = null;
        _hours.Value = 0;
        _note.Clear();
        _save.Text = "Aggiungi";
        _cancelEdit.Visible = false;
    }

    private Guid? SelectedId() =>
        _list.SelectedItems.Count > 0 ? (Guid)_list.SelectedItems[0].Tag! : null;

    private void UpdateButtons() => _edit.Enabled = _delete.Enabled = _list.SelectedItems.Count > 0;

    private void FocusHours()
    {
        _hours.Focus();
        _hours.Select(0, _hours.Text.Length);
    }

    private void FitLastColumn()
    {
        int used = _list.Columns[0].Width + _list.Columns[1].Width;
        _list.Columns[2].Width = Math.Max(LogicalToDeviceUnits(120), _list.ClientSize.Width - used);
    }

    private bool ConfirmDiscard() =>
        !HasUnsavedInput || MessageBox.Show(this, "Chiudere senza salvare i dati inseriti?", "Consuntivo Ore",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.Enter:
                if (TodoActive) _todo.Save();
                else Save();
                return true;
            case Keys.Escape:
                if (TodoActive)
                {
                    if (_todo.IsEditing) _todo.ResetForm();
                    else if (ConfirmDiscard()) Close();
                }
                else if (_client.DroppedDown) _client.DroppedDown = false;
                else if (_editingId is not null) ResetForm();
                else if (ConfirmDiscard()) Close();
                return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
