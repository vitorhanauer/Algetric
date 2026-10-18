using System.Security.Cryptography;
using Algetric.Infrastructure;
using Xunit;

namespace Algetric.Tests;

public sealed class WindowsPlatformTests
{
    [WindowsFact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task WindowsCredentialsRoundTripWithoutStoringPlainText()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var store = new WindowsSecretStore(directory);
            Assert.Null(await store.GetAsync("test"));
            await store.SetAsync("test", "private-token-value");
            Assert.Equal("private-token-value", await store.GetAsync("test"));
            Assert.DoesNotContain("private-token-value", System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Assert.Single(Directory.GetFiles(directory)))));
            await File.WriteAllBytesAsync(Assert.Single(Directory.GetFiles(directory)), [1, 2, 3]);
            await Assert.ThrowsAsync<CryptographicException>(() => store.GetAsync("test"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}

public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute() { if (!OperatingSystem.IsWindows()) Skip = "Requires Windows DPAPI."; }
}
