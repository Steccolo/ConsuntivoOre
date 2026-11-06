using Avalonia.Controls;
using Avalonia.Input;

namespace TodoAvalonia;

public partial class MainWindow : Window
{
    // Richiesto dal caricatore XAML in fase di progettazione.
    public MainWindow() : this(new MainViewModel(Program.OpenStore())) { }

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

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
            if (e.Key != Key.Escape) return;
            if (Tabs.SelectedIndex == 0 && todo.IsEditing) todo.ResetFormCommand.Execute(null);
            else if (Tabs.SelectedIndex == 1 && entry.IsEditing) entry.ResetFormCommand.Execute(null);
        };
        // Ctrl+Invio salva anche mentre si scrive la nota (dove Invio va a capo).
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            if (Tabs.SelectedIndex == 0) todo.SaveCommand.Execute(null);
            else entry.SaveCommand.Execute(null);
            e.Handled = true;
        };
    }

    public void ShowTab(int index) => Tabs.SelectedIndex = index;
}
