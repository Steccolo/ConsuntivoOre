namespace ConsuntivoOre;

internal static class Program
{
    // Il nome del mutex è usato anche dall'installer (AppMutex) per rilevare l'app in esecuzione.
    private const string MutexName = "ConsuntivoOreMutex";
    private const string ShowEventName = "ConsuntivoOreShowEvent";

    [STAThread]
    private static void Main(string[] args)
    {
        // Chiamato dal disinstallatore: toglie le notifiche e la loro registrazione in Windows.
        if (args.Contains("--uninstall-notifications", StringComparer.OrdinalIgnoreCase))
        {
            Toast.Uninstall();
            return;
        }

        bool startup = args.Contains("--startup", StringComparer.OrdinalIgnoreCase);

        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // Già in esecuzione: se avviata a mano chiede all'istanza attiva di aprire la finestra.
            if (!startup && EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
            {
                existing.Set();
                existing.Dispose();
            }
            return;
        }

        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => ReportError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportError(e.ExceptionObject as Exception);

        DataStore store;
        try
        {
            store = new DataStore();
        }
        catch (Exception ex) when (ex is DataStoreException || DataStore.IsFileError(ex))
        {
            Log.Error(ex);
            MessageBox.Show($"{ex.Message}\n\nL'app verrà chiusa per non alterare i dati.", "Consuntivo Ore",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (store.LoadProblems.Count > 0)
        {
            MessageBox.Show(
                string.Join("\n\n", store.LoadProblems) +
                $"\n\nL'app riparte senza quei dati. I backup degli ultimi giorni sono in:\n{store.BackupDirectory}",
                "Consuntivo Ore", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        Application.Run(new TrayApplicationContext(store, showEvent));
        GC.KeepAlive(mutex);
    }

    private static void ReportError(Exception? ex)
    {
        Log.Error(ex);
        MessageBox.Show($"Si è verificato un errore:\n{ex?.Message}\n\nDettagli in: {Log.FilePath}", "Consuntivo Ore",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
