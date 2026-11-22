using Avalonia.Controls;
using Avalonia.Input;

namespace ConsuntivoOre.Views;

/// <summary>Schede nell'ordine in cui compaiono.</summary>
public enum MainTab { Dashboard, Todo, Entry }

public partial class MainWindow : Window
{
    // Richiesto dal caricatore XAML in fase di progettazione.
    public MainWindow() : this(new MainViewModel(new DataStore(Path.GetTempPath()))) { }

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        ViewModel = vm;

        ColumnLayout.Remember(TodoGrid, "todo", vm.Store, this);
        ColumnLayout.Remember(EntryGrid, "ore", vm.Store, this);

        Func<Task<JiraIssueRef?>>? pickIssue = vm.Jira is { } jira
            ? () => new IssuePickerDialog(jira.SearchAsync).ShowDialog<JiraIssueRef?>(this)
            : null;

        var todo = vm.Todo;
        todo.AskLog = (item, logged) => new LogDialog(item, logged).ShowDialog<(DateOnly, decimal)?>(this);
        todo.AskConfirm = message => ConfirmDialog.Show(this, message);
        todo.AskIssue = pickIssue;
        TodoGrid.DoubleTapped += (_, _) => todo.EditCommand.Execute(null);
        TodoGrid.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete && todo.DeleteCommand.CanExecute(null)) todo.DeleteCommand.Execute(null);
        };
        FollowJira(todo, nameof(TodoViewModel.JiraEnabled), () => todo.JiraEnabled, TodoGrid.Columns[4]);

        var entry = vm.Entry;
        entry.AskConfirm = message => ConfirmDialog.Show(this, message);
        entry.AskChoice = (message, buttons) => ChoiceDialog.Show(this, message, buttons);
        entry.AskIssue = pickIssue;
        FollowJira(entry, nameof(EntryViewModel.JiraEnabled), () => entry.JiraEnabled, EntryGrid.Columns[2]);
        EntryGrid.DoubleTapped += (_, _) =>
        {
            entry.EditCommand.Execute(null);
            HoursBox.Focus();
        };
        EntryGrid.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete && entry.DeleteCommand.CanExecute(null)) entry.DeleteCommand.Execute(null);
        };

        // La dashboard si aggiorna anche quando ci si torna (cambio di giorno a finestra aperta).
        Tabs.SelectionChanged += (_, _) =>
        {
            if (CurrentTab == MainTab.Dashboard) vm.Dashboard.Refresh();
        };

        KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Escape when CurrentTab == MainTab.Todo && todo.IsEditing:
                    todo.ResetFormCommand.Execute(null);
                    break;
                case Key.Escape when CurrentTab == MainTab.Entry && entry.IsEditing:
                    entry.ResetFormCommand.Execute(null);
                    break;
                case Key.Escape:
                    _ = CloseIfConfirmed();
                    break;
                // Ctrl+Invio salva anche mentre si scrive la nota o la descrizione (dove Invio va a capo).
                case Key.Enter when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                    if (CurrentTab == MainTab.Todo) todo.SaveCommand.Execute(null);
                    else if (CurrentTab == MainTab.Entry) entry.SaveCommand.Execute(null);
                    break;
                default:
                    return;
            }
            e.Handled = true;
        };
    }

    /// <summary>La colonna Jira si vede solo con il collegamento attivo.</summary>
    private static void FollowJira(System.ComponentModel.INotifyPropertyChanged vm, string property, Func<bool> enabled,
        DataGridColumn column)
    {
        column.IsVisible = enabled();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == property) column.IsVisible = enabled();
        };
    }

    public MainViewModel ViewModel { get; }

    public MainTab CurrentTab => (MainTab)Math.Clamp(Tabs.SelectedIndex, 0, 2);

    public void ShowTab(MainTab tab) => Tabs.SelectedIndex = (int)tab;

    private async Task CloseIfConfirmed()
    {
        if (!ViewModel.HasUnsavedInput || await ConfirmDialog.Show(this, "Chiudere senza salvare i dati inseriti?"))
            Close();
    }
}
