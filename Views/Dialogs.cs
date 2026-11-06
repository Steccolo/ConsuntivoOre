using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
namespace ConsuntivoOre.Views;

/// <summary>Ore e giorno (di default oggi) da registrare per un todo.</summary>
public sealed class LogDialog : Window
{
    public LogDialog() : this(new TodoItem(), 0) { }

    public LogDialog(TodoItem todo, decimal alreadyLogged)
    {
        Title = "Consuntiva";
        Icon = AppAssets.Icon;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        decimal remaining = todo.PlannedHours - alreadyLogged;
        var hours = new NumericUpDown
        {
            Minimum = 0, Maximum = 24, Increment = 0.5m, FormatString = "0.##", Width = 130,
            Value = Math.Clamp(remaining > 0 ? remaining : todo.PlannedHours, 0, 24),
        };
        var day = new CalendarDatePicker { SelectedDate = DateTime.Today, Width = 180 };
        var error = new TextBlock { Classes = { "error" }, IsVisible = false };

        string hint = (alreadyLogged > 0, todo.PlannedHours > 0) switch
        {
            (true, true) => $"già fatte {Fmt.Hours(alreadyLogged)} h su {Fmt.Hours(todo.PlannedHours)} h previste",
            (true, false) => $"già fatte {Fmt.Hours(alreadyLogged)} h",
            (false, true) => $"previste {Fmt.Hours(todo.PlannedHours)} h",
            _ => "nessuna stima indicata",
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            RowSpacing = 10, ColumnSpacing = 12,
        };
        void Row(int row, string label, Control content)
        {
            var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(l, row);
            Grid.SetRow(content, row);
            Grid.SetColumn(content, 1);
            grid.Children.Add(l);
            grid.Children.Add(content);
        }
        Row(0, "Giorno", new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { day, new Button { Content = "Oggi", Command = new Relay(() => day.SelectedDate = DateTime.Today) } },
        });
        Row(1, "Cliente", new TextBlock { Text = todo.Name, FontWeight = FontWeight.SemiBold });
        Row(2, "Nota", new TextBlock { Text = todo.Description, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 });
        Row(3, "Ore", new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10,
            Children = { hours, new TextBlock { Text = hint, Classes = { "hint" } } },
        });

        var ok = new Button { Content = "Registra", Classes = { "accent" }, IsDefault = true, MinWidth = 100 };
        ok.Click += (_, _) =>
        {
            if ((hours.Value ?? 0) <= 0 || day.SelectedDate is null)
            {
                error.Text = day.SelectedDate is null ? "Scegli il giorno." : "Indica quante ore hai lavorato.";
                error.IsVisible = true;
                return;
            }
            Close(((DateOnly, decimal)?)(DateOnly.FromDateTime(day.SelectedDate.Value), hours.Value!.Value));
        };
        var cancel = new Button { Content = "Annulla", IsCancel = true };
        cancel.Click += (_, _) => Close(null);

        Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 14,
            Children =
            {
                grid,
                error,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { cancel, ok },
                },
            },
        };
        Opened += (_, _) => hours.Focus();
    }
}

/// <summary>Domanda Sì/No: Avalonia non ha un MessageBox già pronto.</summary>
public static class ConfirmDialog
{
    public static Task<bool> Show(Window owner, string message)
    {
        var window = new Window
        {
            Title = "Consuntivo Ore",
            Icon = AppAssets.Icon,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var yes = new Button { Content = "Sì", Classes = { "accent" }, MinWidth = 80 };
        var no = new Button { Content = "No", IsDefault = true, IsCancel = true, MinWidth = 80 };
        yes.Click += (_, _) => window.Close(true);
        no.Click += (_, _) => window.Close(false);
        window.Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 380 },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { yes, no },
                },
            },
        };
        return window.ShowDialog<bool>(owner);
    }
}

/// <summary>Avviso con il solo pulsante OK; senza finestra proprietaria compare al centro dello schermo.</summary>
public static class MessageDialog
{
    public static Task Show(Window? owner, string message, bool error = false)
    {
        var window = new Window
        {
            Title = "Consuntivo Ore",
            Icon = AppAssets.Icon,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
        };
        var ok = new Button { Content = "OK", Classes = { "accent" }, IsDefault = true, IsCancel = true, MinWidth = 80 };
        ok.Click += (_, _) => window.Close();
        window.Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 16,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = error ? "✖" : "⚠", FontSize = 22, VerticalAlignment = VerticalAlignment.Top,
                            Foreground = new SolidColorBrush(error ? Color.FromRgb(220, 38, 38) : Color.FromRgb(180, 83, 9)),
                        },
                        new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 },
                    },
                },
                new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Children = { ok } },
            },
        };
        if (owner is not null) return window.ShowDialog(owner);

        var closed = new TaskCompletionSource();
        window.Closed += (_, _) => closed.TrySetResult();
        window.Show();
        return closed.Task;
    }
}

/// <summary>Comando minimo per i pulsanti creati da codice.</summary>
internal sealed class Relay(Action action) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action();
}
