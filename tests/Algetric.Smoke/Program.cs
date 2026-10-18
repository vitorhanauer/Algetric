using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Themes.Fluent;
using Algetric.Core;
using Algetric.Desktop;
using System.Reflection;
using System.Text.Json;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => AppBuilder.Configure<SmokeApplication>().UsePlatformDetect()
        .WithInterFont().LogToTrace().StartWithClassicDesktopLifetime(args);
}
public sealed partial class SmokeApplication : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme());
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Avalonia.Controls.ColorPicker/"))
        { Source = new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml") });
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Default; }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow(Path.GetFullPath("artifacts/smoke/profile")) { Position = new PixelPoint(100, 100) }; desktop.MainWindow = window;
            window.Opened += async (_, _) =>
            {
                var exitCode = 0;
                try { await RunAsync(window); }
                catch (Exception error) { Console.Error.WriteLine(error); exitCode = 1; }
                finally
                {
                    typeof(MainWindow).GetField("closingApproved", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, true);
                    desktop.Shutdown(exitCode);
                }
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
    private static async Task RunAsync(MainWindow window)
    {
        await Task.Delay(200);
        var model = Field<EditorViewModel>(window, "model"); var map = Field<NativeWebView>(window, "map");
        if (model.Editor.Document.Layers.Length != 0 || model.Editor.ActiveLayerId is not null)
            throw new InvalidOperationException("New projects must start with no layers.");
        await VerifyMissingLayerWarningAsync(window, model);
        model.Editor.AddLayer("Camada 1", "#009CA6");
        model.Editor.AddLayer("Camada 2", "#FF791F");
        model.Editor.SetActiveLayer(model.Editor.Document.Layers[0].Id);
        model.Add(ShapeKind.Polygon, [new(0, 0), new(100, 0), new(100, 100), new(0, 100)]);
        await VerifyLanguagesAsync(window, model);
        await VerifyPolygonVisibilityAsync(window, model);
        await VerifyOtherObjectVisibilityAsync(window, model);
        await VerifyFunctionGraphsAsync(window, model);
        await VerifyToolShortcutsAsync(window, model);
        await VerifyCoveragePathAsync(window, model);
        await VerifyPropertiesAsync(window, model);
        await VerifyAreaSelectionAsync(window, model);
        VerifyToolbar(window);
        await VerifyFixedPropertiesAsync(window, model);
        await VerifyThemesAsync(window, model);
        VerifyNavigation(window, model);
        await VerifyLayerPanelAsync(window, model);
        await VerifyLayerDeletionAsync(window, model);
        await VerifyLayerColorsAsync(window, model);
        model.Editor.MarkSaved();
        await Task.Delay(1000);
        Directory.CreateDirectory("artifacts/smoke");
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height)))
        { bitmap.Render(window); bitmap.Save("artifacts/smoke/cartesian.png", PngBitmapEncoderOptions.Default); }
        if (((IClassicDesktopStyleApplicationLifetime)Current!.ApplicationLifetime!).Args?.Contains("--properties-only") == true) return;
        window.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex = 1;
        var ready = false;
        for (var i = 0; i < 40; i++)
        {
            await Task.Delay(500);
            var response = await map.InvokeScript("Boolean(window.algetric?.diagnostics().objects===1)");
            if (response?.Contains("true") == true) { ready = true; break; }
        }
        if (!ready) throw new InvalidOperationException("Globo não inicializou: " + await map.InvokeScript("document.body.innerText"));
        await VerifyMapLanguageAsync(window, map);
        await VerifyMapCoverageMenuAsync(window, map, model);
        await VerifyMapWarningsAndShortcutsAsync(window, map, model);
        await VerifyMapAreaSelectionAsync(map, model);
        Console.WriteLine("GLOBE: " + await map.InvokeScript("JSON.stringify(window.algetric.diagnostics())"));
        var store = Field<Algetric.Infrastructure.SqliteTileStore>(window, "tiles");
        var server = Field<Algetric.Infrastructure.LocalMapServer>(window, "server");
        var provider = new TileProvider("offline-smoke", "Mapa local de teste", "https://offline.invalid/{z}/{x}/{y}", 0, 0, "Mapa sintético de teste", true);
        var region = new OfflineRegion(Guid.NewGuid(), "Mundo de teste", provider.Id, -180, -85, 180, 85, 0, 0);
        await store.SaveRegionAsync(region);
        using (var tile = new RenderTargetBitmap(new PixelSize(256, 256)))
        using (var buffer = new MemoryStream())
        {
            var solid = new Border { Width = 256, Height = 256, Background = Avalonia.Media.Brushes.LightSlateGray };
            solid.Measure(new Size(256, 256)); solid.Arrange(new Rect(0, 0, 256, 256));
            tile.Render(solid); tile.Save(buffer, PngBitmapEncoderOptions.Default);
            await store.PutAsync(provider.Id, new(0, 0, 0), buffer.ToArray(), "image/png", region.Id);
        }
        model.Provider = provider; server.Provider = provider; model.SetTool(EditorTool.Select);
        var offlineLoaded = false;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(500);
            var diagnostic = await map.InvokeScript("JSON.stringify(window.algetric.diagnostics())");
            if (diagnostic?.Contains("offline-smoke") == true && diagnostic.Contains("\"tilesLoaded\":true")
                && diagnostic.Contains("\"mapMissing\":false")) { offlineLoaded = true; break; }
        }
        if (!offlineLoaded) throw new InvalidOperationException("Cesium não carregou o tile SQLite offline.");
        Console.WriteLine("OFFLINE TILE: SQLite raster loaded by Cesium without an external server.");

        await map.InvokeScript("document.getElementById('home').click()");
        var transitioned = false;
        for (var i = 0; i < 40; i++)
        {
            await Task.Delay(500); var result = await map.InvokeScript("JSON.stringify(window.algetric.diagnostics())");
            if (result?.Contains("2D") == true) { Console.WriteLine("TRANSITION: " + result); transitioned = true; break; }
        }
        if (!transitioned) throw new InvalidOperationException("Transição automática 2D não ocorreu.");
        model.SetTool(EditorTool.Point);
        await Task.Delay(500);
        await map.InvokeScript("""
            (()=>{
              const canvas=document.querySelector('canvas'),r=canvas.getBoundingClientRect();
              const options={bubbles:true,clientX:r.left+r.width/2+20,clientY:r.top+r.height/2+20,button:0,pointerId:1,pointerType:'mouse',isPrimary:true};
              canvas.dispatchEvent(new PointerEvent('pointerdown',{...options,buttons:1}));
              canvas.dispatchEvent(new PointerEvent('pointerup',{...options,buttons:0}));
            })()
            """);
        await Task.Delay(1000);
        if (model.Editor.Document.Objects.Length != 2) throw new InvalidOperationException("Edição sobre o mapa não criou o ponto: " + model.Status);
        model.Editor.Undo();
        if (model.Editor.Document.Objects.Length != 1) throw new InvalidOperationException("Undo da edição terrestre falhou.");
        model.Editor.Redo();
        var returned = false;
        for (var attempt = 0; attempt < 8 && !returned; attempt++)
        {
            await map.InvokeScript("""
                (()=>{const canvas=document.querySelector('canvas'),r=canvas.getBoundingClientRect();
                canvas.dispatchEvent(new WheelEvent('wheel',{bubbles:true,deltaY:1200,clientX:r.left+r.width/2,clientY:r.top+r.height/2}));})()
                """);
            await Task.Delay(1200);
            var diagnostic = await map.InvokeScript("JSON.stringify(window.algetric.diagnostics())");
            returned = diagnostic?.Contains("3D") == true && diagnostic.Contains("\"morphing\":false");
        }
        if (!returned) throw new InvalidOperationException("Retorno automático ao globo não ocorreu.");
        var repository = new Algetric.Infrastructure.JsonProjectRepository();
        await repository.SaveAsync("artifacts/smoke/example.algetric", model.Editor.Document);
        model.Load(await repository.LoadAsync("artifacts/smoke/example.algetric"));
        if (model.Editor.Document.Objects.Length != 2) throw new InvalidOperationException("Persistência perdeu objetos.");
        model.Editor.MarkSaved();
        await File.WriteAllTextAsync("artifacts/smoke/map.txt", await map.InvokeScript("document.body.innerText"));
        // The native WebView is captured separately during manual desktop verification.
        Console.WriteLine("SMOKE PASS: UI, WebGL, offline raster, synchronization, 3D↔2D, map editing, undo/redo and persistence.");
    }
    private static void VerifyNavigation(MainWindow window, EditorViewModel model)
    {
        var canvas = Field<CartesianCanvas>(window, "canvas");
        var move = window.GetVisualDescendants().OfType<Button>().SingleOrDefault(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Mover");
        if (move is null) throw new InvalidOperationException("Missing Move toolbar button.");
        var original = model.Editor.Document;
        move.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (model.Tool != EditorTool.Move) throw new InvalidOperationException("Move button did not select navigation.");
        VerifyPan(window, canvas, model);
        model.SetTool(EditorTool.Segment); canvas.Focus();
        ClickCanvas(window, canvas, new(400, 300));
        canvas.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        canvas.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        if (model.Tool != EditorTool.Move) throw new InvalidOperationException("Space did not temporarily select Move.");
        VerifyPan(window, canvas, model);
        canvas.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        if (model.Tool != EditorTool.Segment) throw new InvalidOperationException("Space release did not restore Segment.");
        ClickCanvas(window, canvas, new(500, 350));
        if (model.Editor.Document.Objects.Length != original.Objects.Length + 1) throw new InvalidOperationException("Temporary pan discarded the in-progress segment.");
        var field = window.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "PropertyName");
        field.Focus(); field.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        if (model.Tool != EditorTool.Segment) throw new InvalidOperationException("Space in a text field activated Move.");
        canvas.Focus(); canvas.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        typeof(WindowBase).GetMethod("HandleDeactivated", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        if (model.Tool != EditorTool.Segment) throw new InvalidOperationException("Window deactivation left Move active.");
        model.Load(original); model.Editor.Select(original.Objects[0].Id);
        Console.WriteLine("NAVIGATION PASS: Move button, left drag, Space repeat/release, draft preservation, text input and deactivation.");
    }
    private static void VerifyPan(MainWindow window, CartesianCanvas canvas, EditorViewModel model)
    {
        var before = model.Editor.Document;
        var pointer = new Avalonia.Input.Pointer(101, PointerType.Mouse, true);
        var start = canvas.TranslatePoint(new(300, 250), window)!.Value;
        var pressed = new PointerPointProperties(Avalonia.Input.RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        canvas.RaiseEvent(new PointerPressedEventArgs(canvas, pointer, window, start, 0, pressed, KeyModifiers.None, 1));
        canvas.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, canvas, pointer, window, start + new Vector(40, 30), 1, pressed, KeyModifiers.None));
        canvas.RaiseEvent(new PointerReleasedEventArgs(canvas, pointer, window, start + new Vector(40, 30), 2, new PointerPointProperties(), KeyModifiers.None, MouseButton.Left));
        var after = model.Editor.Document;
        if (Math.Abs(after.View.CenterX - (before.View.CenterX - 40 / before.View.PixelsPerMeter)) > 1e-8 ||
            Math.Abs(after.View.CenterY - (before.View.CenterY + 30 / before.View.PixelsPerMeter)) > 1e-8 ||
            !after.Objects.SequenceEqual(before.Objects)) throw new InvalidOperationException("Left drag did not pan without changing geometry.");
    }
    private static void ClickCanvas(MainWindow window, CartesianCanvas canvas, Point point)
    {
        var pointer = new Avalonia.Input.Pointer(102, PointerType.Mouse, true);
        var position = canvas.TranslatePoint(point, window)!.Value;
        canvas.RaiseEvent(new PointerPressedEventArgs(canvas, pointer, window, position, 0,
            new PointerPointProperties(Avalonia.Input.RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1));
        canvas.RaiseEvent(new PointerReleasedEventArgs(canvas, pointer, window, position, 1, new PointerPointProperties(), KeyModifiers.None, MouseButton.Left));
    }
    private static async Task VerifyPropertiesAsync(MainWindow window, EditorViewModel model)
    {
        await Task.Delay(200);
        T Control<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void Enter(TextBox field) => field.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        var original = model.Editor.Document;
        var shape = model.Selected!;
        var x = Control<TextBox>("PropertyX"); var y = Control<TextBox>("PropertyY");
        var name = Control<TextBox>("PropertyName"); var vertex = Control<ComboBox>("PropertyVertex");
        Check(x.TranslatePoint(default, window)!.Value.Y == y.TranslatePoint(default, window)!.Value.Y, "X and Y must share a row.");
        Check(name.TranslatePoint(default, window)!.Value.Y == vertex.TranslatePoint(default, window)!.Value.Y, "Name and vertex must share a row.");
        var deleteTarget = Control<Button>($"DeleteObject_{shape.Id:N}");
        x.Focus(); x.Text = "12,5"; Enter(x);
        Check(ReferenceEquals(deleteTarget, Control<Button>($"DeleteObject_{shape.Id:N}")), "Confirmation replaced the inline delete target.");
        Check(model.Selected!.Vertices[0].X == 12.5, "Enter did not commit X.");
        Check(x.IsFocused, "Enter replaced the focused field.");
        y.Focus(); await Task.Delay(50);
        Check(y.IsFocused, "Blur prevented focus moving from X to Y.");
        model.Editor.Undo();
        Check(model.Selected!.Vertices[0] == shape.Vertices[0], "Enter and blur added duplicate undo operations.");
        y.Text = "15"; name.Focus(); await Task.Delay(50);
        Check(model.Selected!.Vertices[0].Y == 15, "Blur did not commit Y.");
        name.Text = "Renamed"; vertex.Focus(); await Task.Delay(50);
        Check(model.Selected!.Name == "Renamed", "Name was not saved on blur.");
        x.Focus(); x.Text = "invalid"; Enter(x);
        Check(model.Selected!.Vertices[0].X == 0, "Invalid text changed the geometry.");
        x.Text = "20"; Enter(x); vertex.SelectedIndex = 1;
        Check(x.Text == "100", "Vertex switch did not refresh coordinates.");
        x.Focus(); x.Text = "110"; name.Focus(); await Task.Delay(50);
        Check(model.Selected!.Vertices[1].X == 110 && model.Selected.Vertices[0].X == 20, "Vertex edit targeted the wrong vertex.");
        var second = model.Editor.Add(ShapeKind.Point, [new(5, 5)]);
        var delete = Control<Button>($"DeleteObject_{shape.Id:N}");
        delete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(model.Editor.Document.Objects.Length == 1 && model.Selected!.Id == second.Id, "Inline deletion removed or deselected the wrong object.");
        model.Editor.Undo();
        Check(model.Editor.Document.Objects.Length == 2, "Inline deletion cannot be undone.");
        model.Load(original); model.Editor.Select(shape.Id);
        Console.WriteLine("PROPERTIES PASS: compact layout, Enter, blur, focus, rename, validation, vertex selection and inline deletion.");
    }
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
}
