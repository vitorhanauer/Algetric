using System.Text.Json;
using Algetric.Core;
using Algetric.Desktop;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;

public sealed partial class SmokeApplication
{
    private static async Task VerifyPolygonVisibilityAsync(MainWindow window, EditorViewModel model)
    {
        var original = model.Editor.Document; var polygon = original.Objects.Single();
        var sibling = model.Editor.Add(ShapeKind.Polygon, [new(150, 0), new(180, 0), new(180, 30), new(150, 30)]);
        model.Editor.Select(polygon.Id);
        var canvas = Field<CartesianCanvas>(window, "canvas");
        var checkbox = LayerControl<CheckBox>(window, $"ObjectVisible_{polygon.Id:N}");
        CheckLayer(checkbox.IsChecked == true, "New polygons must start visible.");
        var visibleImage = CaptureCanvas(canvas);
        checkbox.IsChecked = false; await Task.Delay(50);
        CheckLayer(!model.Editor.Document.Objects.First(shape => shape.Id == polygon.Id).Visible &&
            model.Editor.Document.Objects.First(shape => shape.Id == sibling.Id).Visible, "The checkbox must hide only its own polygon.");
        CheckLayer(!visibleImage.SequenceEqual(CaptureCanvas(canvas)), "Hiding a polygon must remove its drawing from the canvas.");
        CheckLayer(LayerControl<Button>(window, $"ObjectSelect_{polygon.Id:N}").IsVisible, "Hidden polygons must remain in the layer list.");
        using (var state = JsonDocument.Parse(model.MapState()))
            CheckLayer(state.RootElement.GetProperty("objects").EnumerateArray().All(shape => shape.GetProperty("id").GetGuid() != polygon.Id),
                "Hidden polygons must be removed from the map snapshot.");
        VerifyHiddenObjectCannotBePicked(window, model, canvas, new(50, 50));
        model.Editor.Undo(); CheckLayer(checkbox.IsChecked == true, "Undo must restore polygon visibility and its checkbox.");
        model.Editor.Redo(); CheckLayer(checkbox.IsChecked == false, "Redo must hide the polygon again.");
        checkbox.IsChecked = true;
        CheckLayer(model.Editor.Document.Objects.First(shape => shape.Id == polygon.Id).Visible, "The checkbox must show the polygon again.");
        model.Load(original); model.Editor.Select(polygon.Id);
        Console.WriteLine("POLYGON VISIBILITY PASS: individual checkbox, rendering, hidden hit tests, map filtering and undo/redo.");
    }

    private static void VerifyHiddenObjectCannotBePicked(MainWindow window, EditorViewModel model, CartesianCanvas canvas, LocalPoint point)
    {
        var position = canvas.TranslatePoint(canvas.ToScreen(point), window)!.Value;
        var pointer = new Avalonia.Input.Pointer(801, PointerType.Mouse, true);
        canvas.RaiseEvent(new PointerPressedEventArgs(canvas, pointer, window, position, 0,
            new PointerPointProperties(Avalonia.Input.RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1));
        ReleaseLayerPointer(window, canvas, pointer, position);
        CheckLayer(model.Editor.SelectedId is null, "Clicking a hidden object must not select it.");
        canvas.RaiseEvent(new PointerPressedEventArgs(canvas, pointer, window, position, 1,
            new PointerPointProperties(Avalonia.Input.RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed), KeyModifiers.None, 1));
        canvas.RaiseEvent(new PointerReleasedEventArgs(canvas, pointer, window, position, 2, new PointerPointProperties(), KeyModifiers.None, MouseButton.Right));
        CheckLayer(canvas.ContextMenu is null, "Right-clicking a hidden object must not expose its canvas context menu.");
    }

    private static byte[] CaptureCanvas(CartesianCanvas canvas)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)canvas.Bounds.Width, (int)canvas.Bounds.Height));
        bitmap.Render(canvas);
        using var stream = new MemoryStream(); bitmap.Save(stream, PngBitmapEncoderOptions.Default); return stream.ToArray();
    }
}
