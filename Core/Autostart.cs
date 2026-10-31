using Microsoft.Win32;

namespace ConsuntivoOre.Core;

/// <summary>Avvio con Windows tramite HKCU\...\Run (stessa voce creata dall'installer).</summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ConsuntivoOre";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --startup");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
