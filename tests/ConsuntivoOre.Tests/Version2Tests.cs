using ConsuntivoOre.Core;

namespace ConsuntivoOre.Tests;

/// <summary>Novità della 2.0: priorità e issue dei todo, giornata lavorativa, dashboard.</summary>
public sealed class Version2Tests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("consuntivo-v2").FullName;
    private static readonly DateOnly Day = new(2026, 11, 2);
    private static readonly JiraIssueRef Issue = new() { Id = 10001, Key = "ABC-12", Summary = "Manutenzione", ProjectName = "Acme" };

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string PathOf(string name) => Path.Combine(_dir, name);

    // ------------------------------------------------------------ todo

    [Fact]
    public void Todos_AreOrderedByPriorityThenDueDate()
    {
        var store = new DataStore(_dir);
        store.AddTodo(new TodoItem { Name = "Media senza data" });
        store.AddTodo(new TodoItem { Name = "Bassa urgente", Priority = TodoPriority.Low, DueDate = Day });
        store.AddTodo(new TodoItem { Name = "Alta", Priority = TodoPriority.High, DueDate = Day.AddDays(9) });
        store.AddTodo(new TodoItem { Name = "Media presto", DueDate = Day });

        Assert.Equal(["Alta", "Media presto", "Media senza data", "Bassa urgente"],
            new DataStore(_dir).OpenTodos().Select(t => t.Name));
    }

    [Fact]
    public void OldTodos_AreMediumWithoutIssue()
    {
        File.WriteAllText(PathOf("todos.json"),
            """{"SchemaVersion":1,"Todos":[{"Id":"8f3c2b1a-5d4e-4a7b-9c61-2e0f3d9a7b15","Name":"Vecchio","Description":"d"}]}""");
        var todo = Assert.Single(new DataStore(_dir).Todos());
        Assert.Equal(TodoPriority.Medium, todo.Priority);
        Assert.Null(todo.Jira);
    }

    [Fact]
    public void TodoPriorityIssueAndMultilineDescription_SurviveReload()
    {
        var store = new DataStore(_dir);
        var todo = new TodoItem { Name = "T", Description = "Fare:\n- uno\n- due" };
        store.AddTodo(todo);
        store.UpdateTodo(todo.Id, "T", 2, "Fare:\n- uno\n- due\n- tre", null, TodoPriority.High, Issue);

        var reloaded = new DataStore(_dir).FindTodo(todo.Id)!;
        Assert.Equal("Fare:\n- uno\n- due\n- tre", reloaded.Description);
        Assert.Equal(TodoPriority.High, reloaded.Priority);
        Assert.Equal("ABC-12", reloaded.Jira!.Key);

        // La vecchia firma non perde priorità e issue.
        store.UpdateTodo(todo.Id, "T2", 1, "x", Day);
        reloaded = new DataStore(_dir).FindTodo(todo.Id)!;
        Assert.Equal((TodoPriority.High, "ABC-12"), (reloaded.Priority, reloaded.Jira!.Key));
    }

    [Fact]
    public void FailedTodoPriorityChange_IsRolledBack()
    {
        var store = new DataStore(_dir);
        var todo = new TodoItem { Name = "T", Description = "d" };
        store.AddTodo(todo);
        Directory.CreateDirectory(PathOf("todos.json.tmp"));
        Assert.NotNull(Record.Exception(() => store.UpdateTodo(todo.Id, "T", 0, "d", null, TodoPriority.High, Issue)));
        Assert.Equal((TodoPriority.Medium, (JiraIssueRef?)null), (todo.Priority, todo.Jira));
    }

    // ------------------------------------------------------------ giornata lavorativa

    [Fact]
    public void OldSettings_KeepReminderTimeAndTarget()
    {
        // 1.x: promemoria alle 17:30, 8 ore previste, prima voce su Tempo alle 8:30.
        File.WriteAllText(PathOf("settings.json"),
            """{"ReminderTime":"17:30:00","DailyTargetHours":8,"Jira":{"DayStart":"08:30:00"}}""");
        var s = new DataStore(_dir).Settings;

        Assert.Equal(new TimeOnly(8, 30), s.WorkStart);
        Assert.Equal(new TimeOnly(17, 30), s.WorkEnd);
        Assert.Equal(60, s.BreakMinutes);
        Assert.Equal(8, s.DailyTargetHours);
        Assert.Equal(new TimeOnly(17, 30), s.ReminderTime);
    }

    [Fact]
    public void WorkDay_DecidesReminderAndTarget()
    {
        var store = new DataStore(_dir);
        var s = store.Settings;
        (s.WorkStart, s.WorkEnd, s.BreakMinutes) = (new TimeOnly(8, 0), new TimeOnly(17, 0), 60);
        s.Normalize();
        Assert.Equal((new TimeOnly(17, 0), 8m), (s.ReminderTime, s.DailyTargetHours));

        store.SaveSettings();
        var reloaded = new DataStore(_dir).Settings;
        Assert.Equal((new TimeOnly(8, 0), new TimeOnly(17, 0), 60, 8m),
            (reloaded.WorkStart, reloaded.WorkEnd, reloaded.BreakMinutes, reloaded.DailyTargetHours));
    }

    [Fact]
    public void WorkDay_InvalidValuesAreFixed()
    {
        File.WriteAllText(PathOf("settings.json"), """{"WorkStart":"18:00:00","WorkEnd":"09:00:00","BreakMinutes":-5}""");
        var s = new DataStore(_dir).Settings;
        Assert.Equal(s.WorkStart, s.WorkEnd);
        Assert.Equal((0, 0m), (s.BreakMinutes, s.DailyTargetHours));

        File.WriteAllText(PathOf("settings.json"), """{"WorkStart":"09:00:00","WorkEnd":"13:00:00","BreakMinutes":999}""");
        Assert.Equal(240, new DataStore(_dir).Settings.BreakMinutes);
    }

    [Theory]
    [InlineData(9, 0, 18, 0, 60, "8")]
    [InlineData(9, 0, 17, 30, 30, "8")]
    [InlineData(8, 30, 12, 45, 0, "4.25")]
    [InlineData(18, 0, 9, 0, 0, "0")]
    [InlineData(9, 0, 10, 0, 120, "0")]
    public void TargetHours_AreEndMinusStartMinusBreak(int sh, int sm, int eh, int em, int pause, string expected) =>
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            AppSettings.TargetHours(new TimeOnly(sh, sm), new TimeOnly(eh, em), pause));

    // ------------------------------------------------------------ dashboard

    private static TimeEntry E(decimal hours, JiraIssueRef? jira = null) => new() { Date = Day, Hours = hours, Client = "c", Jira = jira };

    [Fact]
    public void Breakdown_ByProjectAndByIssue()
    {
        var abc12 = Issue;
        var abc13 = new JiraIssueRef { Id = 2, Key = "ABC-13", Summary = "Altro", ProjectName = "Acme" };
        var inf1 = new JiraIssueRef { Id = 3, Key = "INF-1", Summary = "Server", ProjectName = "Infrastruttura" };
        TimeEntry[] entries = [E(2, abc12), E(1, abc13), E(4, inf1), E(3), E(0)];

        var byProject = Dashboard.ByProject(entries);
        Assert.Equal(["INF", "ABC", ""], byProject.Select(r => r.Key));
        Assert.Equal(["Infrastruttura", "Acme", Dashboard.WithoutJira], byProject.Select(r => r.Label));
        Assert.Equal([4m, 3m, 3m], byProject.Select(r => r.Hours));
        Assert.Equal(0.4m, byProject[0].Share);

        var byIssue = Dashboard.ByIssue(entries);
        Assert.Equal(["INF-1", "ABC-12", "ABC-13", ""], byIssue.Select(r => r.Key));
        Assert.Equal("Server", byIssue[0].Label);
        Assert.Equal(1m, byIssue.Sum(r => r.Share));

        Assert.Empty(Dashboard.ByProject([]));
    }

    [Theory]
    [InlineData("10", "30", "33,3%")]
    [InlineData("3", "30", "10%")]
    [InlineData("1", "0", "")]
    public void Percent_HasAtMostOneDecimal(string part, string total, string expected)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Equal(expected, Fmt.Percent(decimal.Parse(part, inv), decimal.Parse(total, inv)));
    }
}
