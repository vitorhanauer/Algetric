using Algetric.Core;
using Algetric.Desktop;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NetTopologySuite.Geometries;

public sealed partial class SmokeApplication
{
    private static async Task VerifyCoverageObstructionsAsync(MainWindow window, EditorViewModel model, GeometryObject polygon)
    {
        var inner = model.Editor.Add(ShapeKind.Polygon, [new(20, 20), new(35, 20), new(35, 35), new(20, 35)]);
        var second = model.Editor.Add(ShapeKind.Polygon, [new(65, 65), new(80, 65), new(80, 80), new(65, 80)]);
        var before = model.Editor.Document;
        var dialog = await OpenCoverageDialogAsync(window, polygon);
        var selector = dialog.GetVisualDescendants().OfType<ListBox>().Single(control => control.Name == "CoverageObstructions");
        CheckLayer(selector.SelectedItems!.Count == 0, "Obstructions must be optional and initially empty.");
        CheckLayer(selector.ItemCount == before.Objects.Count(shape => shape.Kind == ShapeKind.Polygon) - 1,
            "All other polygons, including external ones, must be available as obstructions.");
        var item = selector.Items[0]!; selector.SelectedItems.Add(item); selector.SelectedItems.Remove(item);
        CheckLayer(selector.SelectedItems.Count == 0, "The last obstruction can be deselected.");
        selector.SelectAll();
        SaveCoverageScreenshot(dialog, "coverage-obstructions");
        dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Gerar caminho"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for (var attempt = 0; attempt < 60 && dialog.IsVisible; attempt++) await Task.Delay(50);
        CheckLayer(!dialog.IsVisible && model.Editor.Document.Objects.Length == before.Objects.Length + 1,
            "Multiple internal and external obstructions must generate a path without an error.");
        var route = model.Editor.Document.Objects.Last();
        var line = new LineString(route.Vertices.Select(point => new Coordinate(point.X, point.Y)).ToArray());
        CheckLayer(new[] { inner, second }.All(shape => GeometryRules.ToGeometry(shape).Buffer(-1e-7).Disjoint(line)),
            "The path must avoid both selected internal obstructions.");
        model.Editor.Undo();
        CheckLayer(model.Editor.Document.Objects.SequenceEqual(before.Objects), "A path with obstructions must undo in one operation.");
        Console.WriteLine("COVERAGE OBSTRUCTIONS PASS: optional multiselect, multiple obstacles, external filtering and undo.");
    }
}
