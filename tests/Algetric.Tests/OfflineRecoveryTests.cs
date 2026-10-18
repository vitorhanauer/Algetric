using Algetric.Core;
using Algetric.Infrastructure;
using System.Net;
using Xunit;
namespace Algetric.Tests;

public class OfflineRecoveryTests
{
    [Fact]
    public async Task InterruptedDownloadResumesAfterReopeningWithoutRefetchingSavedTile()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".sqlite");
        try
        {
            var store = new SqliteTileStore(path); using var cancellation = new CancellationTokenSource();
            using var firstClient = new HttpClient(new InterruptHandler());
            var provider = new TileProvider("test", "Test", "https://example.com/{z}/{x}/{y}", 0, 18, "Test", true);
            var region = new OfflineRegion(Guid.NewGuid(), "Test", "test", 0, 0, 1, 1, 0, 1);
            var downloader = new RegionDownloadService(store, firstClient);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloader.DownloadAsync(provider, region,
                new ImmediateProgress(_ => cancellation.Cancel()), cancellation.Token));
            var addresses = TileCoverage.Enumerate(region).ToArray();
            var saved = 0; foreach (var tile in addresses) if (await store.GetAsync("test", tile) is not null) saved++;
            Assert.Equal(1, saved);
            var resumedHandler = new CountingHandler(); using var resumedClient = new HttpClient(resumedHandler);
            var reopened = new SqliteTileStore(path);
            await new RegionDownloadService(reopened, resumedClient).DownloadAsync(provider, region, null, CancellationToken.None);
            Assert.Equal(addresses.Length - saved, resumedHandler.Requests);
            foreach (var tile in addresses) Assert.NotNull(await reopened.GetAsync("test", tile));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }
    [Fact]
    public async Task LoopbackTileEndpointServesCacheWithoutNetworkAndRejectsForeignOrigin()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(directory);
        try
        {
            var store = new SqliteTileStore(Path.Combine(directory, "cache.sqlite"));
            await store.PutAsync("test", new(0, 0, 0), [1, 2, 3], "image/png");
            using var unreachableClient = new HttpClient(new FailingHandler());
            using var server = new LocalMapServer(directory, store, unreachableClient)
            { Provider = new("test", "Test", "https://example.com/{z}/{x}/{y}", 0, 18, "Test", true) };
            using var client = new HttpClient(); var url = new Uri(server.BaseUri, "tiles/test/0/0/0");
            Assert.Equal(new byte[] { 1, 2, 3 }, await client.GetByteArrayAsync(url));
            using var request = new HttpRequestMessage(HttpMethod.Get, url); request.Headers.Add("Origin", "https://foreign.example");
            using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    }
    private sealed class ImmediateProgress(Action<DownloadProgress> callback) : IProgress<DownloadProgress>
    { public void Report(DownloadProgress value) => callback(value); }
    private sealed class InterruptHandler : HttpMessageHandler
    {
        private int calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { if (Interlocked.Increment(ref calls) > 1) await Task.Delay(Timeout.Infinite, token); return TileResponse(); }
    }
    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Interlocked.Increment(ref Requests); return Task.FromResult(TileResponse()); }
    }
    private sealed class FailingHandler : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => throw new HttpRequestException("offline"); }
    private static HttpResponseMessage TileResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        response.Content.Headers.ContentType = new("image/png"); return response;
    }
}
