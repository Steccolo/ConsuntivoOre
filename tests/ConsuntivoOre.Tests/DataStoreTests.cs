using ConsuntivoOre.Core;

namespace ConsuntivoOre.Tests;

public sealed class DataStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("consuntivo-test").FullName;
    private static readonly DateOnly Day = new(2026, 11, 2);

    private string PathOf(string name) => Path.Combine(_dir, name);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static TimeEntry Entry(decimal hours = 1) =>
        new() { Date = Day, Hours = hours, Client = "Acme", Note = "n" };

    [Fact]
    public void Entries_SurviveReload()
    {
        new DataStore(_dir).Add(Entry(2.5m));
        var reloaded = new DataStore(_dir);
        Assert.Equal(2.5m, reloaded.HoursOn(Day));
        Assert.Empty(reloaded.LoadProblems);
    }

    [Fact]
    public void LegacyEntriesArray_IsStillRead()
    {
        File.WriteAllText(PathOf("entries.json"),
            """[{"Id":"8f3c2b1a-5d4e-4a7b-9c61-2e0f3d9a7b15","Date":"2026-11-02","Hours":3,"Client":"Acme","Note":null}]""");
        var store = new DataStore(_dir);
        Assert.Equal(3, store.HoursOn(Day));
        Assert.Equal("", store.EntriesBetween(Day, Day)[0].Note);
        Assert.Empty(store.LoadProblems);
    }

    [Theory]
    [InlineData("{ non è json")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("{}")]
    public void UnreadableEntries_AreSetAsideAndReported(string content)
    {
        File.WriteAllText(PathOf("entries.json"), content);
        var store = new DataStore(_dir);
        Assert.Single(store.LoadProblems);
        var copy = Assert.Single(Directory.GetFiles(_dir, "entries.json.corrotto-*"));
        Assert.Equal(content, File.ReadAllText(copy));
    }

    [Fact]
    public void NewerSchema_RefusesToStartAndKeepsTheFile()
    {
        const string content = """{"SchemaVersion":999,"Entries":[]}""";
        File.WriteAllText(PathOf("entries.json"), content);
        Assert.Throws<DataStoreException>(() => new DataStore(_dir));
        Assert.Equal(content, File.ReadAllText(PathOf("entries.json")));
    }

    [Fact]
    public void FailedAdd_LeavesMemoryUnchanged()
    {
        var store = new DataStore(_dir);
        store.Add(Entry());
        int notified = 0;
        store.EntriesChanged += () => notified++;

        // Il file temporaneo non si può creare: il salvataggio fallisce.
        Directory.CreateDirectory(PathOf("entries.json.tmp"));
        var ex = Record.Exception(() => store.Add(Entry(5)));

        Assert.True(ex is not null && DataStore.IsFileError(ex));
        Assert.Equal(1, store.HoursOn(Day));
        Assert.Equal(0, notified);
    }

    [Fact]
    public void FailedUpdateAndDelete_AreRolledBack()
    {
        var store = new DataStore(_dir);
        var entry = Entry();
        store.Add(entry);
        Directory.CreateDirectory(PathOf("entries.json.tmp"));

        Assert.NotNull(Record.Exception(() => store.Update(entry.Id, 9, "Altro", "x")));
        Assert.Equal((1m, "Acme", "n"), (entry.Hours, entry.Client, entry.Note));

        Assert.NotNull(Record.Exception(() => store.Delete(entry.Id)));
        Assert.NotNull(store.Find(entry.Id));
    }

    [Fact]
    public void LegacySchedulerState_IsMigratedFromSettings()
    {
        File.WriteAllText(PathOf("settings.json"), """{"LastReportWeek":"2026-11-02"}""");
        var store = new DataStore(_dir);
        Assert.Equal(Day, store.State.LastReportWeek);

        store.SaveSettings();
        Assert.Equal(Day, new DataStore(_dir).State.LastReportWeek);
    }

    [Fact]
    public void InvalidSettings_AreNormalized()
    {
        File.WriteAllText(PathOf("settings.json"),
            """{"ReminderDays":null,"ReminderRepeatMinutes":-5,"DailyTargetHours":99}""");
        var s = new DataStore(_dir).Settings;
        Assert.Empty(s.ReminderDays);
        Assert.Equal(0, s.ReminderRepeatMinutes);
        Assert.Equal(24, s.DailyTargetHours);
    }

    [Fact]
    public void Backups_AreRotated()
    {
        new DataStore(_dir).Add(Entry());
        var backups = PathOf("backup");
        Directory.CreateDirectory(backups);
        for (int i = 1; i <= 10; i++)
            File.WriteAllText(Path.Combine(backups, $"entries-200001{i:00}.json"), "[]");

        new DataStore(_dir);

        var files = Directory.GetFiles(backups);
        Assert.Equal(7, files.Length);
        Assert.Contains(files, f => f.EndsWith($"entries-{DateTime.Now:yyyyMMdd}.json"));
    }

    [Fact]
    public void Todos_SurviveReloadAndAreOrderedByDueDate()
    {
        var store = new DataStore(_dir);
        store.AddTodo(new TodoItem { Name = "Senza data", PlannedHours = 1 });
        store.AddTodo(new TodoItem { Name = "Tardi", PlannedHours = 2, DueDate = Day.AddDays(5) });
        store.AddTodo(new TodoItem { Name = "Presto", PlannedHours = 3, Description = "d", DueDate = Day });

        var names = new DataStore(_dir).OpenTodos().Select(t => t.Name);
        Assert.Equal(["Presto", "Tardi", "Senza data"], names);
    }

    [Fact]
    public void CompletedTodo_LeavesTheOpenList()
    {
        var store = new DataStore(_dir);
        var todo = new TodoItem { Name = "Task", PlannedHours = 1 };
        store.AddTodo(todo);
        store.CompleteTodo(todo.Id);

        var reloaded = new DataStore(_dir);
        Assert.Empty(reloaded.OpenTodos());
        Assert.True(reloaded.FindTodo(todo.Id)!.IsDone);
    }

    [Fact]
    public void CompletedTodos_StayListedAfterOpenOnes_AndCanBeReopened()
    {
        var store = new DataStore(_dir);
        var done = new TodoItem { Name = "Fatto", DueDate = Day };
        var open = new TodoItem { Name = "Da fare", DueDate = Day.AddDays(3) };
        store.AddTodo(done);
        store.AddTodo(open);
        store.CompleteTodo(done.Id);

        Assert.Equal(["Da fare", "Fatto"], new DataStore(_dir).Todos().Select(t => t.Name));

        store.ReopenTodo(done.Id);
        Assert.Equal(["Fatto", "Da fare"], new DataStore(_dir).Todos().Select(t => t.Name));
        Assert.False(store.FindTodo(done.Id)!.IsDone);
    }

    [Fact]
    public void HoursLoggedFromTodo_AreCounted()
    {
        var store = new DataStore(_dir);
        var todo = new TodoItem { Name = "Task", PlannedHours = 4 };
        store.AddTodo(todo);
        store.Add(new TimeEntry { Date = Day, Hours = 1.5m, Client = "Task", TodoId = todo.Id });
        store.Add(Entry(2));

        Assert.Equal(1.5m, new DataStore(_dir).HoursForTodo(todo.Id));
    }

    [Fact]
    public void FailedTodoUpdate_IsRolledBack()
    {
        var store = new DataStore(_dir);
        var todo = new TodoItem { Name = "Task", PlannedHours = 1 };
        store.AddTodo(todo);
        int notified = 0;
        store.TodosChanged += () => notified++;
        Directory.CreateDirectory(PathOf("todos.json.tmp"));

        Assert.NotNull(Record.Exception(() => store.UpdateTodo(todo.Id, "Altro", 9, "x", Day)));
        Assert.Equal(("Task", 1m, "", (DateOnly?)null), (todo.Name, todo.PlannedHours, todo.Description, todo.DueDate));
        Assert.NotNull(Record.Exception(() => store.CompleteTodo(todo.Id)));
        Assert.False(todo.IsDone);
        Assert.Equal(0, notified);
    }

    [Fact]
    public void UnreadableTodos_AreSetAsideAndReported()
    {
        File.WriteAllText(PathOf("todos.json"), "{ rotto");
        var store = new DataStore(_dir);
        Assert.Single(store.LoadProblems);
        Assert.Empty(store.OpenTodos());
    }

    [Fact]
    public void ColumnWidths_SurviveReloadAndAreKeptInRange()
    {
        var store = new DataStore(_dir);
        store.Settings.ColumnWidths["todo"] = [160, 95.5];
        store.SaveSettings();
        Assert.Equal([160, 95.5], new DataStore(_dir).Settings.ColumnWidths["todo"]);

        File.WriteAllText(PathOf("settings.json"), """{"ColumnWidths":{"todo":[1,99999],"ore":null}}""");
        var widths = new DataStore(_dir).Settings.ColumnWidths;
        Assert.Equal([30, 2000], widths["todo"]);
        Assert.False(widths.ContainsKey("ore"));
    }
}
