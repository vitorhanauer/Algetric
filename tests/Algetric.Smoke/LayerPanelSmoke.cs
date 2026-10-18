using Algetric.Core;
using Algetric.Desktop;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using System.Reflection;
using System.Text.Json;

public sealed partial class SmokeApplication
{
    private static T LayerControl<T>(MainWindow window, string name) where T : Control
        => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    private static void CheckLayer(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static async Task VerifyLayerPanelAsync(MainWindow window, EditorViewModel model)
    {
        await Task.Delay(50);
        var add = LayerControl<Button>(window, "AddLayer");
        CheckLayer(add.Content is string text && text == "+", "The layer heading must contain an icon-only add button.");
        var original = model.Editor.Document; var shape = original.Objects[0];
        var source = shape.LayerId; var target = original.Layers[1].Id;
        var second = model.Editor.Add(ShapeKind.Point, [new(20, 30)]);
        var row = LayerControl<Grid>(window, $"Object_{shape.Id:N}");
        var sourceList = LayerControl<StackPanel>(window, $"LayerObjects_{source:N}");
        CheckLayer(row.Parent == sourceList && sourceList.Margin.Left > 0, "Objects must be indented inside their layer.");
        await VerifyLayerTransferAsync(window, model, shape, target);
        await VerifyCancelledLayerDropsAsync(window, model, shape.Id, target);
        LayerControl<Button>(window, $"SelectLayer_{target:N}").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        model.Add(ShapeKind.Point, [new(40, 50)]);
        CheckLayer(model.Selected!.LayerId == target, "Clicking a layer must select it for new objects.");
        CheckLayer(model.Editor.Document.Objects.Any(item => item.Id == second.Id), "Dragging removed a neighboring object.");
        model.Load(original); model.Editor.Select(shape.Id);
        Console.WriteLine("LAYERS PASS: indented objects, active layer, drag preview, valid/invalid drops, cancellation and undo/redo.");
    }
    private static async Task VerifyLayerTransferAsync(MainWindow window, EditorViewModel model, GeometryObject shape, Guid target)
    {
        var pointer = await StartLayerDragAsync(window, shape.Id);
        var row = LayerControl<Grid>(window, $"Object_{shape.Id:N}");
        var ghost = LayerControl<Border>(window, "LayerDragPreview");
        CheckLayer(!row.IsVisible && ghost.IsVisible, "Dragging must remove the list row and show a floating preview.");
        CheckLayer(model.Editor.Document.Objects.First(item => item.Id == shape.Id).LayerId == shape.LayerId, "Dragging must not change the project before dropping.");
        var destination = LayerControl<Border>(window, $"Layer_{target:N}").TranslatePoint(new Point(80, 16), window)!.Value;
        var tree = LayerControl<Control>(window, "LayerTree");
        MoveLayerPointer(window, tree, pointer, destination);
        CheckLayer(Math.Abs(Canvas.GetLeft(ghost) - destination.X - 12) < 0.01, "The dragged preview must follow the pointer.");
        await Task.Delay(50);
        Directory.CreateDirectory("artifacts/smoke");
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height)))
        { bitmap.Render(window); bitmap.Save("artifacts/smoke/layer-drag.png", PngBitmapEncoderOptions.Default); }
        ReleaseLayerPointer(window, tree, pointer, destination);
        CheckLayer(model.Editor.Document.Objects.First(item => item.Id == shape.Id).LayerId == target, "Dropping on another layer must transfer the object.");
        CheckLayer(row.IsVisible && !ghost.IsVisible && row.Parent == LayerControl<StackPanel>(window, $"LayerObjects_{target:N}"), "The dropped row must appear in the destination layer.");
        CheckLayer(model.Editor.SelectedId == shape.Id, "Moving layers must preserve selection.");
        using (var state = JsonDocument.Parse(model.MapState()))
        {
            var mapped = state.RootElement.GetProperty("objects").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == shape.Id);
            CheckLayer(mapped.GetProperty("color").GetString() == model.Editor.Document.Layers.First(layer => layer.Id == target).Color, "Map synchronization must use the destination layer style.");
        }
        model.Editor.Undo();
        CheckLayer(model.Editor.Document.Objects.First(item => item.Id == shape.Id).LayerId == shape.LayerId, "Layer transfer must undo in one step.");
        model.Editor.Redo();
        CheckLayer(model.Editor.Document.Objects.First(item => item.Id == shape.Id).LayerId == target, "Layer transfer must redo.");
        await Task.Delay(30);
    }
    private static async Task VerifyCancelledLayerDropsAsync(MainWindow window, EditorViewModel model, Guid id, Guid source)
    {
        var before = model.Editor.Document;
        var tree = LayerControl<Control>(window, "LayerTree");
        var pointer = await StartLayerDragAsync(window, id);
        var outside = Field<CartesianCanvas>(window, "canvas").TranslatePoint(new Point(200, 200), window)!.Value;
        MoveLayerPointer(window, tree, pointer, outside); ReleaseLayerPointer(window, tree, pointer, outside);
        CheckLayer(ReferenceEquals(before, model.Editor.Document) && LayerControl<Grid>(window, $"Object_{id:N}").IsVisible, "Dropping outside layers must restore the row without changing history.");
        pointer = await StartLayerDragAsync(window, id);
        var sameLayer = LayerControl<Border>(window, $"Layer_{source:N}").TranslatePoint(new Point(80, 16), window)!.Value;
        ReleaseLayerPointer(window, tree, pointer, sameLayer);
        CheckLayer(ReferenceEquals(before, model.Editor.Document), "Dropping on the original layer must not add history.");
        pointer = await StartLayerDragAsync(window, id);
        tree.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        CheckLayer(!LayerControl<Border>(window, "LayerDragPreview").IsVisible && LayerControl<Grid>(window, $"Object_{id:N}").IsVisible, "Escape must restore the dragged row.");
        pointer = await StartLayerDragAsync(window, id); pointer.Capture(null);
        CheckLayer(!LayerControl<Border>(window, "LayerDragPreview").IsVisible && ReferenceEquals(before, model.Editor.Document), "Capture loss must cancel the drag.");
        pointer = await StartLayerDragAsync(window, id);
        typeof(WindowBase).GetMethod("HandleDeactivated", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        CheckLayer(!LayerControl<Border>(window, "LayerDragPreview").IsVisible && LayerControl<Grid>(window, $"Object_{id:N}").IsVisible, "Window deactivation must restore the dragged row.");
    }
    private static async Task<IPointer> StartLayerDragAsync(MainWindow window, Guid id)
    {
        var button = LayerControl<Button>(window, $"ObjectSelect_{id:N}");
        var position = button.TranslatePoint(new Point(12, 12), window)!.Value;
        var pointer = new Avalonia.Input.Pointer(201, PointerType.Mouse, true);
        var pressed = new PointerPointProperties(Avalonia.Input.RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        button.RaiseEvent(new PointerPressedEventArgs(button, pointer, window, position, 0, pressed, KeyModifiers.None, 1));
        MoveLayerPointer(window, button, pointer, position + new Vector(8, 8));
        await Task.Delay(50); return pointer;
    }
    private static void MoveLayerPointer(MainWindow window, Control source, IPointer pointer, Point position)
        => source.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, source, pointer, window, position, 1,
            new PointerPointProperties(Avalonia.Input.RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
    private static void ReleaseLayerPointer(MainWindow window, Control source, IPointer pointer, Point position)
        => source.RaiseEvent(new PointerReleasedEventArgs(source, pointer, window, position, 2, new PointerPointProperties(), KeyModifiers.None, MouseButton.Left));
}
