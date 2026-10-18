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
    private readonly WarningToast warningToast;
    private readonly LayerPanel layers;
    private readonly Canvas dragOverlay = new() { IsHitTestVisible = false };
    private readonly ObjectPropertiesPanel properties;
    private readonly TextBlock selectionSummary = new() { Name = "SelectionSummary", TextWrapping = TextWrapping.Wrap };
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
        UiTheme.Initialize(Application.Current!);
        Width = 1380; Height = 880; MinWidth = 940; MinHeight = 620;
        dataDirectory = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "algetric");
        Directory.CreateDirectory(dataDirectory);
        tiles = new(Path.Combine(dataDirectory, "maps.sqlite")); settings = new(dataDirectory);
        canvas = new(model); warningToast = new();
        model.WarningRequested += warningToast.Show;
        properties = new(model, canvas.InvalidateVisual);
        layers = new(model, dragOverlay, layer => _ = RunAsync(() => EditLayerAsync(layer)),
            layer => _ = RunAsync(async () => { await Dialogs.DeleteLayer(this, model, layer); }));
        server = new(Path.Combine(AppContext.BaseDirectory, "Web"), tiles, client);
        server.GetStateAsync = async () => await Dispatcher.UIThread.InvokeAsync(() => { mapReady = true; return model.MapState(); });
        server.HandleMessageAsync = async json => await Dispatcher.UIThread.InvokeAsync(() => model.HandleMessage(json));
        server.Diagnostic += message => Dispatcher.UIThread.Post(() => model.SetStatus(message));
        map.Source = new Uri(server.BaseUri, "index.html");
        Content = BuildLayout(); DataContext = model;
        model.Changed += Refresh;
        model.FileActionRequested += command => _ = RunAsync(command switch { "save" => () => SaveAsync(false), "open" => OpenAsync, "addLayer" => () => EditLayerAsync(null), _ => NewAsync });
        model.PropertyChanged += (_, _) => { Title = model.Title; status.Text = model.Status; };
        KeyDown += OnKeyDown;
        AddHandler(KeyDownEvent, OnMoveKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnMoveKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        Deactivated += (_, _) => { model.EndTemporaryMove(); layers.CancelDrag(); };
        Closing += OnClosing;
        Closed += (_, _) => { warningToast.IsOpen = false; server.Dispose(); client.Dispose(); };
        Opened += async (_, _) => await RunAsync(LoadProviderAsync);
        Refresh();
    }
    private Control BuildLayout()
    {
        var root = new DockPanel();
        UiTheme.Bind(root, BackgroundProperty, "AppSurface");
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top);
        header.Children.Add(BuildMenu()); header.Children.Add(BuildToolbar()); root.Children.Add(header);
        DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var left = BuildSidebar();
        DockPanel.SetDock(left, Dock.Left); root.Children.Add(left);
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Plano cartesiano", Content = canvas });
        tabs.Items.Add(new TabItem { Header = "🌐 Mapa", Content = map });
        tabs.SelectionChanged += (_, _) => { layers.CancelDrag(); model.EndTemporaryMove(); canvas.CancelDraft(); }; root.Children.Add(tabs);
        var overlay = new Grid(); warningToast.PlacementTarget = overlay; overlay.Children.Add(root); overlay.Children.Add(dragOverlay); overlay.Children.Add(warningToast);
        return overlay;
    }
    private Control BuildSidebar()
    {
        var sidebar = new Grid { Name = "Sidebar", RowDefinitions = new RowDefinitions("*,280") };
        var layerSection = new StackPanel { Spacing = 14, Margin = new Thickness(12) };
        layerSection.Children.Add(Heading("Projeto"));
        layerSection.Children.Add(BuildLayersHeading()); layerSection.Children.Add(layers);
        sidebar.Children.Add(new ScrollViewer { Name = "LayerScroll", Content = layerSection });
        var propertyContent = new StackPanel { Spacing = 14, Margin = new Thickness(12) };
        propertyContent.Children.Add(Heading("Propriedades")); propertyContent.Children.Add(properties); propertyContent.Children.Add(selectionSummary);
        var propertySection = new Border
        {
            Name = "PropertiesSection", BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new ScrollViewer { Content = propertyContent }
        };
        UiTheme.Bind(propertySection, Border.BorderBrushProperty, "PanelBorder");
        Grid.SetRow(propertySection, 1); sidebar.Children.Add(propertySection);
        var border = new Border { Name = "SidebarBorder", Width = 285, BorderThickness = new Thickness(0, 0, 1, 0), Child = sidebar };
        UiTheme.Bind(border, Border.BackgroundProperty, "PanelSurface");
        UiTheme.Bind(border, Border.BorderBrushProperty, "PanelBorder");
        return border;
    }
    private MenuItem BuildAppearanceMenu()
    {
        var appearance = new MenuItem { Header = "Aparência" };
        var system = Item("Seguir o sistema", () => SetThemeAsync(Avalonia.Styling.ThemeVariant.Default)); system.Name = "SystemTheme";
        var light = Item("Tema claro", () => SetThemeAsync(Avalonia.Styling.ThemeVariant.Light)); light.Name = "LightTheme";
        var dark = Item("Tema escuro", () => SetThemeAsync(Avalonia.Styling.ThemeVariant.Dark)); dark.Name = "DarkTheme";
        appearance.Items.Add(system); appearance.Items.Add(light); appearance.Items.Add(dark);
        return appearance;
    }
    private Task SetThemeAsync(Avalonia.Styling.ThemeVariant theme)
    {
        Application.Current!.RequestedThemeVariant = theme;
        return Task.CompletedTask;
    }
    private Control BuildLayersHeading()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var add = Button("+", () => EditLayerAsync(null)); add.Name = "AddLayer";
        ToolTip.SetTip(add, "Criar camada (Ctrl+Shift+N)"); Avalonia.Automation.AutomationProperties.SetName(add, "Criar camada");
        Grid.SetColumn(add, 1); var title = Heading("Camadas"); title.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(title); row.Children.Add(add); return row;
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
            await Dialogs.Message(this, "Como editar", "Escolha uma ferramenta e clique para criar.\nLinhas e polígonos: duplo clique ou Enter para concluir; Esc cancela.\nSelecionar: arraste no espaço vazio para selecionar uma área; arraste um vértice ou objeto para mover.\nPlano: Mover + arraste navega; segure Espaço para mover temporariamente.\nSolte Espaço para voltar à ferramenta anterior. Botão direito/central também move; roda ajusta o zoom.\nMapa: use Mover para navegar; use o zoom para alternar globo e 2D.\nMedidas planimétricas • limite de 50 km da origem.");
        }));
        menu.Items.Add(file); menu.Items.Add(edit); menu.Items.Add(maps); menu.Items.Add(BuildAppearanceMenu()); menu.Items.Add(help); return menu;
    }
    private Control BuildToolbar()
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        foreach (var shortcut in EditorShortcuts.Tools)
        {
            var button = ToolbarButton(shortcut.Icon, shortcut.Label,
                () => { SelectTool(shortcut.Tool); return Task.CompletedTask; });
            var hint = $"{shortcut.Label} ({shortcut.Key})";
            if (shortcut.Tool == EditorTool.Move) hint += " • segure Espaço para mover temporariamente";
            if (shortcut.Tool is EditorTool.Polyline or EditorTool.Polygon) hint += " • Enter ou duplo clique para concluir";
            ToolTip.SetTip(button, hint);
            tools.Add((button, shortcut.Tool)); panel.Children.Add(button);
        }
        panel.Children.Add(ToolbarButton("↶", "Desfazer", () => { model.Editor.Undo(); return Task.CompletedTask; }, "Ctrl+Z"));
        panel.Children.Add(ToolbarButton("↷", "Refazer", () => { model.Editor.Redo(); return Task.CompletedTask; }, "Ctrl+Y")); return panel;
    }
    private Button ToolbarButton(string icon, string label, Func<Task> action, string? shortcut = null)
    {
        var button = Button(icon, action);
        button.Width = 40; button.Height = 40;
        button.FontSize = 28;
        button.Padding = new Thickness(0);
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        button.Background = Brushes.Transparent;
        if (icon == "mouse") button.Content = MousePointerIcon(button);
        ToolTip.SetTip(button, shortcut is null ? label : $"{label} ({shortcut})");
        Avalonia.Automation.AutomationProperties.SetName(button, label);
        return button;
    }
    private static Control MousePointerIcon(Button button)
    {
        var pointer = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M3,2 L3,22 L8,17 L12,25 L16,23 L12,15 L20,15 Z"),
            Width = 23, Height = 27, Stretch = Stretch.Uniform
        };
        pointer.Bind(Avalonia.Controls.Shapes.Shape.FillProperty,
            new Avalonia.Data.Binding(nameof(Foreground)) { Source = button });
        return pointer;
    }
    private void Refresh()
    {
        Title = model.Title; status.Text = model.Status;
        selectionSummary.IsVisible = model.Editor.SelectedIds.Count > 1;
        selectionSummary.Text = $"{model.Editor.SelectedIds.Count} objetos selecionados";
        foreach (var (button, tool) in tools) UiTheme.SetHighlight(button, Avalonia.Controls.Button.BackgroundProperty, model.Tool == tool, "ActiveTool");
        layers.Refresh(); properties.Refresh(); _ = SyncMapAsync();
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
        if (e.Key == Key.Escape && layers.CancelDrag()) { e.Handled = true; return; }
        if (HandleToolShortcut(e)) return;
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (control && e.Key == Key.N && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        { _ = RunAsync(() => EditLayerAsync(null)); e.Handled = true; return; }
        if (control && e.Key == Key.S) { _ = RunAsync(() => SaveAsync(false)); e.Handled = true; }
        else if (control && e.Key == Key.O) { _ = RunAsync(OpenAsync); e.Handled = true; }
        else if (control && e.Key == Key.N) { _ = RunAsync(NewAsync); e.Handled = true; }
        else if (control && e.Key == Key.Z) { model.Editor.Undo(); e.Handled = true; }
        else if (control && e.Key == Key.Y) { model.Editor.Redo(); e.Handled = true; }
        else if (e.Source is not TextBox && e.Key == Key.Delete) { model.Editor.DeleteSelected(); e.Handled = true; }
        else if (e.Key == Key.Escape) { canvas.CancelDraft(); model.SetTool(EditorTool.Select); _ = map.InvokeScript("window.algetric?.cancel()"); e.Handled = true; }
        else if (e.Source is not TextBox && e.Key == Key.Enter) { canvas.FinishDraft(); _ = map.InvokeScript("window.algetric?.finish()"); e.Handled = true; }
    }
    private void SelectTool(EditorTool tool)
    {
        canvas.CancelDraft(); model.SetTool(tool);
    }
    private bool HandleToolShortcut(KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || e.Source is TextBox or ComboBox || e.Source is Visual visual &&
            visual.GetVisualAncestors().Any(parent => parent is TextBox or ComboBox or Menu)) return false;
        var shortcut = EditorShortcuts.Tools.FirstOrDefault(shortcut => shortcut.Key == e.Key);
        if (shortcut is null) return false;
        SelectTool(shortcut.Tool); e.Handled = true; return true;
    }
    private Task EditLayerAsync(Layer? layer) => Dialogs.EditLayer(this, model, layer);
    private Task EditOriginAsync() => Dialogs.EditOrigin(this, model);
    private async Task EditProviderAsync() { if (await Dialogs.EditProvider(this, settings, model.Provider)) await LoadProviderAsync(); }
    private async Task OfflineAsync()
    {
        if (model.Provider is null) { await Dialogs.Message(this, "Mapas offline", "Configure um provedor no menu Mapa antes de baixar regiões."); return; }
        await new OfflineWindow(model.Provider, model.Editor.Document.Origin, tiles, client, dataDirectory).ShowDialog(this);
    }
    private static TextBlock Heading(string text)
    {
        var heading = new TextBlock { Text = text, FontWeight = FontWeight.Bold };
        UiTheme.Bind(heading, TextBlock.ForegroundProperty, "HeadingText");
        return heading;
    }
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
