#if WINDOWS
using Microsoft.Win32;
#endif

namespace ConsuntivoOre.Platform;

/// <summary>
/// Avvio automatico con il sistema. Windows: HKCU\...\Run (stessa voce creata dall'installer).
/// Linux: file .desktop in ~/.config/autostart.
/// </summary>
internal static class Autostart
{
    private static string ExePath => Environment.ProcessPath ?? "";

#if WINDOWS
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ConsuntivoOre";

    private static string Command => $"\"{ExePath}\" --startup";

    /// <summary>Vero solo se la voce punta a questo eseguibile: un percorso vecchio non vale.</summary>
    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void SetEnabled(bool enabled)
    {
#if !DEBUG
        // Una build di sviluppo non deve toccare la voce dell'app installata.
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
            key.SetValue(ValueName, Command);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
#endif
    }
#else
    private static string DesktopFile => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } config
            ? config
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
        "autostart", "consuntivo-ore.desktop");

    private static string ExecLine => $"Exec=\"{ExePath}\" --startup";

    public static bool IsEnabled =>
        File.Exists(DesktopFile) && File.ReadAllLines(DesktopFile).Contains(ExecLine);

    public static void SetEnabled(bool enabled)
    {
#if !DEBUG
        if (!enabled)
        {
            File.Delete(DesktopFile);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(DesktopFile)!);
        File.WriteAllLines(DesktopFile,
        [
            "[Desktop Entry]",
            "Type=Application",
            "Name=Consuntivo Ore",
            "Comment=Promemoria e consuntivazione delle ore di lavoro",
            ExecLine,
            "X-GNOME-Autostart-enabled=true",
        ]);
#endif
    }
#endif
}
