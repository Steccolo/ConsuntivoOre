using System.Text;
using System.Text.Json;

namespace ConsuntivoOre.Core;

/// <summary>
/// Token di Jira e Tempo in un file a parte (<c>jira.dat</c>), cifrati con la funzione data dalla piattaforma
/// (Windows: DPAPI dell'utente). Mai in <c>settings.json</c>, che si copia e si apre a mano.
/// </summary>
public sealed class CredentialStore(string path, Func<byte[], byte[]> protect, Func<byte[], byte[]> unprotect)
{
    public string FilePath { get; } = path;

    /// <summary>
    /// Credenziali salvate; vuote se il file manca o non si decifra (es. copiato da un altro PC o utente):
    /// a differenza dei dati, i token si rigenerano, quindi basta reinserirli.
    /// </summary>
    public JiraCredentials Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new JiraCredentials();
            byte[] plain = unprotect(File.ReadAllBytes(FilePath));
            var creds = JsonSerializer.Deserialize<JiraCredentials>(Encoding.UTF8.GetString(plain)) ?? new JiraCredentials();
            creds.JiraToken ??= "";
            creds.TempoToken ??= "";
            return creds;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return new JiraCredentials();
        }
    }

    /// <summary>Salvataggio atomico come gli altri file dell'app.</summary>
    public void Save(JiraCredentials credentials)
    {
        byte[] data = protect(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(credentials)));
        string tmp = FilePath + ".tmp";
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(data);
            stream.Flush(flushToDisk: true);
        }
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(tmp, FilePath, overwrite: true);
    }
}
