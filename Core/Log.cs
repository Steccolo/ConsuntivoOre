namespace ConsuntivoOre.Core;

public static class Log
{
    private static readonly object Gate = new();

    public static void Error(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DataStore.DataDirectory);
                File.AppendAllText(Path.Combine(DataStore.DataDirectory, "errori.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // il log non deve mai far cadere l'applicazione
        }
    }
}
