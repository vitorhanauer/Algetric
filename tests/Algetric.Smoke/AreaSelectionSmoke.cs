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
