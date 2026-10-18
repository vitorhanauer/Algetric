using Algetric.Core;
using Algetric.Infrastructure;
using System.Net;
using Xunit;

namespace Algetric.Tests;

public class OfflineTests
{
    [Fact]
    public void RegionEnumeratesXyzTilesAndRejectsUnboundedDownloads()
    {
        var region = new OfflineRegion(Guid.NewGuid(), "Centro", "test", -46.7, -23.6, -46.6, -23.5, 10, 12);
        var tiles = TileCoverage.Enumerate(region).ToArray();
        Assert.NotEmpty(tiles); Assert.Equal(tiles.Length, tiles.Distinct().Count());
        Assert.Throws<ArgumentException>(() => TileCoverage.Enumerate(region with { West = 180, East = -180 }).ToArray());
    }

    [Fact]
    public async Task CachedTilesSurviveRestartAndRegionsCanBeRemoved()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".sqlite");
        try
        {
            var region = new OfflineRegion(Guid.NewGuid(), "Local", "test", 0, 0, 1, 1, 0, 0);
            var store = new SqliteTileStore(path);
            await store.SaveRegionAsync(region);
            await store.PutAsync("test", new(0, 0, 0), [1, 2, 3], "image/png", region.Id);
            var reopened = new SqliteTileStore(path);
            Assert.Equal(new byte[] { 1, 2, 3 }, (await reopened.GetAsync("test", new(0, 0, 0)))!.Bytes);
            Assert.Single(await reopened.ListRegionsAsync());
            await reopened.RemoveRegionAsync(region.Id);
            Assert.Null(await reopened.GetAsync("test", new(0, 0, 0)));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [Fact]
    public async Task DownloaderRetriesRateLimitAndResumesWithoutFetchingCachedTiles()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".sqlite");
        try
        {
            var handler = new RateLimitHandler();
            var store = new SqliteTileStore(path);
            var downloader = new RegionDownloadService(store, new HttpClient(handler));
            var provider = new TileProvider("test", "Teste", "https://tiles.example/{z}/{x}/{y}.png", 0, 18, "Local", true);
            var region = new OfflineRegion(Guid.NewGuid(), "Local", "test", 0, 0, 1, 1, 0, 0);
            await downloader.DownloadAsync(provider, region, null, CancellationToken.None);
            Assert.Equal(2, handler.Requests);
            await downloader.DownloadAsync(provider, region, null, CancellationToken.None);
            Assert.Equal(2, handler.Requests);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    private sealed class RateLimitHandler : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var response = new HttpResponseMessage(Requests == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK);
            if (Requests == 1) response.Headers.RetryAfter = new(TimeSpan.Zero);
            else { response.Content = new ByteArrayContent([1, 2, 3]); response.Content.Headers.ContentType = new("image/png"); }
            return Task.FromResult(response);
        }
    }
}
