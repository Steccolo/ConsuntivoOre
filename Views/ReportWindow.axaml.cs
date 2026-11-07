using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace ConsuntivoOre.Views;

public partial class ReportWindow : Window
{
    // Richiesto dal caricatore XAML in fase di progettazione.
    public ReportWindow() : this(new ReportViewModel(new DataStore(Path.GetTempPath()), DateOnly.FromDateTime(DateTime.Today))) { }

    public ReportWindow(ReportViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        ViewModel = vm;

        ColumnLayout.Remember(DetailsGrid, "report", vm.Store, this);
        ColumnLayout.Remember(ClientsGrid, "clienti", vm.Store, this);

        vm.CopyToClipboard = CopyToClipboard;
        vm.AskSavePath = AskSavePath;
        vm.ShowWarning = message => MessageDialog.Show(this, message);
        vm.Refresh();

        // La colonna Jira si vede solo con il collegamento attivo.
        var jiraColumn = DetailsGrid.Columns[2];
        jiraColumn.IsVisible = vm.JiraEnabled;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ReportViewModel.JiraEnabled)) jiraColumn.IsVisible = vm.JiraEnabled;
        };

        CloseButton.Click += (_, _) => Close();
        CopyMenu.Click += (_, _) => _ = CopyCurrentCell();
        DetailsGrid.DoubleTapped += (_, _) => _ = CopyCurrentCell();
        // Ctrl+C prima che lo gestisca la tabella (che copierebbe la riga intera).
        DetailsGrid.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key is Key.C or Key.Insert && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                _ = CopyCurrentCell();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
    }

    public ReportViewModel ViewModel { get; }

    private Task CopyCurrentCell() =>
        ViewModel.CopyCell(DetailsGrid.SelectedItem as ReportRow, DetailsGrid.CurrentColumn?.DisplayIndex ?? 0);

    private async Task<bool> CopyToClipboard(string text)
    {
        try
        {
            if (Clipboard is null) return false;
            await Clipboard.SetTextAsync(text);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            await MessageDialog.Show(this, "Gli appunti sono occupati da un'altra applicazione: riprova.");
            return false;
        }
    }

    private async Task<string?> AskSavePath(string suggestedName)
    {
        var documents = await StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents);
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Esporta CSV",
            SuggestedFileName = suggestedName,
            DefaultExtension = "csv",
            SuggestedStartLocation = documents,
            FileTypeChoices = [new FilePickerFileType("File CSV") { Patterns = ["*.csv"] }],
        });
        return file?.TryGetLocalPath();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
