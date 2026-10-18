using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Algetric.Infrastructure;

public interface IProviderSecretStore
{
    Task<string?> GetAsync(string provider);
    Task SetAsync(string provider, string secret);
}

public static class ProviderSecretStore
{
    public static IProviderSecretStore Create() => OperatingSystem.IsWindows()
        ? new WindowsSecretStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "algetric", "credentials"))
        : new LinuxSecretStore();
}

[SupportedOSPlatform("windows")]
public sealed class WindowsSecretStore(string directory) : IProviderSecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Algetric map provider credentials v1");
    public async Task<string?> GetAsync(string provider)
    {
        var path = CredentialPath(provider);
        if (!File.Exists(path)) return null;
        var bytes = ProtectedData.Unprotect(await File.ReadAllBytesAsync(path), Entropy, DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public async Task SetAsync(string provider, string secret)
    {
        Directory.CreateDirectory(directory);
        var clear = Encoding.UTF8.GetBytes(secret);
        byte[] encrypted;
        try { encrypted = ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(clear); }
        var path = CredentialPath(provider);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllBytesAsync(temporary, encrypted); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private string CredentialPath(string provider) => Path.Combine(directory,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(provider))) + ".dpapi");
}
