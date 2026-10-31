namespace ConsuntivoOre;

internal static class Program
{
    // Il nome del mutex è usato anche dall'installer (AppMutex) per rilevare l'app in esecuzione.
    private const string MutexName = "ConsuntivoOreMutex";
    private const string ShowEventName = "ConsuntivoOreShowEvent";

    [STAThread]
    private static void Main(string[] args)
    {
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
        Application.ThreadException += (_, e) => Log.Error(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error(e.ExceptionObject as Exception);

        Application.Run(new TrayApplicationContext(showEvent, openEntryOnStart: !startup));
        GC.KeepAlive(mutex);
    }
}
