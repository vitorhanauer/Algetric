using Algetric.Infrastructure;
using Xunit;
namespace Algetric.Tests;

public class SecretStoreTests
{
    [Fact]
    public async Task PublicProvidersDoNotRequireSecretToolButStoringTokensDoes()
    {
        var store = new LinuxSecretStore("/nonexistent/algetric-secret-tool");
        Assert.Null(await store.GetAsync("provider"));
        await Assert.ThrowsAsync<IOException>(() => store.SetAsync("provider", "example"));
    }
}
