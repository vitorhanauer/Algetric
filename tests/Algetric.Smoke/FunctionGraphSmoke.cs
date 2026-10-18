using Algetric.Core;
using Algetric.Desktop;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

public sealed partial class SmokeApplication
{
    private static async Task VerifyFunctionGraphsAsync(MainWindow window, EditorViewModel model)
    {
        var original = model.Editor.Document;
        var toolbar = LayerControl<Button>(window, "FunctionGraphButton");
        CheckLayer(Equals(toolbar.Content, "f(x)") && ToolTip.GetTip(toolbar)?.ToString() == "Desenhar função (F)", "The toolbar must expose f(x) and its shortcut.");
        toolbar.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(100);
        var dialog = LayerDialog("Desenhar função");
        var expression = FunctionField(dialog, "FunctionExpression"); expression.Text = "sin(";
        SaveFunction(dialog); await Task.Delay(50);
        CheckLayer(dialog.IsVisible && ReferenceEquals(original, model.Editor.Document), "Invalid expressions must keep the modal open without changing the project.");
        expression.Text = "2*x^2 + sin(x)"; FunctionField(dialog, "FunctionMinimumX").Text = "-4"; FunctionField(dialog, "FunctionMaximumX").Text = "4";
        SaveCoverageScreenshot(dialog, "function-dialog"); SaveFunction(dialog); await Task.Delay(100);
        var shape = model.Selected!;
        CheckLayer(!dialog.IsVisible && shape.Kind == ShapeKind.FunctionGraph && shape.Function!.Expression == "2*x^2 + sin(x)", "Applying a function must add a formula object.");
        CheckLayer(shape.LayerId == model.Editor.ActiveLayerId && !LayerControl<ComboBox>(window, "PropertyVertex").IsEffectivelyVisible,
            "Functions must belong to the active layer and expose formula editing instead of vertex controls.");
        await VerifyFunctionEditingAsync(window, model, shape);
        model.Load(original); model.Editor.Select(original.Objects[0].Id);
        window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F }); await Task.Delay(100);
        var shortcutDialog = LayerDialog("Desenhar função"); shortcutDialog.Close();
        CheckLayer(model.Editor.Document.Objects.SequenceEqual(original.Objects), "Cancelling the function shortcut must preserve the document.");
        Console.WriteLine("FUNCTION GRAPH PASS: f(x), shortcut, expressions, validation, drawing, editing, discontinuities, visibility and history.");
    }
    private static async Task VerifyFunctionEditingAsync(MainWindow window, EditorViewModel model, GeometryObject shape)
    {
        var canvas = Field<CartesianCanvas>(window, "canvas"); var drawn = CaptureCanvas(canvas);
        canvas.FocusFunction(new FunctionDefinition("exp(x)", -10, 10));
        CheckLayer(model.Editor.Document.View.PixelsPerMeter > 10 && Math.Abs(model.Editor.Document.View.CenterY) < 10,
            "Exponential tails must not make the useful part of the graph disappear from view.");
        canvas.FocusFunction(shape.Function!);
        var visible = LayerControl<CheckBox>(window, $"ObjectVisible_{shape.Id:N}"); visible.IsChecked = false;
        CheckLayer(!drawn.SequenceEqual(CaptureCanvas(canvas)), "The function must be drawn and disappear when hidden.");
        visible.IsChecked = true;
        LayerControl<Button>(window, "EditFunctionGraph").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(100);
        var dialog = LayerDialog("Editar função");
        CheckLayer(FunctionField(dialog, "FunctionExpression").Text == shape.Function!.Expression, "Editing must restore the saved formula.");
        FunctionField(dialog, "FunctionExpression").Text = "1/(x-0.12345)"; SaveFunction(dialog); await Task.Delay(100);
        CheckLayer(model.Selected!.Id == shape.Id && model.Selected.Function!.GetSegments().Length >= 2,
            "Editing must preserve identity and split a curve around its pole.");
        CheckLayer(model.Editor.Document.View.PixelsPerMeter > 10, "A nearby pole must not make the entire graph too small to inspect.");
        SaveCoverageScreenshot(window, "function-graph");
        model.Editor.Undo(); CheckLayer(model.Selected!.Function!.Expression == shape.Function.Expression, "Function editing must undo in one step.");
        model.Editor.Redo(); CheckLayer(model.Selected!.Function!.Expression == "1/(x-0.12345)", "Function editing must redo.");
    }
    private static TextBox FunctionField(Window dialog, string name) => dialog.GetVisualDescendants().OfType<TextBox>().Single(field => field.Name == name);
    private static void SaveFunction(Window dialog) => dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "SaveFunctionGraph")
        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
