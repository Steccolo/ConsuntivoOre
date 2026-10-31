using Microsoft.Win32;

namespace ConsuntivoOre.Platform;

/// <summary>Avvio con Windows tramite HKCU\...\Run (stessa voce creata dall'installer).</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ConsuntivoOre";

    private static string Command => $"\"{Environment.ProcessPath}\" --startup";

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
}
