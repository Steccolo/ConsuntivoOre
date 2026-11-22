using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ConsuntivoOre.ViewModels;

/// <summary>Riga della ripartizione: chiave (progetto o issue), descrizione, barra, percentuale e ore.</summary>
public sealed class BreakdownItem(BreakdownRow row, decimal total)
{
    public string Key => row.Key.Length > 0 ? row.Key : "—";
    public string Label => row.Label;
    public double Fraction => (double)row.Share;
    public string Percent => Fmt.Percent(row.Hours, total);
    public string Hours => $"{Fmt.Hours(row.Hours)} h";
    public bool IsWithoutJira => row.Key.Length == 0;
}

/// <summary>Scheda iniziale: ore di oggi e della settimana corrente, ripartite per progetto o per issue Jira.</summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly DataStore _store;

    public DashboardViewModel(DataStore store)
    {
        _store = store;
        _store.EntriesChanged += Refresh;
        Refresh();
    }

    public void Detach() => _store.EntriesChanged -= Refresh;

    public ObservableCollection<BreakdownItem> Rows { get; } = [];

    [ObservableProperty] public partial string Subtitle { get; set; } = "";
    [ObservableProperty] public partial double TodayHours { get; set; }
    [ObservableProperty] public partial double TodayTarget { get; set; }
    [ObservableProperty] public partial string TodayText { get; set; } = "";
    [ObservableProperty] public partial string TodayTargetText { get; set; } = "";
    [ObservableProperty] public partial double WeekHours { get; set; }
    [ObservableProperty] public partial double WeekTarget { get; set; }
    [ObservableProperty] public partial string WeekText { get; set; } = "";
    [ObservableProperty] public partial string WeekTargetText { get; set; } = "";
    [ObservableProperty] public partial bool IsEmpty { get; set; }

    /// <summary>Vero = per progetto (come nel mockup), falso = per issue.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ByIssue), nameof(BreakdownTitle))]
    public partial bool ByProject { get; set; } = true;

    public bool ByIssue => !ByProject;
    public string BreakdownTitle => ByProject ? "Ore per progetto Jira" : "Ore per issue Jira";

    partial void OnByProjectChanged(bool value) => Refresh();

    [RelayCommand] private void ShowProjects() => ByProject = true;
    [RelayCommand] private void ShowIssues() => ByProject = false;

    /// <summary>Anche dopo un cambio delle impostazioni (ore previste, giorni lavorativi).</summary>
    public void Refresh()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monday = Fmt.WeekStart(today);
        var s = _store.Settings;

        decimal day = _store.HoursOn(today);
        TodayHours = (double)day;
        TodayTarget = (double)s.DailyTargetHours;
        TodayText = $"{Fmt.Hours(day)} h";
        TodayTargetText = s.DailyTargetHours > 0 ? $"su {Fmt.Hours(s.DailyTargetHours)} h previste" : "nessun obiettivo";

        var entries = _store.EntriesBetween(monday, monday.AddDays(6));
        decimal week = entries.Sum(e => e.Hours);
        decimal weekTarget = Dashboard.WeekTarget(s);
        WeekHours = (double)week;
        WeekTarget = (double)weekTarget;
        WeekText = $"{Fmt.Hours(week)} h";
        WeekTargetText = weekTarget > 0 ? $"su {Fmt.Hours(weekTarget)} h previste" : "nessun obiettivo";

        Subtitle = $"Settimana corrente · {Fmt.Date(monday)} – {Fmt.Date(monday.AddDays(6))}";

        Rows.Clear();
        var rows = ByProject ? Dashboard.ByProject(entries) : Dashboard.ByIssue(entries);
        foreach (var r in rows) Rows.Add(new BreakdownItem(r, week));
        IsEmpty = Rows.Count == 0;
    }
}
