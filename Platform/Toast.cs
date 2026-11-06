using System.Diagnostics;
using System.Globalization;
#if WINDOWS
using Microsoft.Toolkit.Uwp.Notifications;
#endif

namespace ConsuntivoOre.Platform;

/// <summary>Cosa fare quando si clicca una notifica o uno dei suoi pulsanti.</summary>
internal enum ToastAction { Entry, Snooze, Skip, Report, None }

/// <summary>Azione con l'eventuale giorno a cui si riferisce (la notifica può essere cliccata più tardi).</summary>
internal readonly record struct ToastCommand(ToastAction Action, DateOnly? Day = null);

/// <summary>
/// Notifiche di sistema con pulsanti. Su Windows sono toast (restano nel Centro notifiche e funzionano
/// anche dopo un riavvio dell'app); su Linux passano da notify-send.
/// Il clic arriva al gestore di <see cref="Initialize"/>, su un thread in background.
/// </summary>
internal static class Toast
{
    private static Action<ToastCommand>? _handler;

    /// <summary>Va chiamato all'avvio, una volta pronto il gestore: se l'app è stata avviata da un clic, quel clic arriva subito dopo.</summary>
    public static void Initialize(Action<ToastCommand> handler)
    {
        if (_handler is not null) return;
        _handler = handler;
        PlatformInitialize();
    }

    private static void Dispatch(ToastCommand command)
    {
        try
        {
            _handler?.Invoke(command);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private static ToastCommand Parse(string? action, string? day) => new(
        Enum.TryParse<ToastAction>(action, out var a) ? a : ToastAction.None,
        DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null);

#if WINDOWS
    private const string Group = "ConsuntivoOre";

    private static void PlatformInitialize()
    {
        try
        {
            ToastNotificationManagerCompat.OnActivated += e =>
            {
                var args = ToastArguments.Parse(e.Argument);
                args.TryGetValue("action", out string? action);
                args.TryGetValue("day", out string? day);
                Dispatch(Parse(action, day));
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    /// <summary>L'app è stata avviata da Windows per un clic su una notifica (era chiusa).</summary>
    public static bool WasLaunchedByClick
    {
        get
        {
            try
            {
                return ToastNotificationManagerCompat.WasCurrentProcessToastActivated();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }
    }

    /// <summary>Mostra la notifica; con lo stesso <paramref name="tag"/> sostituisce la precedente. Falso se Windows non la accetta.</summary>
    public static bool TryShow(string tag, string title, string text, ToastCommand onClick,
        params (string Label, ToastCommand Command)[] buttons)
    {
        try
        {
            var builder = new ToastContentBuilder();
            AddArguments(onClick, (k, v) => builder.AddArgument(k, v));
            builder.AddText(title).AddText(text);
            foreach (var (label, command) in buttons)
            {
                var button = new ToastButton().SetContent(label);
                AddArguments(command, (k, v) => button.AddArgument(k, v));
                builder.AddButton(button);
            }
            builder.Show(toast =>
            {
                toast.Tag = tag;
                toast.Group = Group;
            });
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return false;
        }
    }

    /// <summary>Toglie le notifiche ancora visibili con questo tag (es. promemoria già superato).</summary>
    public static void Remove(string tag)
    {
        try
        {
            ToastNotificationManagerCompat.History.Remove(tag, Group);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    /// <summary>Cancella registrazione e notifiche dell'app: usato dal disinstallatore.</summary>
    public static void Uninstall()
    {
        try
        {
            ToastNotificationManagerCompat.Uninstall();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private static void AddArguments(ToastCommand command, Action<string, string> add)
    {
        add("action", command.Action.ToString());
        if (command.Day is { } day) add("day", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
#else
    // notify-send (libnotify): con --wait resta in attesa e stampa la chiave dell'azione cliccata.
    private static readonly Dictionary<string, Process> Visible = [];

    private static void PlatformInitialize() { }

    public static bool WasLaunchedByClick => false;

    public static bool TryShow(string tag, string title, string text, ToastCommand onClick,
        params (string Label, ToastCommand Command)[] buttons)
    {
        try
        {
            var info = new ProcessStartInfo("notify-send") { RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add("--app-name=Consuntivo Ore");
            info.ArgumentList.Add("--wait");
            var commands = new Dictionary<string, ToastCommand> { ["default"] = onClick };
            info.ArgumentList.Add("--action=default=Apri");
            for (int i = 0; i < buttons.Length; i++)
            {
                commands[$"a{i}"] = buttons[i].Command;
                info.ArgumentList.Add($"--action=a{i}={buttons[i].Label}");
            }
            info.ArgumentList.Add(title);
            info.ArgumentList.Add(text);

            Remove(tag);
            var process = Process.Start(info);
            if (process is null) return false;
            lock (Visible) Visible[tag] = process;

            _ = Task.Run(async () =>
            {
                string key = (await process.StandardOutput.ReadToEndAsync()).Trim();
                await process.WaitForExitAsync();
                lock (Visible)
                {
                    if (Visible.TryGetValue(tag, out var p) && p == process) Visible.Remove(tag);
                }
                if (commands.TryGetValue(key, out var command)) Dispatch(command);
                process.Dispose();
            });
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return false;
        }
    }

    public static void Remove(string tag)
    {
        Process? process;
        lock (Visible)
        {
            if (!Visible.Remove(tag, out process)) return;
        }
        try
        {
            if (!process.HasExited) process.Kill();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    public static void Uninstall() { }
#endif
}
