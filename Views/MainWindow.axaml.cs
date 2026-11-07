using Avalonia.Controls;
using Avalonia.Input;

namespace ConsuntivoOre.Views;

public enum MainTab { Todo, Entry }

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

        var todo = vm.Todo;
        todo.AskLog = (item, logged) => new LogDialog(item, logged).ShowDialog<(DateOnly, decimal)?>(this);
        todo.AskConfirm = message => ConfirmDialog.Show(this, message);
        TodoGrid.DoubleTapped += (_, _) => todo.EditCommand.Execute(null);
        TodoGrid.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete && todo.DeleteCommand.CanExecute(null)) todo.DeleteCommand.Execute(null);
        };

        var entry = vm.Entry;
        entry.AskConfirm = message => ConfirmDialog.Show(this, message);
        entry.AskChoice = (message, buttons) => ChoiceDialog.Show(this, message, buttons);
        if (vm.Jira is { } jira)
            entry.AskIssue = () => new IssuePickerDialog(jira.SearchAsync).ShowDialog<JiraIssueRef?>(this);

        // La colonna Jira si vede solo con il collegamento attivo.
        var jiraColumn = EntryGrid.Columns[2];
        jiraColumn.IsVisible = entry.JiraEnabled;
        entry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EntryViewModel.JiraEnabled)) jiraColumn.IsVisible = entry.JiraEnabled;
        };
        EntryGrid.DoubleTapped += (_, _) =>
        {
            entry.EditCommand.Execute(null);
            HoursBox.Focus();
        };
        EntryGrid.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete && entry.DeleteCommand.CanExecute(null)) entry.DeleteCommand.Execute(null);
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
                // Ctrl+Invio salva anche mentre si scrive la nota (dove Invio va a capo).
                case Key.Enter when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                    if (CurrentTab == MainTab.Todo) todo.SaveCommand.Execute(null);
                    else entry.SaveCommand.Execute(null);
                    break;
                default:
                    return;
            }
            e.Handled = true;
        };
    }

    public MainViewModel ViewModel { get; }

    public MainTab CurrentTab => Tabs.SelectedIndex == 0 ? MainTab.Todo : MainTab.Entry;

    public void ShowTab(MainTab tab) => Tabs.SelectedIndex = tab == MainTab.Todo ? 0 : 1;

    private async Task CloseIfConfirmed()
    {
        if (!ViewModel.HasUnsavedInput || await ConfirmDialog.Show(this, "Chiudere senza salvare i dati inseriti?"))
            Close();
    }
}
