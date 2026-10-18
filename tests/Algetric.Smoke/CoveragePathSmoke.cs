using Algetric.Core;
using Algetric.Desktop;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

public sealed partial class SmokeApplication
{
    private static async Task<Window> OpenCoverageDialogAsync(MainWindow window, GeometryObject polygon)
    {
        var canvas = Field<CartesianCanvas>(window, "canvas");
        var point = canvas.TranslatePoint(canvas.ToScreen(new(50, 50)), window)!.Value;
        var pointer = new Avalonia.Input.Pointer(601, PointerType.Mouse, true);
        canvas.RaiseEvent(new PointerPressedEventArgs(canvas, pointer, window, point, 0,
            new PointerPointProperties(Avalonia.Input.RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed), KeyModifiers.None, 1));
        canvas.RaiseEvent(new PointerReleasedEventArgs(canvas, pointer, window, point, 1, new PointerPointProperties(), KeyModifiers.None, MouseButton.Right));
        canvas.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = InputElement.ContextRequestedEvent });
        await Task.Delay(50);
        CheckLayer(canvas.ContextMenu?.IsOpen == true, "Right-clicking a polygon must open its context menu.");
        var menu = canvas.ContextMenu!;
        var generate = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Gerar caminho de pontos…"));
        generate.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); menu.Close(); await Task.Delay(100);
        var dialog = LayerDialog("Gerar caminho de pontos");
        var selector = dialog.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "CoveragePolygon");
        CheckLayer(Equals(selector.SelectedValue, polygon.Id), "The context polygon must be preselected in the modal.");
        return dialog;
    }
    private static async Task VerifyCoveragePathAsync(MainWindow window, EditorViewModel model)
    {
        var original = model.Editor.Document; var polygon = original.Objects.First(shape => shape.Kind == ShapeKind.Polygon);
        model.Editor.Add(ShapeKind.Point, [new(10, 10)]); model.Editor.Add(ShapeKind.Point, [new(90, 90)]);
        model.Editor.Add(ShapeKind.Polygon, [new(200, 200), new(250, 200), new(250, 250), new(200, 250)]);
        var before = model.Editor.Document;
        var dialog = await OpenCoverageDialogAsync(window, polygon);
        var polygonChoice = dialog.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "CoveragePolygon");
        polygonChoice.SelectedIndex = 1;
        CheckLayer(dialog.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "CoverageStartX").Text == "200", "Changing polygons must update the default endpoints.");
        dialog.Close();
        CheckLayer(ReferenceEquals(before, model.Editor.Document), "Cancelling the path modal must preserve the document.");
        dialog = await OpenCoverageDialogAsync(window, polygon);
        dialog.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "CoverageStartPoint").SelectedIndex = 1;
        dialog.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "CoverageEndPoint").SelectedIndex = 2;
        var width = dialog.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "CoverageWidth");
        SaveCoverageScreenshot(dialog, "coverage-dialog");
        width.Text = "0";
        var generate = dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Gerar caminho"));
        generate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(150);
        CheckLayer(ReferenceEquals(before, model.Editor.Document) && dialog.IsVisible, "Invalid width must keep the modal open without creating a path.");
        width.Text = "10";
        dialog.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "CoverageOverlap").Text = "25";
        generate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for (var attempt = 0; attempt < 60 && dialog.IsVisible; attempt++) await Task.Delay(50);
        CheckLayer(!dialog.IsVisible && model.Editor.Document.Objects.Length == before.Objects.Length + 1, "Generating must create one path and close the modal.");
        var path = model.Editor.Document.Objects.Last();
        CheckLayer(path.Kind == ShapeKind.CoveragePath && path.LayerId == polygon.LayerId && path.Vertices[0] == new LocalPoint(10, 10)
            && path.Vertices[^1] == new LocalPoint(90, 90), "The path must use the selected points and polygon layer.");
        await Task.Delay(100);
        CheckLayer(LayerControl<Button>(window, $"ObjectSelect_{path.Id:N}").IsEffectivelyVisible, "The generated path must appear in the layer list.");
        SaveCoverageScreenshot(window, "coverage-result");
        model.Editor.Undo(); CheckLayer(model.Editor.Document.Objects.SequenceEqual(before.Objects), "Path generation must undo in one operation.");
        model.Load(original); model.Editor.Select(polygon.Id);
        Console.WriteLine("COVERAGE PATH PASS: polygon menu, preselection, point choices, validation, real native route, cancel and undo.");
    }
    private static void SaveCoverageScreenshot(Window window, string name)
    {
        Directory.CreateDirectory("artifacts/smoke");
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
        bitmap.Render(window); bitmap.Save($"artifacts/smoke/{name}.png", PngBitmapEncoderOptions.Default);
    }
}
