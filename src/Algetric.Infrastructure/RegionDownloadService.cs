using System.Net;
using System.Net.Http.Headers;
using Algetric.Core;

namespace Algetric.Infrastructure;

public sealed class RegionDownloadService(ITileStore store, HttpClient client,
    Func<long, bool>? hasSpace = null) : IRegionDownloadService
{
    public async Task DownloadAsync(TileProvider provider, OfflineRegion region,
        IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        provider.Validate();
        if (!provider.AllowsOffline) throw new InvalidOperationException("O provedor não permite downloads offline.");
        if (region.ProviderId != provider.Id || region.MinZoom < provider.MinZoom || region.MaxZoom > provider.MaxZoom)
            throw new ArgumentException("Região incompatível com o provedor.");
        var tiles = TileCoverage.Enumerate(region).ToArray();
        if (hasSpace is not null && !hasSpace(tiles.Length * 40_000L + 10_000_000))
            throw new IOException("Espaço insuficiente para o download estimado.");
        await store.SaveRegionAsync(region);
        var completed = 0;
        await Parallel.ForEachAsync(tiles, new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = cancellationToken },
            async (tile, token) =>
            {
                var data = await store.GetAsync(provider.Id, tile) ?? await FetchAsync(provider, tile, token);
                token.ThrowIfCancellationRequested();
                if (hasSpace is not null && !hasSpace(data.Bytes.Length + 1_000_000)) throw new IOException("Espaço em disco insuficiente.");
                await store.PutAsync(provider.Id, tile, data.Bytes, data.ContentType, region.Id);
                var count = Interlocked.Increment(ref completed);
                progress?.Report(new(count, tiles.Length, $"{count}/{tiles.Length} tiles"));
            });
    }
    public async Task<TileData> FetchAsync(TileProvider provider, TileAddress tile, CancellationToken token)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var response = await client.GetAsync(provider.TileUrl(tile), HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            {
                if (attempt == 3) response.EnsureSuccessStatusCode();
                var wait = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
                await Task.Delay(wait < TimeSpan.Zero ? TimeSpan.Zero : wait, token); continue;
            }
            response.EnsureSuccessStatusCode();
            var type = response.Content.Headers.ContentType?.MediaType ?? "";
            if (type is not ("image/png" or "image/jpeg" or "image/webp")) throw new InvalidDataException("O servidor não retornou um tile raster.");
            if (response.Content.Headers.ContentLength > 5_000_000) throw new InvalidDataException("Tile excede 5 MB.");
            await using var source = await response.Content.ReadAsStreamAsync(token);
            using var buffer = new MemoryStream(); var chunk = new byte[8192]; int length;
            while ((length = await source.ReadAsync(chunk, token)) > 0)
            {
                if (buffer.Length + length > 5_000_000) throw new InvalidDataException("Tile excede 5 MB.");
                buffer.Write(chunk, 0, length);
            }
            if (buffer.Length == 0) throw new InvalidDataException("Tile vazio.");
            return new(buffer.ToArray(), type);
        }
        throw new HttpRequestException("Falha ao obter tile.");
    }
}
