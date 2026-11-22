using System.Globalization;

namespace ConsuntivoOre.Core;

public static class Fmt
{
    public static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public static string Hours(decimal hours) => hours.ToString("0.##", It);

    /// <summary>Durata nel formato di Jira: 1,5 → "1h 30m", 0,25 → "15m", 2 → "2h".</summary>
    public static string Duration(decimal hours)
    {
        int minutes = (int)Math.Round(hours * 60, MidpointRounding.AwayFromZero);
        if (minutes <= 0) return "0m";
        int h = minutes / 60, m = minutes % 60;
        return h == 0 ? $"{m}m" : m == 0 ? $"{h}h" : $"{h}h {m}m";
    }

    public static string Date(DateOnly day) => day.ToString("dd/MM/yyyy", It);

    public static string DayName(DayOfWeek day) => It.TextInfo.ToTitleCase(It.DateTimeFormat.GetDayName(day));

    public static string DayLong(DateOnly day) => $"{DayName(day.DayOfWeek)} {day.ToString("dd/MM", It)}";

    /// <summary>"Venerdì 20/11/2026".</summary>
    public static string DayFull(DateOnly day) => $"{DayName(day.DayOfWeek)} {Date(day)}";

    public static string Priority(TodoPriority p) => p switch
    {
        TodoPriority.High => "Alta",
        TodoPriority.Low => "Bassa",
        _ => "Media",
    };

    /// <summary>Percentuale con al massimo un decimale: 33,3% · 10%.</summary>
    public static string Percent(decimal part, decimal total) =>
        total > 0 ? (part / total * 100).ToString("0.#", It) + "%" : "";

    public static DateOnly WeekStart(DateOnly day) => day.AddDays(-MondayIndex(day.DayOfWeek));

    /// <summary>Lunedì = 0 ... Domenica = 6.</summary>
    public static int MondayIndex(DayOfWeek day) => ((int)day + 6) % 7;

    public static int IsoWeek(DateOnly day) => ISOWeek.GetWeekOfYear(day.ToDateTime(TimeOnly.MinValue));

    public static int IsoYear(DateOnly day) => ISOWeek.GetYear(day.ToDateTime(TimeOnly.MinValue));

    public static DayOfWeek[] WeekDays { get; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
         DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
}
