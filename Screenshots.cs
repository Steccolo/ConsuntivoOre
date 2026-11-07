using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace ConsuntivoOre;

/// <summary>
/// Opzione per lo sviluppo: <c>ConsuntivoOre --screenshot cartella</c> disegna le finestre con dati di esempio
/// (in una cartella temporanea, mai quelli reali) e le salva come PNG, senza mostrarle.
/// </summary>
internal static class Screenshots
{
    public static int Run(string outputDirectory)
    {
        AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        Directory.CreateDirectory(outputDirectory);
        string data = Directory.CreateTempSubdirectory("consuntivo-screenshot").FullName;
        try
        {
            var store = new DataStore(data);
            Seed(store);
            string Out(string name) => Path.Combine(outputDirectory, name);

            // Jira finto: nessuna chiamata di rete, solo per vedere le parti dell'interfaccia.
            store.Settings.Jira = new JiraSettings { Enabled = true, SiteUrl = "https://azienda.atlassian.net", Email = "mario.rossi@azienda.it" };
            var creds = new CredentialStore(Path.Combine(data, "jira.dat"), b => b, b => b);
            creds.Save(new JiraCredentials { JiraToken = "token-di-prova", TempoToken = "token-di-prova" });
            using var jira = new JiraService(store, creds);

            var vm = new MainViewModel(store, jira);
            vm.Todo.SelectRow(1);
            var main = new MainWindow(vm);
            main.Show();
            Capture(main, Out("1-todo.png"));
            main.ShowTab(MainTab.Entry);
            vm.Entry.Selected = vm.Entry.Rows.ElementAtOrDefault(1); // voce collegata a Jira
            vm.Entry.EditCommand.Execute(null);
            Capture(main, Out("2-consuntiva.png"));
            main.Close();

            var report = new ReportWindow(new ReportViewModel(store, DateOnly.FromDateTime(DateTime.Today), jira));
            report.Show();
            report.DetailsGrid.SelectedIndex = 1;
            report.DetailsGrid.CurrentColumn = report.DetailsGrid.Columns[1];
            Capture(report, Out("3-report.png"));
            report.Close();

            var settings = new SettingsWindow(store, jira, () => { });
            settings.Show();
            Capture(settings, Out("4-impostazioni.png"));
            settings.Scroll.ScrollToEnd();
            Capture(settings, Out("4b-impostazioni-jira.png"));
            settings.Close();

            var dialog = new LogDialog(vm.Todo.Rows[1].Todo, 0);
            dialog.Show();
            Capture(dialog, Out("5-consuntiva-todo.png"));
            dialog.Close();

            var picker = new IssuePickerDialog((_, _) => Task.FromResult<IReadOnlyList<JiraIssueRef>>(SampleIssues));
            picker.Show();
            Dispatcher.UIThread.RunJobs();
            Capture(picker, Out("6-scegli-issue.png"));
            picker.Close();

            Console.WriteLine($"Immagini salvate in {outputDirectory}");
            return 0;
        }
        finally
        {
            Directory.Delete(data, recursive: true);
        }
    }

    private static void Capture(Window window, string path)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Nessun fotogramma");
#pragma warning disable CS0618 // basta il PNG con le opzioni predefinite
        frame.Save(path);
#pragma warning restore CS0618
    }

    private static readonly JiraIssueRef[] SampleIssues =
    [
        new() { Id = 10001, Key = "GEST-42", Summary = "Analisi requisiti nuovo gestionale", ProjectName = "Gestionale Rossi" },
        new() { Id = 10002, Key = "GEST-57", Summary = "Migrazione anagrafiche", ProjectName = "Gestionale Rossi" },
        new() { Id = 10003, Key = "INT-8", Summary = "Riunioni e attività interne", ProjectName = "Interno" },
    ];

    private static void Seed(DataStore store)
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

        var sent = new TimeEntry { Date = today, Hours = 3, Client = "Rossi Srl", Note = "Analisi requisiti del nuovo gestionale", Jira = SampleIssues[0] };
        store.Add(sent);
        store.SetTempoSync(sent.Id, TempoSync.Of(sent, 1, DateTime.Now));
        store.Add(new TimeEntry { Date = today, Hours = 2.5m, Client = "Interno", Note = "Riunione di reparto\ne aggiornamento procedure", Jira = SampleIssues[2] });
        var monday = Fmt.WeekStart(today);
        if (monday < today)
            store.Add(new TimeEntry { Date = monday, Hours = 8, Client = "Acme", Note = "Migrazione server di posta" });
    }
}
