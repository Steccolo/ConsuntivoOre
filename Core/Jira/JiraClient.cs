using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ConsuntivoOre.Core;

public sealed record JiraUser(string AccountId, string DisplayName);

/// <summary>Worklog come lo vuole Tempo (API v4).</summary>
public sealed record TempoWorklog(string AuthorAccountId, long IssueId, DateOnly Date, TimeOnly StartTime, int Seconds, string Description)
{
    /// <summary>Ore → secondi arrotondati al minuto (Tempo li mostra in minuti).</summary>
    public static int ToSeconds(decimal hours) => (int)Math.Round(hours * 60, MidpointRounding.AwayFromZero) * 60;

    /// <summary>Tempo non accetta descrizioni vuote: senza nota si usa la issue.</summary>
    public static string DescriptionFor(TimeEntry entry)
    {
        string note = entry.Note.Trim();
        if (note.Length > 0) return note;
        return entry.Jira is { } j ? (j.Summary.Length > 0 ? j.Summary : j.Key) : entry.Client;
    }

    public JsonObject ToJson() => new()
    {
        ["authorAccountId"] = AuthorAccountId,
        ["issueId"] = IssueId,
        ["startDate"] = Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["startTime"] = StartTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        ["timeSpentSeconds"] = Seconds,
        ["description"] = Description,
    };
}

/// <summary>Costruzione delle ricerche JQL (separata per poterla testare).</summary>
public static partial class Jql
{
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*-\d+$")]
    private static partial Regex IssueKeyPattern();

    // Caratteri con un significato nella ricerca testuale di Jira (Lucene): si tolgono.
    [GeneratedRegex(@"[+\-&|!(){}\[\]^~*?:\\/""']")]
    private static partial Regex SpecialChars();

    public static bool IsIssueKey(string query) => IssueKeyPattern().IsMatch(query.Trim());

    public static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    /// <summary>
    /// Ricerca tra i progetti consentiti: vuota → issue recenti assegnate o lavorate da me;
    /// chiave di un progetto → issue recenti del progetto; altrimenti parole del titolo (anche iniziali).
    /// </summary>
    public static string Search(string query, IReadOnlyCollection<string> projectKeys)
    {
        string q = query.Trim();
        var exactProject = projectKeys.FirstOrDefault(k => string.Equals(k, q, StringComparison.OrdinalIgnoreCase));
        if (exactProject is not null)
            return $"project = {Quote(exactProject)} ORDER BY updated DESC";

        string projects = $"project in ({string.Join(",", projectKeys.Select(Quote))})";
        var words = SpecialChars().Replace(q, " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return $"{projects} AND (assignee = currentUser() OR worklogAuthor = currentUser()) ORDER BY updated DESC";

        string text = string.Join(" ", words.Select(w => w + "*"));
        return $"{projects} AND summary ~ {Quote(text)} ORDER BY updated DESC";
    }
}

/// <summary>
/// Chiamate a Jira Cloud (utente, progetti in cui posso registrare lavoro, ricerca issue) e a Tempo (worklog).
/// Jira: email + token API (Basic). Tempo: token personale (Bearer).
/// </summary>
public sealed class JiraClient : IDisposable
{
    private const string JiraName = "Jira";
    private const string TempoName = "Tempo";
    private const int MaxResults = 30;

    private readonly HttpClient _http;
    private readonly string _site;
    private readonly string _tempo;
    private readonly AuthenticationHeaderValue _jiraAuth;
    private readonly AuthenticationHeaderValue _tempoAuth;

    private JiraUser? _me;
    private IReadOnlyList<string>? _projects;

    public JiraClient(JiraSettings settings, JiraCredentials credentials, HttpMessageHandler? handler = null)
    {
        _site = JiraSettings.NormalizeUrl(settings.SiteUrl);
        _tempo = JiraSettings.NormalizeUrl(settings.TempoApiUrl);
        if (_site.Length == 0) throw new JiraException("Indica l'indirizzo di Jira nelle impostazioni.");
        if (settings.Email.Trim().Length == 0 || credentials.JiraToken.Trim().Length == 0)
            throw new JiraException("Indica email e token API di Jira nelle impostazioni.");

        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _jiraAuth = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Email.Trim()}:{credentials.JiraToken.Trim()}")));
        _tempoAuth = new AuthenticationHeaderValue("Bearer", credentials.TempoToken.Trim());
        HasTempoToken = credentials.TempoToken.Trim().Length > 0;
    }

    public bool HasTempoToken { get; }

    public void Dispose() => _http.Dispose();

    // ------------------------------------------------------------ Jira

    public async Task<JiraUser> MyselfAsync(CancellationToken ct = default)
    {
        if (_me is not null) return _me;
        var json = await SendAsync(JiraName, HttpMethod.Get, $"{_site}/rest/api/3/myself", null, ct);
        string id = json?["accountId"]?.GetValue<string>() ?? throw new JiraException("Jira non ha restituito l'utente.");
        return _me = new JiraUser(id, json!["displayName"]?.GetValue<string>() ?? "");
    }

    /// <summary>Chiavi dei progetti in cui l'utente ha il permesso di registrare lavoro.</summary>
    public async Task<IReadOnlyList<string>> LoggableProjectsAsync(CancellationToken ct = default)
    {
        if (_projects is not null) return _projects;
        var body = new JsonObject { ["permissions"] = new JsonArray("WORK_ON_ISSUES") };
        var json = await SendAsync(JiraName, HttpMethod.Post, $"{_site}/rest/api/3/permissions/project", body, ct);
        var keys = (json?["projects"] as JsonArray ?? [])
            .Select(p => p?["key"]?.GetValue<string>())
            .OfType<string>()
            .ToList();
        return _projects = keys;
    }

    public async Task<IReadOnlyList<JiraIssueRef>> SearchIssuesAsync(string query, CancellationToken ct = default)
    {
        var projects = await LoggableProjectsAsync(ct);
        if (projects.Count == 0)
            throw new JiraException("Il tuo utente non ha progetti Jira in cui registrare ore.");

        if (Jql.IsIssueKey(query))
        {
            var issue = await GetIssueAsync(query.Trim().ToUpperInvariant(), ct);
            return issue is not null && projects.Contains(ProjectKeyOf(issue.Key), StringComparer.OrdinalIgnoreCase)
                ? [issue]
                : [];
        }

        var body = new JsonObject
        {
            ["jql"] = Jql.Search(query, projects),
            ["fields"] = new JsonArray("summary", "project"),
            ["maxResults"] = MaxResults,
        };
        var json = await SendAsync(JiraName, HttpMethod.Post, $"{_site}/rest/api/3/search/jql", body, ct);
        return (json?["issues"] as JsonArray ?? []).Select(ParseIssue).OfType<JiraIssueRef>().ToList();
    }

    private async Task<JiraIssueRef?> GetIssueAsync(string key, CancellationToken ct)
    {
        try
        {
            var json = await SendAsync(JiraName, HttpMethod.Get,
                $"{_site}/rest/api/3/issue/{Uri.EscapeDataString(key)}?fields=summary,project", null, ct);
            return ParseIssue(json);
        }
        catch (JiraHttpException ex) when (ex.Status == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static string ProjectKeyOf(string issueKey) => issueKey[..Math.Max(0, issueKey.LastIndexOf('-'))];

    public static JiraIssueRef? ParseIssue(JsonNode? node)
    {
        if (node is null) return null;
        string? key = node["key"]?.GetValue<string>();
        string? idText = node["id"]?.ToString();
        if (key is null || !long.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out long id)) return null;
        var fields = node["fields"];
        return new JiraIssueRef
        {
            Id = id,
            Key = key,
            Summary = fields?["summary"]?.GetValue<string>() ?? "",
            ProjectName = fields?["project"]?["name"]?.GetValue<string>() ?? "",
        };
    }

    // ------------------------------------------------------------ Tempo

    /// <summary>Controlla che il token di Tempo funzioni (lettura dei miei worklog di oggi).</summary>
    public async Task TestTempoAsync(CancellationToken ct = default)
    {
        RequireTempo();
        var me = await MyselfAsync(ct);
        string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await SendAsync(TempoName, HttpMethod.Get,
            $"{_tempo}/worklogs/user/{Uri.EscapeDataString(me.AccountId)}?from={today}&to={today}&limit=1", null, ct);
    }

    public async Task<long> CreateWorklogAsync(TempoWorklog worklog, CancellationToken ct = default)
    {
        RequireTempo();
        var json = await SendAsync(TempoName, HttpMethod.Post, $"{_tempo}/worklogs", worklog.ToJson(), ct);
        return WorklogIdOf(json);
    }

    /// <summary>Aggiorna il worklog; se su Tempo è stato cancellato ne crea uno nuovo.</summary>
    public async Task<long> UpdateWorklogAsync(long worklogId, TempoWorklog worklog, CancellationToken ct = default)
    {
        RequireTempo();
        try
        {
            var json = await SendAsync(TempoName, HttpMethod.Put, $"{_tempo}/worklogs/{worklogId}", worklog.ToJson(), ct);
            return WorklogIdOf(json, worklogId);
        }
        catch (JiraHttpException ex) when (ex.Status == HttpStatusCode.NotFound)
        {
            return await CreateWorklogAsync(worklog, ct);
        }
    }

    /// <summary>Cancella il worklog; se non c'è già più va bene così.</summary>
    public async Task DeleteWorklogAsync(long worklogId, CancellationToken ct = default)
    {
        RequireTempo();
        try
        {
            await SendAsync(TempoName, HttpMethod.Delete, $"{_tempo}/worklogs/{worklogId}", null, ct);
        }
        catch (JiraHttpException ex) when (ex.Status == HttpStatusCode.NotFound)
        {
        }
    }

    private void RequireTempo()
    {
        if (!HasTempoToken) throw new JiraException("Indica il token di Tempo nelle impostazioni.");
    }

    private static long WorklogIdOf(JsonNode? json, long fallback = 0)
    {
        string? text = json?["tempoWorklogId"]?.ToString();
        if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long id)) return id;
        return fallback > 0 ? fallback : throw new JiraException("Tempo non ha restituito l'identificativo del worklog.");
    }

    // ------------------------------------------------------------ HTTP

    private async Task<JsonNode?> SendAsync(string service, HttpMethod method, string url, JsonNode? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = service == TempoName ? _tempoAuth : _jiraAuth;
        if (body is not null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        string text;
        try
        {
            response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new JiraException($"Impossibile raggiungere {service}: {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new JiraHttpException(response.StatusCode, Explain(service, response.StatusCode, ErrorDetail(text)));
            if (text.Length == 0) return null;
            try
            {
                return JsonNode.Parse(text);
            }
            catch (JsonException ex)
            {
                throw new JiraException($"{service} ha risposto in modo inatteso (controlla l'indirizzo nelle impostazioni).", ex);
            }
        }
    }

    public static string Explain(string service, HttpStatusCode status, string detail)
    {
        string suffix = detail.Length > 0 ? $"\n{detail}" : "";
        return status switch
        {
            HttpStatusCode.Unauthorized when service == TempoName =>
                "Tempo ha rifiutato il token: controllalo o creane uno nuovo." + suffix,
            HttpStatusCode.Unauthorized => "Jira ha rifiutato l'accesso: controlla email e token API." + suffix,
            HttpStatusCode.Forbidden => $"{service}: permesso negato." + suffix,
            HttpStatusCode.NotFound => $"{service}: risorsa non trovata (controlla l'indirizzo)." + suffix,
            HttpStatusCode.TooManyRequests => $"{service}: troppe richieste, riprova tra qualche minuto.",
            _ => $"{service} ha risposto con l'errore {(int)status}." + suffix,
        };
    }

    /// <summary>Messaggi d'errore di Jira (<c>errorMessages</c>, <c>errors</c>) e di Tempo (<c>errors[].message</c>).</summary>
    public static string ErrorDetail(string body)
    {
        try
        {
            var json = JsonNode.Parse(body);
            var messages = new List<string>();
            if (json?["errorMessages"] is JsonArray em) messages.AddRange(em.Select(m => m?.ToString()).OfType<string>());
            switch (json?["errors"])
            {
                case JsonObject obj:
                    messages.AddRange(obj.Select(kv => $"{kv.Key}: {kv.Value}"));
                    break;
                case JsonArray arr:
                    messages.AddRange(arr.Select(e => e?["message"]?.ToString() ?? e?.ToString()).OfType<string>());
                    break;
            }
            if (messages.Count == 0 && json?["message"]?.ToString() is { Length: > 0 } m) messages.Add(m);
            return string.Join("\n", messages.Where(s => s.Length > 0));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return "";
        }
    }
}

/// <summary>Errore HTTP con il codice, per gestire i casi come "non trovato".</summary>
public sealed class JiraHttpException(HttpStatusCode status, string message) : JiraException(message)
{
    public HttpStatusCode Status { get; } = status;
}
