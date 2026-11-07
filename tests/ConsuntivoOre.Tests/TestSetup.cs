using System.Runtime.CompilerServices;
using ConsuntivoOre.Core;

namespace ConsuntivoOre.Tests;

internal static class TestSetup
{
    /// <summary>Prima di qualsiasi test: gli errori provocati apposta non vanno nel log reale dell'utente.</summary>
    [ModuleInitializer]
    internal static void RedirectLog() =>
        Log.FilePath = Path.Combine(Path.GetTempPath(), "consuntivo-test-log", "errori.log");
}
