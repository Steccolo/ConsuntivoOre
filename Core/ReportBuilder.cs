using System.Text;

namespace ConsuntivoOre.Core;

public static class ReportBuilder
{
    public static string Title(DateOnly weekStart) =>
        $"Settimana {Fmt.IsoWeek(weekStart)} · {weekStart:dd/MM} – {Fmt.Date(weekStart.AddDays(6))}";

    public static IEnumerable<(string Client, decimal Hours)> ByClient(IEnumerable<TimeEntry> entries) =>
        entries.GroupBy(e => e.Client, StringComparer.CurrentCultureIgnoreCase)
               .Select(g => (g.First().Client, g.Sum(e => e.Hours)))
               .OrderByDescending(x => x.Item2);

    public static string Text(IReadOnlyList<TimeEntry> entries, DateOnly weekStart)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Report ore — {Title(weekStart)}");
        sb.AppendLine();
        foreach (var day in entries.GroupBy(e => e.Date))
        {
            sb.AppendLine($"{Fmt.DayLong(day.Key)} — {Fmt.Hours(day.Sum(e => e.Hours))} h");
            foreach (var e in day)
            {
                string note = string.IsNullOrWhiteSpace(e.Note) ? "" : $": {e.Note.ReplaceLineEndings(" ")}";
                sb.AppendLine($"  • {e.Client} — {Fmt.Hours(e.Hours)} h{note}");
            }
            sb.AppendLine();
        }
        sb.AppendLine("Totale per cliente");
        foreach (var (client, hours) in ByClient(entries))
            sb.AppendLine($"  • {client}: {Fmt.Hours(hours)} h");
        sb.AppendLine();
        sb.AppendLine($"Totale settimana: {Fmt.Hours(entries.Sum(e => e.Hours))} h");
        return sb.ToString();
    }

    /// <summary>CSV con separatore ';' e virgola decimale, apribile direttamente in Excel italiano.</summary>
    public static string Csv(IEnumerable<TimeEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Data;Giorno;Cliente;Ore;Nota");
        foreach (var e in entries)
            sb.AppendLine(string.Join(';', Fmt.Date(e.Date), Fmt.DayName(e.Date.DayOfWeek),
                Quote(e.Client), Fmt.Hours(e.Hours), Quote(e.Note)));
        return sb.ToString();
    }

    private static string Quote(string value) =>
        value.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
