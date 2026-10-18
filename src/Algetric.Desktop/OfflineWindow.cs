using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Algetric.Core;
using Algetric.Infrastructure;

namespace Algetric.Desktop;

internal sealed class OfflineWindow : Window
{
    private readonly TileProvider provider;
    private readonly SqliteTileStore store;
    private readonly RegionDownloadService downloader;
    private readonly TextBox name, west, south, east, north, min, max;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ListBox list = new() { Height = 125 };
    private readonly StackPanel fields = new() { Spacing = 6 };
    private readonly Button download = new() { Content = "Baixar nova região" };
    private readonly Button resume = new() { Content = "Retomar selecionada" };
    private readonly Button remove = new() { Content = "Remover selecionada" };
    private CancellationTokenSource? cancellation;
    private IReadOnlyList<OfflineRegion> regions = [];
    public OfflineWindow(TileProvider provider, GeographicPoint origin, SqliteTileStore store, HttpClient client, string dataDirectory)
    {
        this.provider = provider; this.store = store;
        downloader = new(store, client, bytes => new DriveInfo(Path.GetPathRoot(Path.GetFullPath(dataDirectory))!).AvailableFreeSpace > bytes);
        Title = "Mapas offline"; Width = 560; Height = 780; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = $"{provider.Name}\n{provider.Attribution}", TextWrapping = TextWrapping.Wrap });
        name = Dialogs.Field(fields, "Nome da região", "Minha região");
        west = Dialogs.Field(fields, "Oeste (longitude)", Dialogs.Number(Math.Max(-180, origin.Longitude - 0.05)));
        east = Dialogs.Field(fields, "Leste (longitude)", Dialogs.Number(Math.Min(180, origin.Longitude + 0.05)));
        south = Dialogs.Field(fields, "Sul (latitude)", Dialogs.Number(origin.Latitude - 0.05));
        north = Dialogs.Field(fields, "Norte (latitude)", Dialogs.Number(origin.Latitude + 0.05));
        min = Dialogs.Field(fields, "Zoom mínimo", Math.Max(provider.MinZoom, 10).ToString());
        max = Dialogs.Field(fields, "Zoom máximo", Math.Min(provider.MaxZoom, 14).ToString()); panel.Children.Add(fields);
        var estimate = new Button { Content = "Estimar tiles e espaço" };
        estimate.Click += (_, _) => Try(() => { var region = ReadRegion(); status.Text = $"{TileCoverage.Enumerate(region).Count():N0} tiles • estimativa {TileCoverage.EstimateBytes(region) / 1_000_000d:N1} MB (40 KB/tile)."; }); panel.Children.Add(estimate);
        download.IsEnabled = provider.AllowsOffline; resume.IsEnabled = provider.AllowsOffline;
        download.Click += async (_, _) => { try { await Download(ReadRegion()); } catch (Exception e) { status.Text = e.Message; } };
        panel.Children.Add(download); panel.Children.Add(status); panel.Children.Add(list);
        resume.Click += async (_, _) => { if (list.SelectedIndex >= 0) await Download(regions[list.SelectedIndex]); }; panel.Children.Add(resume);
        remove.Click += async (_, _) => { if (list.SelectedIndex >= 0) { await store.RemoveRegionAsync(regions[list.SelectedIndex].Id); await RefreshRegions(); } }; panel.Children.Add(remove);
        var cancel = new Button { Content = "Interromper download" }; cancel.Click += (_, _) => cancellation?.Cancel(); panel.Children.Add(cancel);
        if (!provider.AllowsOffline) status.Text = "O provedor não autoriza armazenamento offline.";
        Content = new ScrollViewer { Content = panel }; Opened += async (_, _) => await RefreshRegions();
        Closing += (_, _) => cancellation?.Cancel();
    }
    private OfflineRegion ReadRegion()
    {
        var region = new OfflineRegion(Guid.NewGuid(), name.Text ?? "", provider.Id, Dialogs.Parse(west), Dialogs.Parse(south), Dialogs.Parse(east), Dialogs.Parse(north), checked((int)Dialogs.Parse(min)), checked((int)Dialogs.Parse(max)));
        if (region.MinZoom < provider.MinZoom || region.MaxZoom > provider.MaxZoom) throw new ArgumentException("Zoom fora dos limites do provedor.");
        _ = TileCoverage.EstimateBytes(region); return region;
    }
    private async Task Download(OfflineRegion region)
    {
        if (cancellation is not null) return;
        cancellation = new(); fields.IsEnabled = download.IsEnabled = resume.IsEnabled = remove.IsEnabled = false;
        try
        {
            await downloader.DownloadAsync(provider, region, new Progress<DownloadProgress>(p => status.Text = p.Message), cancellation.Token);
            status.Text = "Região disponível offline.";
        }
        catch (OperationCanceledException) { status.Text = "Download interrompido. Selecione a região para retomar."; }
        catch (Exception e) { status.Text = e is HttpRequestException ? "Falha de rede. Selecione a região para retomar." : e.Message; }
        finally
        {
            cancellation.Dispose(); cancellation = null; fields.IsEnabled = remove.IsEnabled = true;
            download.IsEnabled = resume.IsEnabled = provider.AllowsOffline; await RefreshRegions();
        }
    }
    private async Task RefreshRegions()
    {
        regions = (await store.ListRegionsAsync()).Where(r => r.ProviderId == provider.Id).ToArray();
        list.ItemsSource = regions.Select(r => $"{r.Name} • zoom {r.MinZoom}–{r.MaxZoom}").ToArray();
    }
    private void Try(Action action) { try { action(); } catch (Exception e) { status.Text = e.Message; } }
}
