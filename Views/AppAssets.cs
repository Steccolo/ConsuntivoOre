using Avalonia.Controls;
using Avalonia.Platform;

namespace ConsuntivoOre.Views;

public static class AppAssets
{
    private static WindowIcon? _icon;

    /// <summary>L'orologio dell'app, per finestre e area di notifica.</summary>
    public static WindowIcon Icon => _icon ??= new WindowIcon(AssetLoader.Open(new Uri("avares://ConsuntivoOre/Assets/app.ico")));
}
