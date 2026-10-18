using System.Net;
using System.Net.Sockets;
using System.Text;
using Algetric.Core;

namespace Algetric.Infrastructure;

public sealed class LocalMapServer : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource stopping = new();
    private readonly string root;
    private readonly string secret = Guid.NewGuid().ToString("N");
    private readonly ITileStore store;
    private readonly HttpClient client;
    public TileProvider? Provider { get; set; }
    public Func<Task<string>>? GetStateAsync { get; set; }
    public Func<string, Task<string>>? HandleMessageAsync { get; set; }
    public Uri BaseUri { get; }
    public event Action<string>? Diagnostic;
    public LocalMapServer(string root, ITileStore store, HttpClient client)
    {
        this.root = Path.GetFullPath(root); this.store = store; this.client = client;
        var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        BaseUri = new($"http://127.0.0.1:{port}/{secret}/"); listener.Start(); _ = ListenAsync();
    }
    private async Task ListenAsync()
    {
        try
        {
            while (!stopping.IsCancellationRequested)
            { var context = await listener.GetContextAsync(); _ = RespondAsync(context); }
        }
        catch (Exception error) when (error is HttpListenerException or ObjectDisposedException) { }
    }
    private async Task RespondAsync(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url!.AbsolutePath;
            if (!path.StartsWith($"/{secret}/", StringComparison.Ordinal)) { context.Response.StatusCode = 404; return; }
            if (context.Request.Headers["Origin"] is { } origin && origin != BaseUri.GetLeftPart(UriPartial.Authority))
            { context.Response.StatusCode = 403; return; }
            path = Uri.UnescapeDataString(path[($"/{secret}/".Length)..]);
            if (path == "state" && GetStateAsync is not null)
            { await TextAsync(context, await GetStateAsync()); return; }
            if (path == "bridge" && context.Request.HttpMethod == "POST" && HandleMessageAsync is not null)
            {
                if (context.Request.ContentLength64 < 0 || context.Request.ContentLength64 > 1_000_000) { context.Response.StatusCode = 413; return; }
                using var reader = new StreamReader(context.Request.InputStream);
                await TextAsync(context, await HandleMessageAsync(await reader.ReadToEndAsync())); return;
            }
            if (path.StartsWith("tiles/", StringComparison.Ordinal)) { await TileAsync(context, path); return; }
            var file = Path.GetFullPath(Path.Combine(root, path.Length == 0 ? "index.html" : path));
            if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(file))
            { context.Response.StatusCode = 404; return; }
            context.Response.ContentType = Path.GetExtension(file) switch
            { ".js" or ".mjs" => "text/javascript", ".css" => "text/css", ".html" => "text/html", ".json" => "application/json", ".png" => "image/png", ".jpg" => "image/jpeg", ".svg" => "image/svg+xml", ".wasm" => "application/wasm", _ => "application/octet-stream" };
            await using var stream = File.OpenRead(file); context.Response.ContentLength64 = stream.Length;
            await stream.CopyToAsync(context.Response.OutputStream, stopping.Token);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Diagnostic?.Invoke(error is IOException ? "Falha de leitura ou armazenamento de mapas." : "Mapa base indisponível; confira conexão e configuração.");
            try { context.Response.StatusCode = 503; } catch (ObjectDisposedException) { }
        }
        finally { context.Response.Close(); }
    }
    private async Task TileAsync(HttpListenerContext context, string path)
    {
        var parts = path.Split('/');
        if (parts.Length != 5 || Provider is not { } provider || parts[1] != provider.Id
            || !int.TryParse(parts[2], out var z) || !int.TryParse(parts[3], out var x) || !int.TryParse(parts[4], out var y)
            || z < provider.MinZoom || z > provider.MaxZoom || x < 0 || y < 0 || x >= (1 << z) || y >= (1 << z))
        { context.Response.StatusCode = 404; return; }
        var address = new TileAddress(z, x, y);
        var data = await store.GetAsync(provider.Id, address);
        if (data is null)
        {
            // Online viewing does not persist tiles unless offline storage was explicitly permitted.
            data = await new RegionDownloadService(store, client).FetchAsync(provider, address, stopping.Token);
        }
        context.Response.ContentType = data.ContentType; context.Response.ContentLength64 = data.Bytes.Length;
        await context.Response.OutputStream.WriteAsync(data.Bytes, stopping.Token);
    }
    private static async Task TextAsync(HttpListenerContext context, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text); context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length; await context.Response.OutputStream.WriteAsync(bytes);
    }
    public void Dispose() { stopping.Cancel(); listener.Close(); stopping.Dispose(); }
}
