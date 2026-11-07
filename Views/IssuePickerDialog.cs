using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace ConsuntivoOre.Views;

/// <summary>
/// Ricerca di una issue tra i progetti Jira in cui l'utente può registrare lavoro.
/// Si cerca per chiave (ABC-123), parole del titolo o chiave del progetto; senza testo mostra le issue recenti.
/// </summary>
public sealed class IssuePickerDialog : Window
{
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<JiraIssueRef>>> _search;
    private readonly TextBox _query;
    private readonly ListBox _list;
    private readonly TextBlock _status;
    private CancellationTokenSource? _pending;

    public IssuePickerDialog() : this((_, _) => Task.FromResult<IReadOnlyList<JiraIssueRef>>([])) { }

    public IssuePickerDialog(Func<string, CancellationToken, Task<IReadOnlyList<JiraIssueRef>>> search)
    {
        _search = search;
        Title = "Scegli la issue di Jira";
        Icon = AppAssets.Icon;
        Width = 680;
        Height = 480;
        MinWidth = 480;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(243, 244, 246));

        _query = new TextBox { PlaceholderText = "Chiave (ABC-123), parole del titolo o chiave del progetto" };
        _status = new TextBlock { Classes = { "hint" }, TextWrapping = TextWrapping.Wrap };
        _list = new ListBox
        {
            Background = Brushes.White,
            ItemTemplate = new FuncDataTemplate<JiraIssueRef>((issue, _) => issue is null ? new TextBlock() : new StackPanel
            {
                Margin = new Thickness(0, 2),
                Children =
                {
                    new TextBlock
                    {
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Inlines = new Avalonia.Controls.Documents.InlineCollection
                        {
                            new Avalonia.Controls.Documents.Run(issue.Key) { FontWeight = FontWeight.SemiBold },
                            new Avalonia.Controls.Documents.Run("  " + issue.Summary),
                        },
                    },
                    new TextBlock { Text = issue.ProjectName, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)) },
                },
            }),
        };

        _query.TextChanged += (_, _) => _ = SearchAsync(delay: true);
        _query.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Down && _list.ItemCount > 0)
            {
                _list.SelectedIndex = Math.Max(0, _list.SelectedIndex);
                (_list.ContainerFromIndex(_list.SelectedIndex) as Control)?.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                Choose(_list.SelectedItem as JiraIssueRef ?? (_list.ItemCount == 1 ? _list.Items[0] as JiraIssueRef : null));
                e.Handled = true;
            }
        };
        _list.DoubleTapped += (_, _) => Choose(_list.SelectedItem as JiraIssueRef);
        _list.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Choose(_list.SelectedItem as JiraIssueRef);
            e.Handled = true;
        };

        var ok = new Button { Content = "Scegli", Classes = { "accent" }, MinWidth = 100 };
        ok.Click += (_, _) => Choose(_list.SelectedItem as JiraIssueRef);
        var cancel = new Button { Content = "Annulla", IsCancel = true };
        cancel.Click += (_, _) => Close(null);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel, ok },
        };
        var card = new Border { Classes = { "card" }, Child = _list, Padding = new Thickness(4) };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(16), RowSpacing = 8 };
        Grid.SetRow(_status, 1);
        Grid.SetRow(card, 2);
        Grid.SetRow(buttons, 3);
        root.Children.AddRange([_query, _status, card, buttons]);
        Content = root;

        Opened += (_, _) =>
        {
            _query.Focus();
            _ = SearchAsync(delay: false);
        };
        Closed += (_, _) => _pending?.Cancel();
    }

    private void Choose(JiraIssueRef? issue)
    {
        if (issue is null)
        {
            _status.Text = "Seleziona una issue dall'elenco.";
            return;
        }
        Close(issue);
    }

    private async Task SearchAsync(bool delay)
    {
        _pending?.Cancel();
        var cts = _pending = new CancellationTokenSource();
        string query = _query.Text ?? "";
        try
        {
            // Aspetta che si smetta di scrivere prima di interrogare Jira.
            if (delay) await Task.Delay(400, cts.Token);
            _status.Text = "Ricerca in corso…";
            var results = await _search(query, cts.Token);
            if (cts.IsCancellationRequested) return;

            _list.ItemsSource = results;
            if (results.Count > 0) _list.SelectedIndex = 0;
            _status.Text = results.Count == 0
                ? "Nessuna issue trovata nei progetti in cui puoi registrare ore."
                : query.Trim().Length == 0
                    ? "Issue assegnate a te o su cui hai lavorato di recente. Scrivi per cercare."
                    : $"{results.Count} issue trovate (Invio o doppio clic per scegliere).";
        }
        catch (OperationCanceledException)
        {
        }
        catch (JiraException ex)
        {
            if (cts.IsCancellationRequested) return;
            _list.ItemsSource = null;
            _status.Text = ex.Message;
        }
    }
}
