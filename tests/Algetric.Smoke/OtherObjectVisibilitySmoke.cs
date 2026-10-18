using System.Text.Json;
using Algetric.Core;
using Algetric.Desktop;
using Avalonia.Controls;
using Avalonia.Interactivity;

public sealed partial class SmokeApplication
{
    private static async Task VerifyOtherObjectVisibilityAsync(MainWindow window, EditorViewModel model)
    {
        var original = model.Editor.Document; var canvas = Field<CartesianCanvas>(window, "canvas");
        foreach (var kind in new[] { ShapeKind.Point, ShapeKind.Segment, ShapeKind.Polyline, ShapeKind.CoveragePath })
        {
            LocalPoint[] vertices = kind switch
            {
                ShapeKind.Point => [new(200, 100)],
                ShapeKind.Segment => [new(200, 100), new(250, 100)],
                _ => [new(200, 100), new(220, 130), new(250, 100)]
            };
            var shape = kind == ShapeKind.CoveragePath ? model.Editor.AddCoveragePath(original.Objects[0].Id, vertices) : model.Editor.Add(kind, vertices);
            await VerifyObjectVisibilityAsync(window, model, canvas, shape);
            model.Load(original); model.Editor.Select(original.Objects[0].Id);
        }
        Console.WriteLine("OBJECT VISIBILITY PASS: points, segments, polylines and coverage paths; rendering, hit testing, map and undo/redo.");
    }

    private static async Task VerifyObjectVisibilityAsync(MainWindow window, EditorViewModel model, CartesianCanvas canvas, GeometryObject shape)
    {
        var checkbox = LayerControl<CheckBox>(window, $"ObjectVisible_{shape.Id:N}");
        CheckLayer(checkbox.IsChecked == true, $"{shape.Kind} must have a checked visibility control.");
        var image = CaptureCanvas(canvas); checkbox.IsChecked = false; await Task.Delay(50);
        if (shape.Kind == ShapeKind.CoveragePath) await VerifyVisibilityTranslationAsync(window, checkbox);
        CheckLayer(!image.SequenceEqual(CaptureCanvas(canvas)), $"Hiding {shape.Kind} must remove its drawing.");
        CheckLayer(!model.Editor.Document.Objects.Single(item => item.Id == shape.Id).Visible &&
            model.Editor.Document.Objects.Where(item => item.Id != shape.Id).All(item => item.Visible), "Hiding an object must preserve the visibility of its neighbors.");
        CheckLayer(LayerControl<Button>(window, $"ObjectSelect_{shape.Id:N}").IsVisible, "A hidden object must remain in the layer list.");
        using (var state = JsonDocument.Parse(model.MapState()))
            CheckLayer(state.RootElement.GetProperty("objects").EnumerateArray().All(item => item.GetProperty("id").GetGuid() != shape.Id),
                "Hidden objects must be omitted from the map.");
        VerifyHiddenObjectCannotBePicked(window, model, canvas, shape.Vertices[0]);
        model.Editor.Undo(); CheckLayer(checkbox.IsChecked == true, "Undo must show the object and update its checkbox.");
        model.Editor.Redo(); CheckLayer(checkbox.IsChecked == false, "Redo must hide the object and update its checkbox.");
        checkbox.IsChecked = true;
        CheckLayer(model.Editor.Document.Objects.Single(item => item.Id == shape.Id).Visible, "Checking visibility must show the object again.");
    }

    private static async Task VerifyVisibilityTranslationAsync(MainWindow window, CheckBox checkbox)
    {
        CheckLayer(ToolTip.GetTip(checkbox)?.ToString() == "Mostrar ou ocultar objeto", "Visibility must use a generic Portuguese tooltip.");
        try
        {
            LanguageChoice(window, "EnglishLanguage").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Task.Delay(50);
            CheckLayer(ToolTip.GetTip(checkbox)?.ToString() == "Show or hide object" && checkbox.IsChecked == false,
                "Changing language must translate visibility controls and preserve hidden objects.");
        }
        finally { LanguageChoice(window, "PortugueseLanguage").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Task.Delay(50); }
    }
}
