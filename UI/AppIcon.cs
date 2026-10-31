namespace ConsuntivoOre.UI;

internal static class AppIcon
{
    private static Icon? _window;
    private static Icon? _tray;

    /// <summary>Icona per le finestre (tutte le risoluzioni).</summary>
    public static Icon Window => _window ??= Load(null);

    /// <summary>Icona piccola per l'area di notifica.</summary>
    public static Icon Tray => _tray ??= Load(SystemInformation.SmallIconSize);

    private static Icon Load(Size? size)
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("app.ico")!;
        return size is { } s ? new Icon(stream, s) : new Icon(stream);
    }
}
