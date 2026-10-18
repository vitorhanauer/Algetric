using System.Net;
using Algetric.Infrastructure;
using Xunit;

namespace Algetric.Tests;

public sealed class LoopbackServerTests
{
    [Fact]
    public async Task ServesLocalAssetsAndRoutesBridgeMessagesOnAnEphemeralLoopbackPort()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "index.html"), "local globe");
            using var client = new HttpClient();
            var store = new SqliteTileStore(Path.Combine(directory, "tiles.sqlite"));
            using var server = new LocalMapServer(directory, store, client)
            { GetStateAsync = () => Task.FromResult("{\"ready\":true}"), HandleMessageAsync = body => Task.FromResult(body) };
            Assert.True(IPAddress.IsLoopback(IPAddress.Parse(server.BaseUri.Host)));
            Assert.NotEqual(0, server.BaseUri.Port);
            Assert.Equal("local globe", await client.GetStringAsync(new Uri(server.BaseUri, "index.html")));
            Assert.Equal("{\"ready\":true}", await client.GetStringAsync(new Uri(server.BaseUri, "state")));
            using var response = await client.PostAsync(new Uri(server.BaseUri, "bridge"), new StringContent("{\"action\":\"select\"}"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("{\"action\":\"select\"}", await response.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(new Uri(server.BaseUri, "../index.html"))).StatusCode);
            var outside = new Uri(server.BaseUri, "..%5Coutside.txt");
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(outside)).StatusCode);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    }
}
