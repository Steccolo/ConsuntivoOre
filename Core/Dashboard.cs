namespace ConsuntivoOre.Core;

/// <summary>Una riga della ripartizione: chiave (progetto o issue), descrizione, ore e quota sul totale.</summary>
public sealed record BreakdownRow(string Key, string Label, decimal Hours, decimal Share);

/// <summary>Numeri della dashboard: ore del giorno e della settimana, ripartizione per progetto o per issue Jira.</summary>
public static class Dashboard
{
    public const string WithoutJira = "Senza Jira";

    public static decimal WeekTarget(AppSettings s) => s.DailyTargetHours * s.WorkDays.Count;

    /// <summary>Per progetto Jira; le ore non collegate a Jira stanno in fondo in "Senza Jira".</summary>
    public static IReadOnlyList<BreakdownRow> ByProject(IEnumerable<TimeEntry> entries) =>
        Breakdown(entries, e => e.Jira?.ProjectKey,
            g => g.Select(e => e.Jira!.ProjectName).LastOrDefault(n => n.Length > 0) ?? g.Key!);

    /// <summary>Per issue Jira (chiave e titolo); le ore non collegate stanno in fondo in "Senza Jira".</summary>
    public static IReadOnlyList<BreakdownRow> ByIssue(IEnumerable<TimeEntry> entries) =>
        Breakdown(entries, e => e.Jira?.Key,
            g => g.Select(e => e.Jira!.Summary).LastOrDefault(n => n.Length > 0) ?? "");

    private static IReadOnlyList<BreakdownRow> Breakdown(IEnumerable<TimeEntry> entries,
        Func<TimeEntry, string?> key, Func<IGrouping<string?, TimeEntry>, string> label)
    {
        var list = entries.Where(e => e.Hours > 0).OrderBy(e => e.Date).ThenBy(e => e.CreatedAt).ToList();
        decimal total = list.Sum(e => e.Hours);
        if (total == 0) return [];

        var groups = list.GroupBy(key, StringComparer.OrdinalIgnoreCase).ToList();
        var rows = groups.Where(g => g.Key is not null)
            .Select(g => new BreakdownRow(g.Key!, label(g), g.Sum(e => e.Hours), g.Sum(e => e.Hours) / total))
            .OrderByDescending(r => r.Hours).ThenBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (groups.FirstOrDefault(g => g.Key is null) is { } none)
            rows.Add(new BreakdownRow("", WithoutJira, none.Sum(e => e.Hours), none.Sum(e => e.Hours) / total));
        return rows;
    }
}
