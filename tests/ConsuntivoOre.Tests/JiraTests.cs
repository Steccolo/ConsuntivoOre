using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ConsuntivoOre.Core;

namespace ConsuntivoOre.Tests;

public sealed class JiraTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("consuntivo-jira").FullName;
    private static readonly DateOnly Day = new(2026, 11, 2);
    private static readonly JiraIssueRef Issue = new() { Id = 10001, Key = "ABC-12", Summary = "Manutenzione", ProjectName = "Acme" };

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static TimeEntry Entry(decimal hours = 1.5m, JiraIssueRef? jira = null) =>
        new() { Date = Day, Hours = hours, Client = "Acme", Note = "Patch server", Jira = jira };

    // ------------------------------------------------------------ stato

    [Fact]
    public void Status_FollowsLinkAndLastSend()
    {
        var e = Entry();
        Assert.Equal(JiraStatus.None, e.JiraStatus);

        e.Jira = Issue;
        Assert.Equal(JiraStatus.ToSend, e.JiraStatus);

        e.Tempo = TempoSync.Of(e, 77, DateTime.Now);
        Assert.Equal(JiraStatus.Sent, e.JiraStatus);
        Assert.False(e.NeedsTempoSync);

        e.Hours = 2;
        Assert.Equal(JiraStatus.Changed, e.JiraStatus);

        e.Hours = 1.5m;
        e.Jira = null;
        Assert.Equal(JiraStatus.Changed, e.JiraStatus); // scollegata: va tolta da Tempo
    }

    [Fact]
    public void JiraFields_SurviveReload_AndEditMarksChanged()
    {
        var store = new DataStore(_dir);
        var entry = Entry(jira: Issue);
        store.Add(entry);
        store.SetTempoSync(entry.Id, TempoSync.Of(entry, 77, DateTime.Now));

        var reloaded = new DataStore(_dir).Find(entry.Id)!;
        Assert.Equal("ABC-12", reloaded.Jira!.Key);
        Assert.Equal(10001, reloaded.Jira.Id);
        Assert.Equal(77, reloaded.Tempo!.WorklogId);
        Assert.Equal(JiraStatus.Sent, reloaded.JiraStatus);

        // La vecchia firma di Update non perde il collegamento.
        store.Update(entry.Id, 3, "Acme", "Patch server");
        var after = new DataStore(_dir).Find(entry.Id)!;
        Assert.Equal("ABC-12", after.Jira!.Key);
        Assert.Equal(JiraStatus.Changed, after.JiraStatus);
    }

    [Fact]
    public void OldEntries_HaveNoJiraLink_AndBrokenLinksAreDropped()
    {
        File.WriteAllText(Path.Combine(_dir, "entries.json"), """
            {"SchemaVersion":1,"Entries":[
              {"Id":"8f3c2b1a-5d4e-4a7b-9c61-2e0f3d9a7b15","Date":"2026-11-02","Hours":3,"Client":"Acme","Note":""},
              {"Id":"9f3c2b1a-5d4e-4a7b-9c61-2e0f3d9a7b15","Date":"2026-11-02","Hours":1,"Client":"Acme","Note":"","Jira":{"Id":0,"Key":null}}
            ]}
            """);
        var store = new DataStore(_dir);
        Assert.Empty(store.LoadProblems);
        Assert.All(store.EntriesBetween(Day, Day), e => Assert.Equal(JiraStatus.None, e.JiraStatus));
    }

    [Fact]
    public void FailedTempoSyncSave_IsRolledBack()
    {
        var store = new DataStore(_dir);
        var entry = Entry(jira: Issue);
        store.Add(entry);
        Directory.CreateDirectory(Path.Combine(_dir, "entries.json.tmp"));
        Assert.NotNull(Record.Exception(() => store.SetTempoSync(entry.Id, TempoSync.Of(entry, 1, DateTime.Now))));
        Assert.Null(entry.Tempo);
    }

    // ------------------------------------------------------------ impostazioni

    [Theory]
    [InlineData("azienda.atlassian.net/", "https://azienda.atlassian.net")]
    [InlineData("  https://azienda.atlassian.net  ", "https://azienda.atlassian.net")]
    [InlineData("http://jira.local", "http://jira.local")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Urls_AreNormalized(string? input, string expected) =>
        Assert.Equal(expected, JiraSettings.NormalizeUrl(input));

    [Fact]
    public void MissingOrNullJiraSettings_GetDefaults()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{"Jira":null}""");
        var s = new DataStore(_dir).Settings.Jira;
        Assert.False(s.Enabled);
        Assert.Equal(JiraSettings.DefaultTempoApiUrl, s.TempoApiUrl);

        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{"Jira":{"Enabled":true,"SiteUrl":"x.atlassian.net","TempoApiUrl":""}}""");
        s = new DataStore(_dir).Settings.Jira;
        Assert.Equal("https://x.atlassian.net", s.SiteUrl);
        Assert.Equal(JiraSettings.DefaultTempoApiUrl, s.TempoApiUrl);
    }

    [Fact]
    public void Credentials_RoundTrip_AndUnreadableFileGivesEmpty()
    {
        string path = Path.Combine(_dir, "jira.dat");
        // "Cifratura" di prova: inverte i byte, così il file non è leggibile come testo.
        var store = new CredentialStore(path, b => b.Reverse().ToArray(), b => b.Reverse().ToArray());
        store.Save(new JiraCredentials { JiraToken = "jt", TempoToken = "tt" });
        Assert.DoesNotContain("jt", File.ReadAllText(path));
        Assert.Equal(("jt", "tt"), (store.Load().JiraToken, store.Load().TempoToken));

        var other = new CredentialStore(path, b => b, _ => throw new InvalidOperationException("altro utente"));
        Assert.Equal("", other.Load().JiraToken);
    }

    // ------------------------------------------------------------ JQL e worklog

    [Theory]
    [InlineData("ABC-12", true)]
    [InlineData(" abc-12 ", true)]
    [InlineData("A1_B-7", true)]
    [InlineData("ABC", false)]
    [InlineData("12-ABC", false)]
    [InlineData("ABC-12 server", false)]
    public void IssueKeys_AreRecognized(string query, bool expected) => Assert.Equal(expected, Jql.IsIssueKey(query));

    [Fact]
    public void Jql_SearchesOnlyLoggableProjects()
    {
        string[] projects = ["ABC", "DEF"];
        Assert.Equal("""project in ("ABC","DEF") AND (assignee = currentUser() OR worklogAuthor = currentUser()) ORDER BY updated DESC""",
            Jql.Search("  ", projects));
        Assert.Equal("""project = "DEF" ORDER BY updated DESC""", Jql.Search("def", projects));
        Assert.Equal("""project in ("ABC","DEF") AND summary ~ "server* posta*" ORDER BY updated DESC""",
            Jql.Search("server posta", projects));
        // Virgolette e caratteri speciali non rompono la query.
        Assert.Equal("""project in ("ABC","DEF") AND summary ~ "a* b*" ORDER BY updated DESC""",
            Jql.Search("\"a\" (b)*", projects));
        Assert.Equal("\"x\\\"y\\\\\"", Jql.Quote("x\"y\\"));
    }

    [Theory]
    [InlineData("1.5", 5400)]
    [InlineData("0.25", 900)]
    [InlineData("0.33", 1200)] // 19,8 minuti → 20
    [InlineData("8", 28800)]
    public void Hours_BecomeWholeMinutes(string hours, int seconds) =>
        Assert.Equal(seconds, TempoWorklog.ToSeconds(decimal.Parse(hours, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Description_FallsBackToIssue()
    {
        Assert.Equal("Patch server", TempoWorklog.DescriptionFor(Entry(jira: Issue)));
        var e = Entry(jira: Issue);
        e.Note = "  ";
        Assert.Equal("Manutenzione", TempoWorklog.DescriptionFor(e));
    }

    [Fact]
    public void StartTimes_FollowInsertionOrder()
    {
        var t0 = new DateTime(2026, 11, 2, 18, 0, 0);
        var a = new TimeEntry { Date = Day, Hours = 1.5m, CreatedAt = t0 };
        var b = new TimeEntry { Date = Day, Hours = 2, CreatedAt = t0.AddMinutes(1) };
        var c = new TimeEntry { Date = Day, Hours = 1, CreatedAt = t0.AddMinutes(2) };
        var otherDay = new TimeEntry { Date = Day.AddDays(1), Hours = 8, CreatedAt = t0 };
        TimeEntry[] all = [c, otherDay, b, a];
        var start = new TimeOnly(9, 0);

        Assert.Equal(new TimeOnly(9, 0), JiraService.StartTime(all, a, start));
        Assert.Equal(new TimeOnly(10, 30), JiraService.StartTime(all, b, start));
        Assert.Equal(new TimeOnly(12, 30), JiraService.StartTime(all, c, start));

        var late = new TimeEntry { Date = Day, Hours = 30, CreatedAt = t0.AddMinutes(-1) };
        Assert.Equal(new TimeOnly(23, 59), JiraService.StartTime([late, a], a, start));
    }

    [Fact]
    public void ErrorDetails_AreReadFromJiraAndTempo()
    {
        Assert.Equal("Issue inesistente\nworklog: no", JiraClient.ErrorDetail("""{"errorMessages":["Issue inesistente"],"errors":{"worklog":"no"}}"""));
        Assert.Equal("Account obbligatorio", JiraClient.ErrorDetail("""{"errors":[{"message":"Account obbligatorio"}]}"""));
        Assert.Equal("", JiraClient.ErrorDetail("<html>"));
    }

    // ------------------------------------------------------------ invio con un server finto

    private (DataStore, JiraService, FakeServer) Service()
    {
        var store = new DataStore(_dir);
        store.Settings.Jira = new JiraSettings { Enabled = true, SiteUrl = "https://x.atlassian.net", Email = "me@x.it" };
        var creds = new CredentialStore(Path.Combine(_dir, "jira.dat"), b => b, b => b);
        creds.Save(new JiraCredentials { JiraToken = "jt", TempoToken = "tt" });
        var server = new FakeServer();
        return (store, new JiraService(store, creds, server), server);
    }

    [Fact]
    public async Task Sync_CreatesUpdatesAndRemovesWorklogs()
    {
        var (store, jira, server) = Service();
        var entry = Entry(jira: Issue);
        store.Add(entry);

        var result = await jira.SyncAsync([entry.Id]);
        Assert.True(result.Ok, string.Join("\n", result.Errors));
        Assert.Equal(1, result.Sent);
        var post = server.Requests.Single(r => r.Method == HttpMethod.Post && r.Url.EndsWith("/4/worklogs"));
        Assert.Equal("Bearer tt", post.Auth);
        var body = JsonNode.Parse(post.Body)!;
        Assert.Equal(10001, body["issueId"]!.GetValue<long>());
        Assert.Equal("acc-1", body["authorAccountId"]!.GetValue<string>());
        Assert.Equal(5400, body["timeSpentSeconds"]!.GetValue<int>());
        Assert.Equal("2026-11-02", body["startDate"]!.GetValue<string>());
        Assert.Equal("09:00:00", body["startTime"]!.GetValue<string>());
        Assert.Equal(500, store.Find(entry.Id)!.Tempo!.WorklogId);
        Assert.Equal(JiraStatus.Sent, store.Find(entry.Id)!.JiraStatus);

        // Già inviata e invariata: nessuna nuova chiamata.
        int count = server.Requests.Count;
        Assert.Equal(0, (await jira.SyncAsync([entry.Id])).Sent);
        Assert.Equal(count, server.Requests.Count);

        store.Update(entry.Id, 2, "Acme", "Patch server", Issue);
        Assert.Equal(1, (await jira.SyncAsync([entry.Id])).Sent);
        Assert.Contains(server.Requests, r => r.Method == HttpMethod.Put && r.Url.EndsWith("/4/worklogs/500"));
        Assert.Equal(JiraStatus.Sent, store.Find(entry.Id)!.JiraStatus);

        store.Update(entry.Id, 2, "Acme", "Patch server", null);
        result = await jira.SyncAsync([entry.Id]);
        Assert.Equal(1, result.Removed);
        Assert.Contains(server.Requests, r => r.Method == HttpMethod.Delete && r.Url.EndsWith("/4/worklogs/500"));
        Assert.Equal(JiraStatus.None, store.Find(entry.Id)!.JiraStatus);
    }

    [Fact]
    public async Task Sync_RecreatesWorklogDeletedOnTempo()
    {
        var (store, jira, server) = Service();
        var entry = Entry(jira: Issue);
        store.Add(entry);
        store.SetTempoSync(entry.Id, TempoSync.Of(entry, 999, DateTime.Now));
        store.Update(entry.Id, 4, "Acme", "Patch server", Issue);
        server.Respond(HttpMethod.Put, "/4/worklogs/999", HttpStatusCode.NotFound, "{}");

        Assert.Equal(1, (await jira.SyncAsync([entry.Id])).Sent);
        Assert.Equal(500, store.Find(entry.Id)!.Tempo!.WorklogId);
    }

    [Fact]
    public async Task Sync_ReportsTempoErrors_AndLeavesEntryToSend()
    {
        var (store, jira, server) = Service();
        var entry = Entry(jira: Issue);
        store.Add(entry);
        server.Respond(HttpMethod.Post, "/4/worklogs", HttpStatusCode.BadRequest, """{"errors":[{"message":"Account obbligatorio"}]}""");

        var result = await jira.SyncAsync([entry.Id]);
        Assert.False(result.Ok);
        Assert.Contains("Account obbligatorio", Assert.Single(result.Errors));
        Assert.Equal(JiraStatus.ToSend, store.Find(entry.Id)!.JiraStatus);
    }

    [Fact]
    public async Task Search_UsesOnlyProjectsWhereICanLogWork()
    {
        var (_, jira, server) = Service();
        server.Respond(HttpMethod.Get, "/rest/api/3/issue/XYZ-1", HttpStatusCode.OK,
            """{"id":"7","key":"XYZ-1","fields":{"summary":"Altro","project":{"name":"Xyz"}}}""");

        var found = await jira.SearchAsync("server");
        Assert.Equal("ABC-12", Assert.Single(found).Key);
        var search = server.Requests.Single(r => r.Url.EndsWith("/rest/api/3/search/jql"));
        Assert.StartsWith("Basic ", search.Auth);
        Assert.Contains("project in (\\u0022ABC\\u0022)", search.Body);

        // Issue esistente ma in un progetto dove non posso registrare ore.
        Assert.Empty(await jira.SearchAsync("xyz-1"));
    }

    [Fact]
    public async Task WrongJiraToken_GivesClearMessage()
    {
        var (_, jira, server) = Service();
        server.Respond(HttpMethod.Post, "/rest/api/3/permissions/project", HttpStatusCode.Unauthorized, "");
        var ex = await Assert.ThrowsAsync<JiraHttpException>(() => jira.SearchAsync(""));
        Assert.Contains("email e token API", ex.Message);
    }

    /// <summary>Simula Jira e Tempo: risposte predefinite, sostituibili per singola chiamata.</summary>
    private sealed class FakeServer : HttpMessageHandler
    {
        public sealed record Request(HttpMethod Method, string Url, string? Auth, string Body);

        public List<Request> Requests { get; } = [];
        private readonly Dictionary<(HttpMethod, string), (HttpStatusCode, string)> _overrides = [];

        public void Respond(HttpMethod method, string pathEnd, HttpStatusCode status, string body) =>
            _overrides[(method, pathEnd)] = (status, body);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string url = request.RequestUri!.GetLeftPart(UriPartial.Path);
            string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add(new Request(request.Method, url, request.Headers.Authorization?.ToString(), body));

            var match = _overrides.FirstOrDefault(o => o.Key.Item1 == request.Method && url.EndsWith(o.Key.Item2));
            var (status, text) = match.Key.Item2 is not null ? match.Value : Default(request.Method, url);
            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        }

        private static (HttpStatusCode, string) Default(HttpMethod method, string url) => url switch
        {
            _ when url.EndsWith("/myself") => (HttpStatusCode.OK, """{"accountId":"acc-1","displayName":"Mario Rossi"}"""),
            _ when url.EndsWith("/permissions/project") => (HttpStatusCode.OK, """{"projects":[{"id":1,"key":"ABC"}]}"""),
            _ when url.EndsWith("/search/jql") => (HttpStatusCode.OK,
                """{"issues":[{"id":"10001","key":"ABC-12","fields":{"summary":"Manutenzione","project":{"name":"Acme"}}}]}"""),
            _ when method == HttpMethod.Delete => (HttpStatusCode.NoContent, ""),
            _ when url.Contains("/4/worklogs") && method != HttpMethod.Get => (HttpStatusCode.OK, """{"tempoWorklogId":500}"""),
            _ when url.Contains("/4/worklogs") => (HttpStatusCode.OK, """{"results":[]}"""),
            _ => (HttpStatusCode.NotFound, "{}"),
        };
    }
}
