using System.Text.Json.Serialization;

namespace ConsuntivoOre.Core;

/// <summary>Issue di Jira a cui è collegata una voce (copia dei dati al momento della scelta).</summary>
public sealed class JiraIssueRef
{
    /// <summary>Id numerico della issue: Tempo vuole questo, non la chiave.</summary>
    public long Id { get; set; }
    public string Key { get; set; } = "";
    public string Summary { get; set; } = "";
    public string ProjectName { get; set; } = "";

    [JsonIgnore]
    public string Display => Summary.Length > 0 ? $"{Key} — {Summary}" : Key;
}

/// <summary>Ultimo invio riuscito a Tempo: serve a capire se la voce è cambiata dopo l'invio.</summary>
public sealed class TempoSync
{
    public long WorklogId { get; set; }
    public long IssueId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Hours { get; set; }
    public string Note { get; set; } = "";
    public DateTime SentAt { get; set; }

    public static TempoSync Of(TimeEntry entry, long worklogId, DateTime sentAt) => new()
    {
        WorklogId = worklogId,
        IssueId = entry.Jira?.Id ?? 0,
        Date = entry.Date,
        Hours = entry.Hours,
        Note = entry.Note,
        SentAt = sentAt,
    };
}

public enum JiraStatus
{
    /// <summary>Voce solo locale.</summary>
    None,
    /// <summary>Collegata a una issue, mai inviata.</summary>
    ToSend,
    /// <summary>Su Tempo con gli stessi dati.</summary>
    Sent,
    /// <summary>Inviata, poi modificata (o scollegata) in locale: va aggiornata su Tempo.</summary>
    Changed,
}

/// <summary>Collegamento a Jira Cloud e Tempo; i token stanno a parte, cifrati (<see cref="CredentialStore"/>).</summary>
public sealed class JiraSettings
{
    public const string DefaultTempoApiUrl = "https://api.tempo.io/4";

    public bool Enabled { get; set; }
    /// <summary>Es. https://azienda.atlassian.net</summary>
    public string SiteUrl { get; set; } = "";
    public string Email { get; set; } = "";
    /// <summary>Cambia solo per le istanze Tempo con dati in UE/USA (api.eu.tempo.io...).</summary>
    public string TempoApiUrl { get; set; } = DefaultTempoApiUrl;
    /// <summary>Al salvataggio di una voce collegata la invia subito a Tempo.</summary>
    public bool SendImmediately { get; set; } = true;
    /// <summary>Ora di inizio della prima voce del giorno su Tempo; le altre seguono in fila.</summary>
    public TimeOnly DayStart { get; set; } = new(9, 0);

    public void Normalize()
    {
        SiteUrl = NormalizeUrl(SiteUrl);
        Email = (Email ?? "").Trim();
        TempoApiUrl = NormalizeUrl(TempoApiUrl);
        if (TempoApiUrl.Length == 0) TempoApiUrl = DefaultTempoApiUrl;
    }

    /// <summary>"azienda.atlassian.net/" → "https://azienda.atlassian.net".</summary>
    public static string NormalizeUrl(string? url)
    {
        string u = (url ?? "").Trim().TrimEnd('/');
        if (u.Length == 0) return "";
        if (!u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            u = "https://" + u;
        return u;
    }
}

public sealed class JiraCredentials
{
    public string JiraToken { get; set; } = "";
    public string TempoToken { get; set; } = "";
}

/// <summary>Errore spiegabile all'utente (credenziali, permessi, rete, dati rifiutati).</summary>
public class JiraException(string message, Exception? inner = null) : Exception(message, inner);
