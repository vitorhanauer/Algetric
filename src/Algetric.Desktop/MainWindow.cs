using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Algetric.Core;
using Algetric.Infrastructure;
using System.Net.Http.Headers;

namespace Algetric.Desktop;

public sealed class MainWindow : Window
{
    private readonly EditorViewModel model = new();
    private readonly CartesianCanvas canvas;
    private readonly StackPanel layers = new() { Spacing = 5 };
    private readonly StackPanel objects = new() { Spacing = 4 };
    private readonly StackPanel properties = new() { Spacing = 8 };
    private readonly TextBlock status = new() { Margin = new Thickness(12, 6) };
    private readonly NativeWebView map = new();
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly LocalMapServer server;
    private readonly SqliteTileStore tiles;
    private readonly ProviderSettings settings;
    private readonly string dataDirectory;
    private bool mapReady;
    private bool closingApproved;
    private readonly List<(Button Button, EditorTool Tool)> tools = [];
    public MainWindow(string? storageDirectory = null)
    {
        Width = 1380; Height = 880; MinWidth = 940; MinHeight = 620;
        dataDirectory = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "algetric");
        Directory.CreateDirectory(dataDirectory);
        tiles = new(Path.Combine(dataDirectory, "maps.sqlite")); settings = new(dataDirectory);
        canvas = new(model);
        server = new(Path.Combine(AppContext.BaseDirectory, "Web"), tiles, client);
        server.GetStateAsync = async () => await Dispatcher.UIThread.InvokeAsync(() => { mapReady = true; return model.MapState(); });
        server.HandleMessageAsync = async json => await Dispatcher.UIThread.InvokeAsync(() => model.HandleMessage(json));
        server.Diagnostic += message => Dispatcher.UIThread.Post(() => model.SetStatus(message));
        map.Source = new Uri(server.BaseUri, "index.html");
        Content = BuildLayout(); DataContext = model;
        model.Changed += Refresh;
        model.FileActionRequested += command => _ = RunAsync(command switch { "save" => () => SaveAsync(false), "open" => OpenAsync, _ => NewAsync });
        model.PropertyChanged += (_, _) => { Title = model.Title; status.Text = model.Status; };
        KeyDown += OnKeyDown;
        Closing += OnClosing;
        Closed += (_, _) => { server.Dispose(); client.Dispose(); };
        Opened += async (_, _) => await RunAsync(LoadProviderAsync);
        Refresh();
    }
    private Control BuildLayout()
    {
        var root = new DockPanel { Background = Brush.Parse("#F6F9FC") };
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top);
        header.Children.Add(BuildMenu()); header.Children.Add(BuildToolbar()); root.Children.Add(header);
        DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var sidebar = new StackPanel { Spacing = 14, Margin = new Thickness(12) };
        sidebar.Children.Add(Heading("Projeto")); sidebar.Children.Add(Heading("Camadas"));
        sidebar.Children.Add(layers); sidebar.Children.Add(Button("+ Camada", () => EditLayerAsync(null)));
        sidebar.Children.Add(Heading("Objetos")); sidebar.Children.Add(objects);
        sidebar.Children.Add(Heading("Propriedades")); sidebar.Children.Add(properties);
        var left = new Border
        {
            Width = 285,
            BorderBrush = Brush.Parse("#DCE5EF"),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Background = Brushes.White,
            Child = new ScrollViewer { Content = sidebar }
        };
        DockPanel.SetDock(left, Dock.Left); root.Children.Add(left);
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Plano cartesiano", Content = canvas });
        tabs.Items.Add(new TabItem { Header = "🌐 Mapa", Content = map });
        tabs.SelectionChanged += (_, _) => canvas.CancelDraft(); root.Children.Add(tabs);
        return root;
    }
    private Menu BuildMenu()
    {
        var menu = new Menu(); var file = new MenuItem { Header = "Arquivo" };
        file.Items.Add(Item("Novo", NewAsync)); file.Items.Add(Item("Abrir…", OpenAsync));
        file.Items.Add(Item("Salvar", () => SaveAsync(false))); file.Items.Add(Item("Salvar como…", () => SaveAsync(true)));
        file.Items.Add(Item("Sair", () => { Close(); return Task.CompletedTask; }));
        var edit = new MenuItem { Header = "Editar" };
        edit.Items.Add(Item("Desfazer", () => { model.Editor.Undo(); return Task.CompletedTask; }));
        edit.Items.Add(Item("Refazer", () => { model.Editor.Redo(); return Task.CompletedTask; }));
        edit.Items.Add(Item("Excluir objeto", () => { model.Editor.DeleteSelected(); return Task.CompletedTask; }));
        var maps = new MenuItem { Header = "Mapa" }; maps.Items.Add(Item("Origem geográfica…", EditOriginAsync));
        maps.Items.Add(Item("Definir origem no mapa", () => { model.SetTool(EditorTool.Origin); return Task.CompletedTask; }));
        maps.Items.Add(Item("Provedor de mapas…", EditProviderAsync)); maps.Items.Add(Item("Regiões offline…", OfflineAsync));
        var help = new MenuItem { Header = "Ajuda" }; help.Items.Add(Item("Como editar", async () =>
        {
            await Dialogs.Message(this, "Como editar", "Escolha uma ferramenta e clique para criar.\nLinhas e polígonos: duplo clique ou Enter para concluir; Esc cancela.\nSelecionar: arraste um vértice ou o objeto.\nPlano: botão direito/central move; roda ajusta o zoom.\nMapa: arraste para navegar; use o zoom para alternar globo e 2D.\nMedidas planimétricas • limite de 50 km da origem.");
        }));
        menu.Items.Add(file); menu.Items.Add(edit); menu.Items.Add(maps); menu.Items.Add(help); return menu;
    }
    private Control BuildToolbar()
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        foreach (var (label, tool) in new[]{("↖ Selecionar",EditorTool.Select),("• Ponto",EditorTool.Point),
            ("╱ Segmento",EditorTool.Segment),("⌁ Linha",EditorTool.Polyline),("⬡ Polígono",EditorTool.Polygon)})
        {
            var button = Button(label, () => { canvas.CancelDraft(); model.SetTool(tool); return Task.CompletedTask; });
            tools.Add((button, tool)); panel.Children.Add(button);
        }
        panel.Children.Add(Button("✓ Concluir", () => { canvas.FinishDraft(); _ = map.InvokeScript("window.algetric?.finish()"); return Task.CompletedTask; }));
        panel.Children.Add(Button("↶ Desfazer", () => { model.Editor.Undo(); return Task.CompletedTask; }));
        panel.Children.Add(Button("↷ Refazer", () => { model.Editor.Redo(); return Task.CompletedTask; })); return panel;
    }
    private void Refresh()
    {
        Title = model.Title; status.Text = model.Status;
        foreach (var (button, tool) in tools) button.Background = model.Tool == tool ? Brush.Parse("#D7F1FB") : Brushes.Transparent;
        RefreshLayers(); RefreshObjects(); RefreshProperties(); _ = SyncMapAsync();
    }
    private void RefreshLayers()
    {
        layers.Children.Clear();
        foreach (var layer in model.Editor.Document.Layers)
        {
            var row = new DockPanel(); var edit = Button("⋯", () => EditLayerAsync(layer)); DockPanel.SetDock(edit, Dock.Right); row.Children.Add(edit);
            var visible = new CheckBox { IsChecked = layer.Visible, Content = layer.Name, Foreground = Brush.Parse(layer.Color) };
            visible.IsCheckedChanged += (_, _) => model.Try(() => model.Editor.UpdateLayer(layer with { Visible = visible.IsChecked == true }));
            row.Children.Add(visible); layers.Children.Add(row);
        }
        var active = new ComboBox
        {
            ItemsSource = model.Editor.Document.Layers.Select(l => l.Name).ToArray(),
            SelectedIndex = Array.FindIndex(model.Editor.Document.Layers.ToArray(), l => l.Id == model.Editor.ActiveLayerId),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        active.SelectionChanged += (_, _) => { if (active.SelectedIndex >= 0) model.Editor.ActiveLayerId = model.Editor.Document.Layers[active.SelectedIndex].Id; };
        layers.Children.Add(new TextBlock { Text = "Camada para novos objetos", FontSize = 11 }); layers.Children.Add(active);
    }
    private void RefreshObjects()
    {
        objects.Children.Clear();
        foreach (var shape in model.Editor.Document.Objects)
        {
            var button = Button(shape.Name, () => { model.VertexIndex = 0; model.Editor.Select(shape.Id); return Task.CompletedTask; });
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.Background = shape.Id == model.Editor.SelectedId ? Brush.Parse("#D7EAFF") : Brushes.Transparent;
            objects.Children.Add(button);
        }
        if (objects.Children.Count == 0) objects.Children.Add(new TextBlock { Text = "Crie seu primeiro objeto.", Foreground = Brushes.Gray });
    }
    private void RefreshProperties()
    {
        properties.Children.Clear(); if (model.Selected is not { } shape) return;
        var name = Dialogs.Field(properties, "Nome", shape.Name);
        var vertex = new ComboBox
        {
            ItemsSource = Enumerable.Range(0, shape.Vertices.Length).Select(i => $"Vértice {i + 1}").ToArray(),
            SelectedIndex = Math.Clamp(model.VertexIndex, 0, shape.Vertices.Length - 1)
        };
        properties.Children.Add(vertex); var p = shape.Vertices[vertex.SelectedIndex];
        var x = Dialogs.Field(properties, "X (m)", Dialogs.Number(p.X)); var y = Dialogs.Field(properties, "Y (m)", Dialogs.Number(p.Y));
        vertex.SelectionChanged += (_, _) =>
        {
            if (vertex.SelectedIndex < 0) return; model.VertexIndex = vertex.SelectedIndex;
            var selected = shape.Vertices[vertex.SelectedIndex]; x.Text = Dialogs.Number(selected.X); y.Text = Dialogs.Number(selected.Y); canvas.InvalidateVisual();
        };
        properties.Children.Add(Button("Aplicar", () =>
        {
            model.Try(() =>
            {
                var points = shape.Vertices.ToArray(); points[vertex.SelectedIndex] = new(Dialogs.Parse(x), Dialogs.Parse(y));
                model.UpdateObject(shape.Id, name.Text ?? "", points);
            }); return Task.CompletedTask;
        }));
        var measure = GeometryRules.Measure(shape);
        properties.Children.Add(new TextBlock { Text = $"Comprimento/perímetro: {measure.Length:N2} m\nÁrea: {measure.Area:N2} m²\nMedidas planimétricas", TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        properties.Children.Add(Button("Excluir", () => { model.Editor.DeleteSelected(); return Task.CompletedTask; }));
    }
    private async Task SyncMapAsync()
    {
        if (!mapReady) return;
        try { await map.InvokeScript($"window.algetric?.sync({model.MapState()})"); }
        catch (Exception) { model.SetStatus("Falha ao atualizar o globo. Confira as dependências do WebView."); }
    }
    private async Task LoadProviderAsync()
    {
        model.Provider = await settings.LoadAsync(); server.Provider = model.Provider;
        client.DefaultRequestHeaders.Authorization = null;
        if (model.Provider is not null)
        {
            var token = await ProviderSecretStore.Create().GetAsync(model.Provider.Id);
            if (!string.IsNullOrEmpty(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        await SyncMapAsync();
    }
    private async Task NewAsync()
    {
        if (!await ConfirmUnsavedAsync()) return; canvas.CancelDraft(); model.FilePath = null; model.Load(ProjectDocument.Empty());
    }
    private async Task OpenAsync()
    {
        if (!await ConfirmUnsavedAsync()) return;
        var files = await StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Abrir projeto",
            AllowMultiple = false,
            FileTypeFilter = [new("Projeto Algetric") { Patterns = ["*.algetric"] }]
        });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath() ?? throw new IOException("Selecione um arquivo local.");
        var document = await new JsonProjectRepository().LoadAsync(path); canvas.CancelDraft(); model.FilePath = path; model.Load(document);
    }
    private async Task SaveAsync(bool saveAs)
    {
        var path = model.FilePath;
        if (saveAs || path is null)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new()
            {
                Title = "Salvar projeto",
                SuggestedFileName = "Projeto.algetric",
                DefaultExtension = "algetric",
                FileTypeChoices = [new("Projeto Algetric") { Patterns = ["*.algetric"] }]
            });
            if (file is null) return; path = file.TryGetLocalPath() ?? throw new IOException("Selecione um arquivo local.");
        }
        var snapshot = model.Editor.Document; await new JsonProjectRepository().SaveAsync(path, snapshot);
        model.FilePath = path; if (model.Editor.Document == snapshot) model.Editor.MarkSaved(); model.SetStatus("Projeto salvo.");
    }
    private async Task<bool> ConfirmUnsavedAsync()
    {
        if (!model.Editor.IsDirty) return true;
        var answer = await Dialogs.Choose(this, "Alterações pendentes", "Salvar o projeto antes de continuar?", ["Salvar", "Descartar", "Cancelar"]);
        if (answer == "Descartar") return true;
        if (answer != "Salvar") return false; await SaveAsync(false); return !model.Editor.IsDirty;
    }
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (closingApproved || !model.Editor.IsDirty) return;
        e.Cancel = true;
        await RunAsync(async () => { if (await ConfirmUnsavedAsync()) { closingApproved = true; Close(); } });
    }
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (control && e.Key == Key.S) { _ = RunAsync(() => SaveAsync(false)); e.Handled = true; }
        else if (control && e.Key == Key.O) { _ = RunAsync(OpenAsync); e.Handled = true; }
        else if (control && e.Key == Key.N) { _ = RunAsync(NewAsync); e.Handled = true; }
        else if (control && e.Key == Key.Z) { model.Editor.Undo(); e.Handled = true; }
        else if (control && e.Key == Key.Y) { model.Editor.Redo(); e.Handled = true; }
        else if (e.Source is not TextBox && e.Key == Key.Delete) { model.Editor.DeleteSelected(); e.Handled = true; }
        else if (e.Key == Key.Escape) { canvas.CancelDraft(); model.SetTool(EditorTool.Select); _ = map.InvokeScript("window.algetric?.cancel()"); e.Handled = true; }
        else if (e.Source is not TextBox && e.Key == Key.Enter) { canvas.FinishDraft(); _ = map.InvokeScript("window.algetric?.finish()"); e.Handled = true; }
    }
    private Task EditLayerAsync(Layer? layer) => Dialogs.EditLayer(this, model, layer);
    private Task EditOriginAsync() => Dialogs.EditOrigin(this, model);
    private async Task EditProviderAsync() { if (await Dialogs.EditProvider(this, settings, model.Provider)) await LoadProviderAsync(); }
    private async Task OfflineAsync()
    {
        if (model.Provider is null) { await Dialogs.Message(this, "Mapas offline", "Configure um provedor no menu Mapa antes de baixar regiões."); return; }
        await new OfflineWindow(model.Provider, model.Editor.Document.Origin, tiles, client, dataDirectory).ShowDialog(this);
    }
    private static TextBlock Heading(string text) => new() { Text = text, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#101B4F") };
    private Button Button(string label, Func<Task> action)
    {
        var button = new Button { Content = label, Margin = new Thickness(2), Padding = new Thickness(9, 6) };
        button.Click += async (_, _) => await RunAsync(action); return button;
    }
    private MenuItem Item(string label, Func<Task> action)
    { var item = new MenuItem { Header = label }; item.Click += async (_, _) => await RunAsync(action); return item; }
    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception e) when (e is not OutOfMemoryException)
        { model.SetStatus(e.Message); await Dialogs.Message(this, "Não foi possível concluir", e.Message); }
    }
}
