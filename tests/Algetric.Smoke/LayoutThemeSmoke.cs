using Algetric.Desktop;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;

public sealed partial class SmokeApplication
{
    private static async Task VerifyFixedPropertiesAsync(MainWindow window, EditorViewModel model)
    {
        var sidebar = LayerControl<Grid>(window, "Sidebar");
        var section = LayerControl<Border>(window, "PropertiesSection");
        var scroll = LayerControl<ScrollViewer>(window, "LayerScroll");
        var original = model.Editor.Document;
        var height = section.Bounds.Height;
        CheckLayer(height == 280 && section.Bounds.Bottom == sidebar.Bounds.Height, "Properties must occupy a fixed 280-pixel section at the bottom.");
        for (var index = 0; index < 20; index++) model.Editor.AddLayer($"Extra {index}", "#009CA6");
        model.Editor.Select(null); await Task.Delay(100);
        CheckLayer(section.Bounds.Height == height && section.Bounds.Bottom == sidebar.Bounds.Height && scroll.Extent.Height > scroll.Viewport.Height,
            "Long layer lists must scroll above fixed properties, even without a selection.");
        var initialHeight = window.Height;
        window.Height = 650; await Task.Delay(100);
        CheckLayer(section.Bounds.Height == height && section.Bounds.Bottom == sidebar.Bounds.Height, "Resizing must keep properties fixed at the bottom.");
        window.Height = initialHeight; model.Load(original); model.Editor.Select(original.Objects[0].Id);
        await Task.Delay(100);
        Console.WriteLine("SIDEBAR PASS: fixed bottom properties, independent layer scrolling, resize and no selection.");
    }
}
