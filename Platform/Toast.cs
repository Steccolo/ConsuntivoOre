using System.Globalization;
using Microsoft.Toolkit.Uwp.Notifications;

namespace ConsuntivoOre.Platform;

/// <summary>Cosa fare quando si clicca una notifica o uno dei suoi pulsanti.</summary>
internal enum ToastAction { Entry, Snooze, Skip, Report, None }

/// <summary>Azione con l'eventuale giorno a cui si riferisce (la notifica può essere cliccata più tardi).</summary>
internal readonly record struct ToastCommand(ToastAction Action, DateOnly? Day = null);

/// <summary>
/// Notifiche di Windows 10/11 con pulsanti (toast). Il clic arriva al gestore di <see cref="Initialize"/>,
/// anche quando la notifica è nel Centro notifiche e l'app nel frattempo è stata riavviata.
/// </summary>
internal static class Toast
{
    private const string Group = "ConsuntivoOre";

    private static Action<ToastCommand>? _handler;

    /// <summary>
    /// Collega i clic alle azioni dell'app. Va chiamato all'avvio, una volta pronto il gestore:
    /// se l'app è stata avviata da un clic, quel clic arriva subito dopo. Il gestore è chiamato
    /// su un thread in background e va riportato sul thread dell'interfaccia.
    /// </summary>
    public static void Initialize(Action<ToastCommand> handler)
    {
        if (_handler is not null) return;
        _handler = handler;
        try
        {
            ToastNotificationManagerCompat.OnActivated += e =>
            {
                try
                {
                    _handler?.Invoke(Parse(e.Argument));
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
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

    /// <summary>
    /// Mostra la notifica; con lo stesso <paramref name="tag"/> sostituisce la precedente.
    /// Falso se Windows non la accetta: in quel caso si usa la notifica dell'area di notifica.
    /// </summary>
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

    private static ToastCommand Parse(string argument)
    {
        var args = ToastArguments.Parse(argument);
        var action = args.TryGetValue("action", out string? a) && Enum.TryParse<ToastAction>(a, out var parsed)
            ? parsed : ToastAction.None;
        DateOnly? day = args.TryGetValue("day", out string? d) &&
                        DateOnly.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDay)
            ? parsedDay : null;
        return new ToastCommand(action, day);
    }
}
