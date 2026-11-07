#if WINDOWS
using System.Security.Cryptography;
#endif

namespace ConsuntivoOre.Platform;

/// <summary>
/// Protezione dei token salvati su disco. Windows: DPAPI legata all'utente (il file copiato su un altro PC
/// o letto da un altro utente non si decifra). Linux: file leggibile solo dall'utente, senza cifratura.
/// </summary>
internal static class Secrets
{
#if WINDOWS
    private static readonly byte[] Entropy = "ConsuntivoOre.Jira"u8.ToArray();

    public static byte[] Protect(byte[] data) => ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
    public static byte[] Unprotect(byte[] data) => ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
#else
    public static byte[] Protect(byte[] data) => data;
    public static byte[] Unprotect(byte[] data) => data;
#endif

    public static CredentialStore For(DataStore store) =>
        new(Path.Combine(store.DataDirectory, "jira.dat"), Protect, Unprotect);
}
