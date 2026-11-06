using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ConsuntivoOre;

public partial class App : Application
{
    private AppController? _controller;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        if (Program.ScreenshotMode || ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

        // App da area di notifica: chiudere le finestre non la termina.
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _ = StartAsync(desktop);
    }

    private async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        DataStore store;
        try
        {
            store = new DataStore();
        }
        catch (Exception ex) when (ex is DataStoreException || DataStore.IsFileError(ex))
        {
            Log.Error(ex);
            await MessageDialog.Show(null, $"{ex.Message}\n\nL'app verrà chiusa per non alterare i dati.", error: true);
            desktop.Shutdown();
            return;
        }

        if (store.LoadProblems.Count > 0)
        {
            await MessageDialog.Show(null,
                string.Join("\n\n", store.LoadProblems) +
                $"\n\nL'app riparte senza quei dati. I backup degli ultimi giorni sono in:\n{store.BackupDirectory}");
        }

        _controller = new AppController(this, desktop, store);
    }
}
