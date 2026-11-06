using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ConsuntivoOre.ViewModels;

/// <summary>
/// Riga del dettaglio: intestazione del giorno oppure voce. Ogni cella ha anche il testo da copiare
/// (ore nel formato di Jira, nota completa su più righe).
/// </summary>
public sealed class ReportRow
{
    private static readonly IBrush HeaderBack = new SolidColorBrush(Color.FromRgb(243, 244, 246));
    private static readonly IBrush Text = new SolidColorBrush(Color.FromRgb(31, 41, 55));
    private static readonly IBrush Gray = new SolidColorBrush(Color.FromRgb(107, 114, 128));

    private ReportRow(bool isHeader, string client, string hours, string note, string? copyHours, string? copyNote)
    {
        IsHeader = isHeader;
        Client = client;
        Hours = hours;
        Note = note;
        CopyHours = copyHours;
        CopyNote = copyNote;
    }

    public static ReportRow Header(DateOnly day, decimal hours, bool empty) =>
        new(true, Fmt.DayLong(day), $"{Fmt.Hours(hours)} h", empty ? "nessuna attività" : "", null, null);

    public static ReportRow Item(TimeEntry e) =>
        new(false, e.Client, Fmt.Hours(e.Hours), e.Note.ReplaceLineEndings(" ").Trim(), Fmt.Duration(e.Hours), e.Note);

    public bool IsHeader { get; }
    public string Client { get; }
    public string Hours { get; }
    public string Note { get; }
    private string? CopyHours { get; }
    private string? CopyNote { get; }

    public IBrush Background => IsHeader ? HeaderBack : Brushes.Transparent;
    public IBrush Foreground => IsHeader && Note.Length > 0 ? Gray : Text;
    public FontWeight Weight => IsHeader ? FontWeight.Bold : FontWeight.Normal;

    /// <summary>Testo da copiare per la colonna indicata (0 cliente, 1 ore, 2 nota); null sulle intestazioni.</summary>
    public string? CopyText(int column) => IsHeader ? null : column switch
    {
        0 => Client,
        1 => CopyHours,
        _ => CopyNote,
    };
}

public sealed record ClientRow(string Client, string Hours, string Percent);

public sealed partial class ReportViewModel : ObservableObject
{
    public const string CopyHint = "Clicca un campo e premi Ctrl+C (o doppio clic) per copiarlo.";

    private readonly DataStore _store;
    private IReadOnlyList<TimeEntry> _entries = [];

    public ReportViewModel(DataStore store, DateOnly anyDayOfWeek)
    {
        _store = store;
        WeekStart = Fmt.WeekStart(anyDayOfWeek);
    }

    public ObservableCollection<ReportRow> Details { get; } = [];
    public ObservableCollection<ClientRow> ByClient { get; } = [];

    [ObservableProperty] public partial DateOnly WeekStart { get; set; }
    [ObservableProperty] public partial string Title { get; set; } = "";
    [ObservableProperty] public partial string Total { get; set; } = "";
    [ObservableProperty] public partial string Status { get; set; } = CopyHint;

    partial void OnWeekStartChanged(DateOnly value) => Refresh();

    /// <summary>Impostati dalla finestra: appunti e scelta del file.</summary>
    public Func<string, Task<bool>>? CopyToClipboard { get; set; }
    public Func<string, Task<string?>>? AskSavePath { get; set; }
    public Func<string, Task>? ShowWarning { get; set; }

    public void ShowWeek(DateOnly anyDay) => WeekStart = Fmt.WeekStart(anyDay);

    [RelayCommand] private void PreviousWeek() => WeekStart = WeekStart.AddDays(-7);
    [RelayCommand] private void NextWeek() => WeekStart = WeekStart.AddDays(7);
    [RelayCommand] private void ThisWeek() => ShowWeek(DateOnly.FromDateTime(DateTime.Today));

    public void Refresh()
    {
        _entries = _store.EntriesBetween(WeekStart, WeekStart.AddDays(6));
        Title = ReportBuilder.Title(WeekStart);

        Details.Clear();
        for (int i = 0; i < 7; i++)
        {
            var day = WeekStart.AddDays(i);
            var dayEntries = _entries.Where(e => e.Date == day).ToList();
            if (dayEntries.Count == 0 && !_store.Settings.WorkDays.Contains(day.DayOfWeek)) continue;
            Details.Add(ReportRow.Header(day, dayEntries.Sum(e => e.Hours), dayEntries.Count == 0));
            foreach (var e in dayEntries) Details.Add(ReportRow.Item(e));
        }

        decimal total = _entries.Sum(e => e.Hours);
        ByClient.Clear();
        foreach (var (client, hours) in ReportBuilder.ByClient(_entries))
            ByClient.Add(new ClientRow(client, Fmt.Hours(hours), total > 0 ? (hours / total).ToString("P0", Fmt.It) : ""));

        var s = _store.Settings;
        decimal expected = s.DailyTargetHours * s.WorkDays.Count;
        Total = expected > 0
            ? $"Totale settimana: {Fmt.Hours(total)} h su {Fmt.Hours(expected)} h previste"
            : $"Totale settimana: {Fmt.Hours(total)} h";
    }

    /// <summary>Copia un campo del dettaglio negli appunti e lo conferma nella barra sotto la tabella.</summary>
    public async Task CopyCell(ReportRow? row, int column)
    {
        if (row?.CopyText(column) is not { Length: > 0 } text || CopyToClipboard is null) return;
        if (!await CopyToClipboard(text)) return;
        string preview = text.ReplaceLineEndings(" ").Trim();
        if (preview.Length > 60) preview = preview[..57] + "…";
        Status = $"Copiato: {preview}";
        await Task.Delay(3000);
        if (Status.EndsWith(preview, StringComparison.Ordinal)) Status = CopyHint;
    }

    [RelayCommand]
    private async Task CopyText()
    {
        if (CopyToClipboard is not null && await CopyToClipboard(ReportBuilder.Text(_entries, WeekStart)))
            Status = "Report copiato negli appunti.";
    }

    [RelayCommand]
    private async Task ExportCsv()
    {
        if (AskSavePath is null) return;
        string name = $"consuntivo_{Fmt.IsoYear(WeekStart)}_settimana{Fmt.IsoWeek(WeekStart):00}.csv";
        if (await AskSavePath(name) is not { } path) return;
        try
        {
            await File.WriteAllTextAsync(path, ReportBuilder.Csv(_entries), new System.Text.UTF8Encoding(true));
            Status = $"Esportato in {path}";
        }
        catch (Exception ex) when (DataStore.IsFileError(ex))
        {
            Log.Error(ex);
            if (ShowWarning is not null) await ShowWarning($"Impossibile salvare il file:\n{ex.Message}");
        }
    }
}
