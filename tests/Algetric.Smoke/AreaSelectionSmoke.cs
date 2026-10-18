using Algetric.Core;
using Algetric.Desktop;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using System.Text.Json;

public sealed partial class SmokeApplication
{
    private static async Task VerifyAreaSelectionAsync(MainWindow window, EditorViewModel model)
    {
        var original = model.Editor.Document;
        var first = model.Editor.Add(ShapeKind.Point, [new(20, 20)]);
        var second = model.Editor.Add(ShapeKind.Point, [new(80, 80)]);
        model.Editor.Add(ShapeKind.Point, [new(200, 200)]);
        var before = model.Editor.Document; model.SetTool(EditorTool.Select);
        var canvas = Field<CartesianCanvas>(window, "canvas");
        var start = canvas.TranslatePoint(canvas.ToScreen(new(-20, -20)), window)!.Value;
        var end = canvas.TranslatePoint(canvas.ToScreen(new(120, 120)), window)!.Value;
        var pointer = new Avalonia.Input.Pointer(501, PointerType.Mouse, true);
        var pressed = new PointerPointProperties(Avalonia.Input.RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        canvas.RaiseEvent(new PointerPressedEventArgs(canvas, pointer, window, start, 0, pressed, KeyModifiers.None, 1));
        canvas.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, canvas, pointer, window, end, 1, pressed, KeyModifiers.None));
        ReleaseLayerPointer(window, canvas, pointer, end);
        using var state = JsonDocument.Parse(model.MapState());
        CheckLayer(state.RootElement.TryGetProperty("selectedIds", out var selected) && selected.GetArrayLength() == 3,
            "Dragging a rectangle must select every enclosed object, including points and polygons.");
        CheckLayer(ReferenceEquals(before, model.Editor.Document), "Area selection must not modify geometry or document history.");
        var firstRow = LayerControl<Button>(window, $"ObjectSelect_{first.Id:N}");
        var secondRow = LayerControl<Button>(window, $"ObjectSelect_{second.Id:N}");
        CheckLayer(firstRow.Background is ISolidColorBrush firstBrush && secondRow.Background is ISolidColorBrush secondBrush && firstBrush.Color == secondBrush.Color,
            "Every selected object must be highlighted in the layer list.");
        model.Load(original); model.Editor.Select(original.Objects[0].Id); await Task.Delay(50);
        Console.WriteLine("AREA SELECTION PASS: drag selection, multiple highlights, map synchronization and unchanged geometry.");
    }
    private static void VerifyToolbar(MainWindow window)
    {
        var buttons = window.GetVisualDescendants().OfType<Button>().ToArray();
        string? Label(Button button) => Avalonia.Automation.AutomationProperties.GetName(button);
        CheckLayer(!buttons.Any(button => Label(button) == "Segmento"), "The segment toolbar button must be removed.");
        CheckLayer(Equals(buttons.Single(button => Label(button) == "Linha").Content, "╱"), "The line button must use the former segment icon.");
        CheckLayer(buttons.Single(button => Label(button) == "Selecionar").Content is Avalonia.Controls.Shapes.Path,
            "Select must use a vector mouse pointer icon.");
    }
}
