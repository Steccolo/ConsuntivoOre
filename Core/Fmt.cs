using System.Globalization;

namespace ConsuntivoOre.Core;

public static class Fmt
{
    public static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public static string Hours(decimal hours) => hours.ToString("0.##", It);

    public static string Date(DateOnly day) => day.ToString("dd/MM/yyyy", It);

    public static string DayName(DayOfWeek day) => It.TextInfo.ToTitleCase(It.DateTimeFormat.GetDayName(day));

    public static string DayLong(DateOnly day) => $"{DayName(day.DayOfWeek)} {day.ToString("dd/MM", It)}";

    public static DateOnly WeekStart(DateOnly day) => day.AddDays(-MondayIndex(day.DayOfWeek));

    /// <summary>Lunedì = 0 ... Domenica = 6.</summary>
    public static int MondayIndex(DayOfWeek day) => ((int)day + 6) % 7;

    public static int IsoWeek(DateOnly day) => ISOWeek.GetWeekOfYear(day.ToDateTime(TimeOnly.MinValue));

    public static int IsoYear(DateOnly day) => ISOWeek.GetYear(day.ToDateTime(TimeOnly.MinValue));

    public static DayOfWeek[] WeekDays { get; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
         DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
}
