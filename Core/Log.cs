namespace ConsuntivoOre.Core;

public static class Log
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object Gate = new();

    /// <summary>
    /// Di norma nella cartella dati reale; test e <c>--screenshot</c> la spostano in una cartella temporanea,
    /// così i loro errori finti non finiscono nel log dell'utente.
    /// </summary>
    public static string FilePath { get; set; } = Path.Combine(DataStore.DefaultDirectory, "errori.log");

    public static void Error(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                    File.Move(FilePath, FilePath + ".1", overwrite: true);
                File.AppendAllText(FilePath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // il log non deve mai far cadere l'applicazione
        }
    }
}
