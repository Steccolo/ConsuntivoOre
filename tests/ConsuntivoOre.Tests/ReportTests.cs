using ConsuntivoOre.Core;

namespace ConsuntivoOre.Tests;

public class ReportTests
{
    [Theory]
    [InlineData("2026-11-02", "2026-11-02")] // lunedì
    [InlineData("2026-11-08", "2026-11-02")] // domenica
    public void WeekStart_IsMonday(string day, string monday) =>
        Assert.Equal(DateOnly.Parse(monday), Fmt.WeekStart(DateOnly.Parse(day)));

    [Fact]
    public void IsoYear_FollowsTheWeekNotTheCalendar()
    {
        var day = new DateOnly(2025, 12, 29);
        Assert.Equal(1, Fmt.IsoWeek(day));
        Assert.Equal(2026, Fmt.IsoYear(day));
    }

    [Fact]
    public void Csv_QuotesSpecialCharacters()
    {
        var csv = ReportBuilder.Csv(
        [
            new TimeEntry { Date = new(2026, 11, 2), Hours = 1.5m, Client = "A;B", Note = "dice \"ok\"" },
        ]);
        Assert.Contains("02/11/2026;Lunedì;\"A;B\";1,5;\"dice \"\"ok\"\"\"", csv);
    }

    [Fact]
    public void ByClient_GroupsIgnoringCaseAndUsesLatestSpelling()
    {
        var result = ReportBuilder.ByClient(
        [
            new TimeEntry { Client = "acme", Hours = 1, CreatedAt = new(2026, 1, 1) },
            new TimeEntry { Client = "ACME", Hours = 2, CreatedAt = new(2026, 1, 2) },
        ]).Single();
        Assert.Equal(("ACME", 3m), result);
    }

    [Fact]
    public void Title_DoesNotDependOnSystemCulture() =>
        Assert.Equal("Settimana 45 · 02/11 – 08/11/2026", ReportBuilder.Title(new DateOnly(2026, 11, 2)));

    [Theory]
    [InlineData("1.5", "1h 30m")]
    [InlineData("2", "2h")]
    [InlineData("0.25", "15m")]
    [InlineData("7.75", "7h 45m")]
    [InlineData("1.33", "1h 20m")]
    [InlineData("0", "0m")]
    public void Duration_UsesJiraFormat(string hours, string expected) =>
        Assert.Equal(expected, Fmt.Duration(decimal.Parse(hours, System.Globalization.CultureInfo.InvariantCulture)));
}
