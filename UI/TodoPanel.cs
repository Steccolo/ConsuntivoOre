namespace ConsuntivoOre.UI;

/// <summary>Scheda dei todo: elenco dei task, con consuntivazione diretta (di default sul giorno corrente).</summary>
internal sealed class TodoPanel : UserControl
{
    private readonly DataStore _store;

    private readonly TextBox _name = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly NumericUpDown _hours = new()
    {
        DecimalPlaces = 2, Increment = 0.5m, Minimum = 0, Maximum = 999, Width = 90,
        TextAlign = HorizontalAlignment.Right,
    };
    private readonly TextBox _description = new()
    {
        Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical,
        Anchor = AnchorStyles.Left | AnchorStyles.Right, Height = 60,
    };
    private readonly DateTimePicker _due = new()
    {
        Format = DateTimePickerFormat.Long, Width = 240, ShowCheckBox = true, Checked = false,
    };
    private readonly ListView _list = new()
    {
        View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false,
        Dock = DockStyle.Fill,
    };
    private readonly Label _summary = new() { AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(3, 8, 3, 3) };
    private readonly Button _log;
    private readonly Button _edit;
    private readonly Button _complete;
    private readonly Button _delete;
    private readonly Button _cancelEdit;

    private Guid? _editingId;

    public TodoPanel(DataStore store)
    {
        _store = store;
        Dock = DockStyle.Fill;

        SaveButton = Ui.Button("Aggiungi", Save, 110);
        _cancelEdit = Ui.Button("Annulla modifica", ResetForm, 110);
        _cancelEdit.Visible = false;
        _log = Ui.Button("Consuntiva…", LogSelected, 120);
        _edit = Ui.Button("Modifica", EditSelected);
        _complete = Ui.Button("Completato", ToggleCompleted, 100);
        _delete = Ui.Button("Elimina", DeleteSelected);
        _log.Font = new Font(Font, FontStyle.Bold);

        // --- form todo
        var form = Ui.Grid(SizeType.AutoSize, SizeType.Percent);
        form.Controls.Add(Ui.Label("Nome"), 0, 0);
        form.Controls.Add(_name, 1, 0);
        form.Controls.Add(Ui.Label("Ore previste"), 0, 1);
        form.Controls.Add(Ui.Flow(_hours, new Label
        {
            Text = "facoltative: lascia 0 se non sai quanto ci vorrà",
            AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(8, 7, 3, 3),
        }), 1, 1);
        var descriptionLabel = Ui.Label("Descrizione");
        descriptionLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        form.Controls.Add(descriptionLabel, 0, 2);
        form.Controls.Add(_description, 1, 2);
        form.Controls.Add(Ui.Label("Entro il"), 0, 3);
        form.Controls.Add(Ui.Flow(_due, new Label
        {
            Text = "facoltativa: spunta la casella per impostarla",
            AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(8, 7, 3, 3),
        }), 1, 3);
        _due.Margin = new Padding(3, 5, 3, 3);
        var formButtons = Ui.Flow(SaveButton, _cancelEdit);
        formButtons.FlowDirection = FlowDirection.RightToLeft;
        formButtons.Anchor = AnchorStyles.Right;
        form.Controls.Add(formButtons, 1, 4);

        // --- elenco
        _list.Columns.Add("Nome", LogicalToDeviceUnits(170));
        _list.Columns.Add("Previste", LogicalToDeviceUnits(70), HorizontalAlignment.Right);
        _list.Columns.Add("Consuntivato", LogicalToDeviceUnits(95), HorizontalAlignment.Right);
        _list.Columns.Add("Entro il", LogicalToDeviceUnits(90));
        _list.Columns.Add("Descrizione", LogicalToDeviceUnits(200));
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
        var listBottom = Ui.Grid(SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize, SizeType.Percent);
        listBottom.Controls.Add(_log, 0, 0);
        listBottom.Controls.Add(_edit, 1, 0);
        listBottom.Controls.Add(_complete, 2, 0);
        listBottom.Controls.Add(_delete, 3, 0);
        listBottom.Controls.Add(_summary, 4, 0);
        listPanel.Controls.Add(listBottom, 0, 1);
        var listGroup = Ui.Group("Da fare", listPanel);
        listGroup.AutoSize = false;

        var root = Ui.Grid(SizeType.Percent);
        root.AutoSize = false;
        root.Padding = new Padding(12);
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(Ui.Group("Nuovo todo", form), 0, 0);
        root.Controls.Add(listGroup, 0, 1);
        Controls.Add(root);

        RefreshList();
    }

    /// <summary>Pulsante predefinito (Invio) quando la scheda è attiva.</summary>
    public Button SaveButton { get; }

    public bool HasUnsavedInput =>
        _editingId is not null || _name.TextLength > 0 || _hours.Value != 0 || _description.TextLength > 0;

    public bool IsEditing => _editingId is not null;

    public void FocusInput() => _name.Focus();

    public void RefreshList()
    {
        var selected = SelectedId();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todos = _store.Todos();

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var todo in todos)
        {
            decimal done = _store.HoursForTodo(todo.Id);
            var item = new ListViewItem(todo.Name) { Tag = todo.Id, UseItemStyleForSubItems = false };
            item.SubItems.Add(todo.PlannedHours > 0 ? Fmt.Hours(todo.PlannedHours) : "");
            var doneCell = item.SubItems.Add(done > 0 ? $"✓ {Fmt.Hours(done)} h" : "");
            var dueCell = item.SubItems.Add(todo.DueDate is { } d ? Fmt.Date(d) : "");
            item.SubItems.Add(Ui.OneLine(todo.Description));

            if (todo.IsDone)
            {
                // I completati restano in elenco, in verde.
                foreach (ListViewItem.ListViewSubItem cell in item.SubItems) cell.ForeColor = Ui.Good;
            }
            else
            {
                if (todo.PlannedHours > 0 && done > todo.PlannedHours) doneCell.ForeColor = Ui.Warn;
                if (todo.DueDate is { } due && due <= today)
                {
                    dueCell.ForeColor = Ui.Warn;
                    dueCell.Font = new Font(_list.Font, FontStyle.Bold);
                }
            }
            item.Selected = todo.Id == selected;
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        FitLastColumn();

        int open = todos.Count(t => !t.IsDone);
        int completed = todos.Count - open;
        int overdue = todos.Count(t => !t.IsDone && t.DueDate is { } d && d < today);
        _summary.Text = todos.Count == 0 ? "Nessun todo"
            : string.Join(", ", new[]
              {
                  $"{open} da fare",
                  overdue > 0 ? $"{overdue} scaduti" : null,
                  completed > 0 ? $"{completed} completati" : null,
              }.Where(p => p is not null));
        _summary.ForeColor = overdue > 0 ? Ui.Warn : SystemColors.ControlText;

        UpdateButtons();
    }

    public void Save()
    {
        string name = _name.Text.Trim();
        if (name.Length == 0)
        {
            Ui.ShowWarning(this, "Indica il nome del todo.");
            _name.Focus();
            return;
        }
        string description = _description.Text.Trim();
        if (description.Length == 0)
        {
            Ui.ShowWarning(this, "Indica la descrizione del todo.");
            _description.Focus();
            return;
        }
        decimal hours = _hours.Value; // facoltative: 0 = stima non indicata
        DateOnly? due = _due.Checked ? DateOnly.FromDateTime(_due.Value) : null;

        try
        {
            if (_editingId is { } id)
                _store.UpdateTodo(id, name, hours, description, due);
            else
                _store.AddTodo(new TodoItem { Name = name, PlannedHours = hours, Description = description, DueDate = due });
        }
        catch (Exception ex) when (DataStore.IsFileError(ex))
        {
            ShowSaveError(ex);
            return;
        }

        ResetForm();
        _name.Focus();
    }

    public void ResetForm()
    {
        _editingId = null;
        _name.Clear();
        _hours.Value = 0;
        _description.Clear();
        _due.Value = DateTime.Today;
        _due.Checked = false;
        SaveButton.Text = "Aggiungi";
        _cancelEdit.Visible = false;
    }

    private void EditSelected()
    {
        if (SelectedId() is not { } id || _store.FindTodo(id) is not { } todo) return;
        _editingId = id;
        _name.Text = todo.Name;
        _hours.Value = Math.Min(todo.PlannedHours, _hours.Maximum);
        _description.Text = todo.Description;
        _due.Value = (todo.DueDate ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
        _due.Checked = todo.DueDate is not null;
        SaveButton.Text = "Salva modifiche";
        _cancelEdit.Visible = true;
        _name.Focus();
    }

    /// <summary>Registra le ore del todo nel giorno scelto (di default oggi, non la data di scadenza).</summary>
    private void LogSelected()
    {
        if (SelectedId() is not { } id || _store.FindTodo(id) is not { } todo) return;
        using var dialog = new LogTodoDialog(todo, _store.HoursForTodo(id));
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            _store.Add(new TimeEntry
            {
                Date = dialog.Day,
                Hours = dialog.Hours,
                Client = todo.Name,
                Note = todo.Description,
                TodoId = todo.Id,
            });
        }
        catch (Exception ex) when (DataStore.IsFileError(ex))
        {
            ShowSaveError(ex);
            return;
        }

        // Consuntivato = fatto: resta in elenco, in verde. Si può sempre riaprire o consuntivare ancora.
        if (!todo.IsDone) SetCompleted(id, true);
    }

    private void ToggleCompleted()
    {
        if (SelectedId() is { } id && _store.FindTodo(id) is { } todo) SetCompleted(id, !todo.IsDone);
    }

    private void SetCompleted(Guid id, bool completed)
    {
        try
        {
            if (completed) _store.CompleteTodo(id);
            else _store.ReopenTodo(id);
        }
        catch (Exception ex) when (DataStore.IsFileError(ex))
        {
            ShowSaveError(ex);
            return;
        }
        if (_editingId == id) ResetForm();
    }

    private void DeleteSelected()
    {
        if (SelectedId() is not { } id) return;
        var answer = MessageBox.Show(this,
            "Eliminare il todo selezionato?\nLe ore già consuntivate restano registrate.", "Consuntivo Ore",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;
        try
        {
            _store.DeleteTodo(id);
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

    private Guid? SelectedId() =>
        _list.SelectedItems.Count > 0 ? (Guid)_list.SelectedItems[0].Tag! : null;

    private void UpdateButtons()
    {
        _log.Enabled = _edit.Enabled = _complete.Enabled = _delete.Enabled = _list.SelectedItems.Count > 0;
        bool done = SelectedId() is { } id && _store.FindTodo(id) is { IsDone: true };
        _complete.Text = done ? "Riapri" : "Completato";
    }

    private void FitLastColumn()
    {
        int used = 0;
        for (int i = 0; i < _list.Columns.Count - 1; i++) used += _list.Columns[i].Width;
        _list.Columns[^1].Width = Math.Max(LogicalToDeviceUnits(120), _list.ClientSize.Width - used);
    }
}

/// <summary>Conferma delle ore da registrare per un todo e del giorno (di default oggi).</summary>
internal sealed class LogTodoDialog : BaseForm
{
    private readonly NumericUpDown _hours = new()
    {
        DecimalPlaces = 2, Increment = 0.5m, Minimum = 0, Maximum = 24, Width = 90,
        TextAlign = HorizontalAlignment.Right,
    };
    private readonly DateTimePicker _day = new() { Format = DateTimePickerFormat.Long, Width = 240, Value = DateTime.Today };

    public LogTodoDialog(TodoItem todo, decimal alreadyLogged)
    {
        Text = "Consuntiva";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        // Proposte: le ore previste ancora da fare (tutte, se sono già state superate); senza stima, nessuna.
        decimal remaining = todo.PlannedHours - alreadyLogged;
        _hours.Value = Math.Clamp(remaining > 0 ? remaining : todo.PlannedHours, 0, 24);
        bool estimated = todo.PlannedHours > 0;

        var grid = Ui.Grid(SizeType.AutoSize, SizeType.Percent);
        grid.Padding = new Padding(12);
        grid.Dock = DockStyle.None;
        grid.Controls.Add(Ui.Label("Giorno"), 0, 0);
        var todayButton = Ui.Button("Oggi", () => _day.Value = DateTime.Today, 70);
        _day.Margin = new Padding(3, 5, 3, 3);
        grid.Controls.Add(Ui.Flow(_day, todayButton), 1, 0);
        grid.Controls.Add(Ui.Label("Cliente"), 0, 1);
        grid.Controls.Add(Ui.Label(todo.Name), 1, 1);
        if (todo.Description.Length > 0)
        {
            var note = Ui.Label(todo.Description);
            note.MaximumSize = new Size(LogicalToDeviceUnits(360), 0);
            grid.Controls.Add(Ui.Label("Nota"), 0, 2);
            grid.Controls.Add(note, 1, 2);
        }
        grid.Controls.Add(Ui.Label("Ore"), 0, 3);
        string hint = (alreadyLogged > 0, estimated) switch
        {
            (true, true) => $"già fatte {Fmt.Hours(alreadyLogged)} h su {Fmt.Hours(todo.PlannedHours)} h previste",
            (true, false) => $"già fatte {Fmt.Hours(alreadyLogged)} h",
            (false, true) => $"previste {Fmt.Hours(todo.PlannedHours)} h",
            _ => "nessuna stima indicata",
        };
        grid.Controls.Add(Ui.Flow(_hours, new Label
        {
            Text = hint, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(8, 7, 3, 3),
        }), 1, 3);

        var ok = Ui.Button("Registra", Accept, 100);
        var cancel = Ui.Button("Annulla", () => DialogResult = DialogResult.Cancel);
        var buttons = Ui.Flow(cancel, ok);
        buttons.FlowDirection = FlowDirection.RightToLeft;
        buttons.Anchor = AnchorStyles.Right;
        buttons.Margin = new Padding(0, 10, 0, 0);
        grid.Controls.Add(buttons, 1, 5);
        Controls.Add(grid);

        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) =>
        {
            _hours.Focus();
            _hours.Select(0, _hours.Text.Length);
        };
    }

    public decimal Hours => _hours.Value;

    public DateOnly Day => DateOnly.FromDateTime(_day.Value);

    private void Accept()
    {
        if (_hours.Value <= 0)
        {
            Ui.ShowWarning(this, "Indica quante ore hai lavorato.");
            _hours.Focus();
            return;
        }
        DialogResult = DialogResult.OK;
    }
}
