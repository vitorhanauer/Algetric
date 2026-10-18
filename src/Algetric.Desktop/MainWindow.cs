using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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
    private readonly ObjectPropertiesPanel properties;
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
    private readonly Dictionary<Guid, (Button Select, Button Delete)> objectButtons = [];
    private Layer[] displayedLayers = [];
    private Guid displayedActiveLayer;
    public MainWindow(string? storageDirectory = null)
    {
        Width = 1380; Height = 880; MinWidth = 940; MinHeight = 620;
        dataDirectory = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "algetric");
        Directory.CreateDirectory(dataDirectory);
        tiles = new(Path.Combine(dataDirectory, "maps.sqlite")); settings = new(dataDirectory);
        canvas = new(model);
        properties = new(model, canvas.InvalidateVisual);
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
        AddHandler(KeyDownEvent, OnMoveKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnMoveKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        Deactivated += (_, _) => model.EndTemporaryMove();
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
        tabs.SelectionChanged += (_, _) => { model.EndTemporaryMove(); canvas.CancelDraft(); }; root.Children.Add(tabs);
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
            await Dialogs.Message(this, "Como editar", "Escolha uma ferramenta e clique para criar.\nLinhas e polígonos: duplo clique ou Enter para concluir; Esc cancela.\nSelecionar: arraste um vértice ou o objeto.\nPlano: Mover + arraste navega; segure Espaço para mover temporariamente.\nSolte Espaço para voltar à ferramenta anterior. Botão direito/central também move; roda ajusta o zoom.\nMapa: arraste para navegar; use o zoom para alternar globo e 2D.\nMedidas planimétricas • limite de 50 km da origem.");
        }));
        menu.Items.Add(file); menu.Items.Add(edit); menu.Items.Add(maps); menu.Items.Add(help); return menu;
    }
    private Control BuildToolbar()
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        foreach (var (label, tool) in new[]{("↖ Selecionar",EditorTool.Select),("✥ Mover",EditorTool.Move),("• Ponto",EditorTool.Point),
            ("╱ Segmento",EditorTool.Segment),("⌁ Linha",EditorTool.Polyline),("⬡ Polígono",EditorTool.Polygon)})
        {
            var button = Button(label, () => { canvas.CancelDraft(); model.SetTool(tool); return Task.CompletedTask; });
            if (tool == EditorTool.Move) ToolTip.SetTip(button, "Mover o plano • segure Espaço para ativar temporariamente");
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
        RefreshLayers(); RefreshObjects(); properties.Refresh(); _ = SyncMapAsync();
    }
    private void RefreshLayers()
    {
        if (displayedLayers.SequenceEqual(model.Editor.Document.Layers) && displayedActiveLayer == model.Editor.ActiveLayerId) return;
        displayedLayers = model.Editor.Document.Layers.ToArray(); displayedActiveLayer = model.Editor.ActiveLayerId;
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
        var shapes = model.Editor.Document.Objects;
        // Keep pointer targets alive when leaving a property field commits an edit.
        if (!objectButtons.Keys.SequenceEqual(shapes.Select(shape => shape.Id)))
        {
            objects.Children.Clear(); objectButtons.Clear();
            foreach (var shape in shapes) AddObjectRow(shape);
        }
        foreach (var shape in model.Editor.Document.Objects)
        {
            var (select, delete) = objectButtons[shape.Id]; select.Content = shape.Name;
            select.Background = shape.Id == model.Editor.SelectedId ? Brush.Parse("#D7EAFF") : Brushes.Transparent;
            Avalonia.Automation.AutomationProperties.SetName(delete, $"Excluir {shape.Name}");
            ToolTip.SetTip(delete, $"Excluir {shape.Name}");
        }
        if (objects.Children.Count == 0) objects.Children.Add(new TextBlock { Text = "Crie seu primeiro objeto.", Foreground = Brushes.Gray });
    }
    private void AddObjectRow(GeometryObject shape)
    {
        var select = Button(shape.Name, () => { model.VertexIndex = 0; model.Editor.Select(shape.Id); return Task.CompletedTask; });
        select.HorizontalAlignment = HorizontalAlignment.Stretch;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var delete = DeleteButton(shape);
        Grid.SetColumn(delete, 1); row.Children.Add(select); row.Children.Add(delete);
        objects.Children.Add(row); objectButtons.Add(shape.Id, (select, delete));
    }
    private Button DeleteButton(GeometryObject shape)
    {
        var button = Button("", () => { model.Editor.Delete(shape.Id); return Task.CompletedTask; });
        button.Name = $"DeleteObject_{shape.Id:N}";
        button.Content = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M3,5 L13,5 M6,5 L6,3 L10,3 L10,5 M4,5 L5,15 L11,15 L12,5 M7,7 L7,12 M9,7 L9,12"),
            Stroke = Brush.Parse("#8A3944"),
            StrokeThickness = 1.5,
            Width = 16,
            Height = 18
        };
        Avalonia.Automation.AutomationProperties.SetName(button, $"Excluir {shape.Name}");
        ToolTip.SetTip(button, $"Excluir {shape.Name}");
        return button;
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
    private void OnMoveKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || e.KeyModifiers != KeyModifiers.None || !canvas.IsEffectivelyVisible) return;
        if (e.Source is TextBox or ComboBox || e.Source is Visual visual &&
            visual.GetVisualAncestors().Any(parent => parent is TextBox or ComboBox or Menu)) return;
        model.BeginTemporaryMove(); e.Handled = true;
    }
    private void OnMoveKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || !model.IsTemporaryMove) return;
        model.EndTemporaryMove(); e.Handled = true;
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
