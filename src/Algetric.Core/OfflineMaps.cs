namespace Algetric.Core;

public sealed record TileProvider(string Id, string Name, string UrlTemplate, int MinZoom,
    int MaxZoom, string Attribution, bool AllowsOffline)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Attribution)
            || !UrlTemplate.Contains("{z}") || !UrlTemplate.Contains("{x}") || !UrlTemplate.Contains("{y}")
            || !Uri.TryCreate(UrlTemplate.Replace("{z}", "0").Replace("{x}", "0").Replace("{y}", "0"), UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query)
            || MinZoom < 0 || MaxZoom > 22 || MinZoom > MaxZoom)
            throw new ArgumentException("Informe servidor HTTPS XYZ sem credenciais na URL, atribuição e zoom entre 0 e 22.");
    }
    public string TileUrl(TileAddress tile) => UrlTemplate.Replace("{z}", tile.Zoom.ToString())
        .Replace("{x}", tile.X.ToString()).Replace("{y}", tile.Y.ToString());
}
public readonly record struct TileAddress(int Zoom, int X, int Y);
public sealed record TileData(byte[] Bytes, string ContentType);
public sealed record OfflineRegion(Guid Id, string Name, string ProviderId, double West, double South,
    double East, double North, int MinZoom, int MaxZoom);
public sealed record DownloadProgress(int Completed, int Total, string Message);
public interface ITileStore
{
    Task<TileData?> GetAsync(string provider, TileAddress tile);
    Task PutAsync(string provider, TileAddress tile, byte[] bytes, string contentType, Guid? regionId = null);
    Task SaveRegionAsync(OfflineRegion region);
    Task<IReadOnlyList<OfflineRegion>> ListRegionsAsync();
    Task RemoveRegionAsync(Guid id);
}
public interface IRegionDownloadService
{
    Task DownloadAsync(TileProvider provider, OfflineRegion region, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}
public static class TileCoverage
{
    public const int MaximumTiles = 50_000;
    public static IEnumerable<TileAddress> Enumerate(OfflineRegion region)
    {
        Validate(region); var count = 0;
        for (var zoom = region.MinZoom; zoom <= region.MaxZoom; zoom++)
        {
            var west = X(region.West, zoom); var east = X(region.East, zoom);
            var north = Y(region.North, zoom); var south = Y(region.South, zoom);
            if ((long)(east - west + 1) * (south - north + 1) + count > MaximumTiles)
                throw new ArgumentException($"A região excede o limite de {MaximumTiles:N0} tiles. Reduza a área ou o zoom.");
            for (var x = west; x <= east; x++) for (var y = north; y <= south; y++)
                { count++; yield return new(zoom, x, y); }
        }
    }
    public static long EstimateBytes(OfflineRegion region) => Enumerate(region).LongCount() * 40_000L;
    private static int X(double longitude, int zoom) => Math.Clamp((int)Math.Floor((longitude + 180) / 360 * (1 << zoom)), 0, (1 << zoom) - 1);
    private static int Y(double latitude, int zoom)
    {
        var radians = latitude * Math.PI / 180;
        return Math.Clamp((int)Math.Floor((1 - Math.Asinh(Math.Tan(radians)) / Math.PI) / 2 * (1 << zoom)), 0, (1 << zoom) - 1);
    }
    private static void Validate(OfflineRegion r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.ProviderId)
            || !double.IsFinite(r.West) || !double.IsFinite(r.East) || !double.IsFinite(r.South) || !double.IsFinite(r.North)
            || r.West < -180 || r.East > 180 || r.West >= r.East || r.South < -85.05112878 || r.North > 85.05112878
            || r.South >= r.North || r.MinZoom < 0 || r.MaxZoom > 22 || r.MinZoom > r.MaxZoom)
            throw new ArgumentException("Região ou níveis de zoom inválidos; não cruze o antimeridiano.");
    }
}
