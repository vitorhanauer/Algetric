using System.Net;
using Algetric.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Algetric.Infrastructure;

public sealed class LocalMapServer : IDisposable
{
    private readonly WebApplication application;
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
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [], ContentRootPath = this.root });
        builder.Logging.ClearProviders();
        // Kestrel binds a user-owned socket on Windows without HTTP.sys URL ACLs.
        builder.WebHost.UseKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
            options.Limits.MaxRequestBodySize = 1_000_000;
        });
        application = builder.Build(); application.Run(RespondAsync);
        Task.Run(() => application.StartAsync()).GetAwaiter().GetResult();
        var address = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        BaseUri = new($"{address}/{secret}/");
    }
    private async Task RespondAsync(HttpContext context)
    {
        try { await RouteAsync(context); }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Diagnostic?.Invoke(error is IOException ? "Falha de leitura ou armazenamento de mapas." : "Mapa base indisponível; confira conexão e configuração.");
            if (!context.Response.HasStarted) context.Response.StatusCode = 503;
        }
    }
    private async Task RouteAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith($"/{secret}/", StringComparison.Ordinal)) { context.Response.StatusCode = 404; return; }
        var origin = context.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && origin != BaseUri.GetLeftPart(UriPartial.Authority)) { context.Response.StatusCode = 403; return; }
        path = Uri.UnescapeDataString(path[($"/{secret}/".Length)..]);
        if (path == "state" && GetStateAsync is not null) { await TextAsync(context, await GetStateAsync()); return; }
        if (path == "bridge" && HttpMethods.IsPost(context.Request.Method) && HandleMessageAsync is not null)
        {
            if (context.Request.ContentLength is null or < 0 or > 1_000_000) { context.Response.StatusCode = 413; return; }
            using var reader = new StreamReader(context.Request.Body);
            await TextAsync(context, await HandleMessageAsync(await reader.ReadToEndAsync())); return;
        }
        if (path.StartsWith("tiles/", StringComparison.Ordinal)) { await TileAsync(context, path); return; }
        await StaticFileAsync(context, path);
    }
    private async Task StaticFileAsync(HttpContext context, string path)
    {
        var file = Path.GetFullPath(Path.Combine(root, path.Length == 0 ? "index.html" : path));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!file.StartsWith(root + Path.DirectorySeparatorChar, comparison) || !File.Exists(file))
        { context.Response.StatusCode = 404; return; }
        context.Response.ContentType = Path.GetExtension(file) switch
        { ".js" or ".mjs" => "text/javascript", ".css" => "text/css", ".html" => "text/html", ".json" => "application/json", ".png" => "image/png", ".jpg" => "image/jpeg", ".svg" => "image/svg+xml", ".wasm" => "application/wasm", _ => "application/octet-stream" };
        await context.Response.SendFileAsync(file, stopping.Token);
    }
    private async Task TileAsync(HttpContext context, string path)
    {
        var parts = path.Split('/');
        if (parts.Length != 5 || Provider is not { } provider || parts[1] != provider.Id
            || !int.TryParse(parts[2], out var z) || !int.TryParse(parts[3], out var x) || !int.TryParse(parts[4], out var y)
            || z < provider.MinZoom || z > provider.MaxZoom || x < 0 || y < 0 || x >= (1 << z) || y >= (1 << z))
        { context.Response.StatusCode = 404; return; }
        var address = new TileAddress(z, x, y);
        var data = await store.GetAsync(provider.Id, address);
        // Navigation only persists tiles through an explicit offline download.
        data ??= await new RegionDownloadService(store, client).FetchAsync(provider, address, stopping.Token);
        context.Response.ContentType = data.ContentType; context.Response.ContentLength = data.Bytes.Length;
        await context.Response.Body.WriteAsync(data.Bytes, stopping.Token);
    }
    private static async Task TextAsync(HttpContext context, string text)
    { context.Response.ContentType = "application/json"; await context.Response.WriteAsync(text); }
    public void Dispose()
    {
        stopping.Cancel();
        Task.Run(async () => { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)); await application.StopAsync(timeout.Token); await application.DisposeAsync(); }).GetAwaiter().GetResult();
        stopping.Dispose();
    }
}
