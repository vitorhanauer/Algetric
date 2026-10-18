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
    private static async Task VerifyThemesAsync(MainWindow window, EditorViewModel model)
    {
        CheckLayer(Current!.RequestedThemeVariant == ThemeVariant.Default, "The application must follow the operating system theme by default.");
        ThemeItem(window, "LightTheme").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await Task.Delay(50);
        var sidebar = LayerControl<Border>(window, "SidebarBorder");
        var light = ((ISolidColorBrush)sidebar.Background!).Color;
        ThemeItem(window, "DarkTheme").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await Task.Delay(100);
        CheckLayer(window.ActualThemeVariant == ThemeVariant.Dark && ((ISolidColorBrush)sidebar.Background!).Color != light,
            "Dark theme must update both controls and sidebar surfaces.");
        var dialog = await OpenLayerEditorAsync(window, model.Editor.Document.Layers[0].Id);
        CheckLayer(dialog.ActualThemeVariant == ThemeVariant.Dark, "Dialogs must use the active application theme.");
        dialog.Close();
        SaveThemeScreenshot(window, "dark");
        ThemeItem(window, "LightTheme").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await Task.Delay(100);
        CheckLayer(window.ActualThemeVariant == ThemeVariant.Light && ((ISolidColorBrush)sidebar.Background!).Color == light,
            "Light theme must restore the original surfaces.");
        SaveThemeScreenshot(window, "light");
        ThemeItem(window, "SystemTheme").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await Task.Delay(50);
        CheckLayer(Current!.RequestedThemeVariant == ThemeVariant.Default, "The user must be able to return to the system theme.");
        Console.WriteLine("THEME PASS: dark/light switching, surfaces and dialogs.");
    }
    private static MenuItem ThemeItem(MainWindow window, string name)
        => window.GetVisualDescendants().OfType<Menu>().Single().Items.OfType<MenuItem>()
            .SelectMany(item => item.Items.OfType<MenuItem>()).Single(item => item.Name == name);
    private static void SaveThemeScreenshot(MainWindow window, string theme)
    {
        Directory.CreateDirectory("artifacts/smoke");
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
        bitmap.Render(window); bitmap.Save($"artifacts/smoke/theme-{theme}.png", PngBitmapEncoderOptions.Default);
    }
}
