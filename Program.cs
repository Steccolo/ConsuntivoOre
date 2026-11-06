using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace ConsuntivoOre;

internal static class Program
{
    public static SingleInstance? Instance { get; private set; }

    /// <summary>Avviato con --screenshot: niente area di notifica né dati reali.</summary>
    public static bool ScreenshotMode { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        // Date e numeri in italiano anche su un sistema in un'altra lingua.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = Fmt.It;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = Fmt.It;

        // Chiamato dal disinstallatore: toglie le notifiche e la loro registrazione in Windows.
        if (args.Contains("--uninstall-notifications", StringComparer.OrdinalIgnoreCase))
        {
            Toast.Uninstall();
            return 0;
        }

        int shot = Array.IndexOf(args, "--screenshot");
        if (shot >= 0 && shot + 1 < args.Length)
        {
            ScreenshotMode = true;
            return Screenshots.Run(args[shot + 1]);
        }

        bool startup = args.Contains("--startup", StringComparer.OrdinalIgnoreCase);
        Instance = SingleInstance.TryAcquire();
        if (Instance is null)
        {
            // Già in esecuzione: se avviata a mano chiede all'istanza attiva di aprire la finestra.
            if (!startup) SingleInstance.SignalShow();
            return 0;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error(e.ExceptionObject as Exception);
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Log.Error(e.Exception);
            e.Handled = true;
            _ = Views.MessageDialog.Show(null,
                $"Si è verificato un errore:\n{e.Exception.Message}\n\nDettagli in: {Log.FilePath}", error: true);
        };

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        }
        finally
        {
            Instance.Dispose();
        }
    }

    // Usato anche dall'anteprima XAML degli IDE.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
