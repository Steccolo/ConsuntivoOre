using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using ConsuntivoOre.Core;

namespace TodoAvalonia;

internal static class Program
{
    /// <summary>Cartella separata da quella dell'app vera: il prototipo non tocca i dati reali.</summary>
    public static readonly string DataDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ConsuntivoOre-Prototipo");

    [STAThread]
    public static int Main(string[] args)
    {
        // Date e numeri in italiano anche sui PC con Windows in un'altra lingua (come l'app vera, tramite Fmt).
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = Fmt.It;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = Fmt.It;
        System.Globalization.CultureInfo.CurrentCulture = Fmt.It;
        System.Globalization.CultureInfo.CurrentUICulture = Fmt.It;

        int i = Array.IndexOf(args, "--screenshot");
        if (i >= 0 && i + 1 < args.Length) return Screenshot(args[i + 1]);

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();

    public static DataStore OpenStore(string? directory = null)
    {
        var store = new DataStore(directory ?? DataDirectory);
        if (store.Todos().Count == 0) DemoData.Seed(store);
        return store;
    }

    /// <summary>Disegna la finestra senza mostrarla e la salva come immagine (per controllare la grafica).</summary>
    private static int Screenshot(string path)
    {
        AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var dir = Directory.CreateTempSubdirectory("todo-prototipo").FullName;
        var vm = new MainViewModel(OpenStore(dir));
        vm.Todo.SelectRow(1);
        var window = new MainWindow(vm) { Width = 900, Height = 680 };
        window.Show();
        Capture(window, path);

        // La scheda di consuntivazione, con una voce in modifica.
        window.ShowTab(1);
        vm.Entry.Selected = vm.Entry.Rows.FirstOrDefault();
        vm.Entry.EditCommand.Execute(null);
        string entryPath = Path.ChangeExtension(path, null) + "-ore.png";
        Capture(window, entryPath);
        window.Close();

        // Anche la finestra "Consuntiva…", sul todo selezionato.
        string dialogPath = Path.ChangeExtension(path, null) + "-consuntiva.png";
        var dialog = new LogDialog(vm.Todo.Rows[1].Todo, 0);
        dialog.Show();
        Capture(dialog, dialogPath);
        dialog.Close();

        Directory.Delete(dir, recursive: true);
        Console.WriteLine($"Immagini salvate in {path}, {entryPath} e {dialogPath}");
        return 0;
    }

    private static void Capture(Avalonia.Controls.Window window, string path)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Nessun fotogramma");
#pragma warning disable CS0618 // basta il PNG con le opzioni predefinite
        frame.Save(path);
#pragma warning restore CS0618
    }
}

internal static class DemoData
{
    public static void Seed(DataStore store)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        store.AddTodo(new TodoItem { Name = "Acme", Description = "Revisione contratto di manutenzione", PlannedHours = 2, DueDate = today.AddDays(-2) });
        store.AddTodo(new TodoItem { Name = "Rossi Srl", Description = "Preparare offerta per il nuovo gestionale", PlannedHours = 4, DueDate = today });
        store.AddTodo(new TodoItem { Name = "Interno", Description = "Aggiornare la documentazione del server", DueDate = today.AddDays(5) });
        store.AddTodo(new TodoItem { Name = "Bianchi", Description = "Chiamare per il rinnovo licenze" });
        var done = new TodoItem { Name = "Acme", Description = "Installazione certificati SSL", PlannedHours = 1.5m };
        store.AddTodo(done);
        store.Add(new TimeEntry { Date = today, Hours = 1.5m, Client = done.Name, Note = done.Description, TodoId = done.Id });
        store.CompleteTodo(done.Id);

        // Qualche ora già registrata oggi e ieri, per la scheda di consuntivazione.
        store.Add(new TimeEntry { Date = today, Hours = 3, Client = "Rossi Srl", Note = "Analisi requisiti del nuovo gestionale" });
        store.Add(new TimeEntry { Date = today, Hours = 2.5m, Client = "Interno", Note = "Riunione di reparto e aggiornamento procedure" });
        store.Add(new TimeEntry { Date = today.AddDays(-1), Hours = 8, Client = "Acme", Note = "Migrazione server di posta" });
    }
}
