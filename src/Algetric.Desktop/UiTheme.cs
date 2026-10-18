using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace Algetric.Desktop;

internal static class UiTheme
{
    public static void Initialize(Application app)
    {
        if (app.Resources.ContainsKey("AlgetricThemeInstalled")) return;
        app.Resources["AlgetricThemeInstalled"] = true;
        var resources = new ResourceDictionary();
        resources.ThemeDictionaries[ThemeVariant.Default] = Palette(false);
        resources.ThemeDictionaries[ThemeVariant.Light] = Palette(false);
        resources.ThemeDictionaries[ThemeVariant.Dark] = Palette(true);
        app.Resources.MergedDictionaries.Add(resources);
    }
    private static ResourceDictionary Palette(bool dark) => new()
    {
        ["WarningSurface"] = Brush.Parse(dark ? "#493C1F" : "#FFF4CE"),
        ["WarningBorder"] = Brush.Parse(dark ? "#AA8738" : "#E2BF67"),
        ["WarningText"] = Brush.Parse(dark ? "#FFF0BD" : "#624500"),
        ["AppSurface"] = Brush.Parse(dark ? "#20242C" : "#F6F9FC"),
        ["PanelSurface"] = Brush.Parse(dark ? "#272C36" : "#FFFFFF"),
        ["PanelBorder"] = Brush.Parse(dark ? "#414958" : "#DCE5EF"),
        ["HeadingText"] = Brush.Parse(dark ? "#E0E8FF" : "#101B4F"),
        ["ActiveTool"] = Brush.Parse(dark ? "#205064" : "#D7F1FB"),
        ["ActiveLayer"] = Brush.Parse(dark ? "#30435C" : "#E8F2FD"),
        ["SelectedObject"] = Brush.Parse(dark ? "#324F73" : "#D7EAFF"),
        ["CanvasSurface"] = Brush.Parse(dark ? "#191E27" : "#FFFFFF"),
        ["CanvasGrid"] = Brush.Parse(dark ? "#2B3442" : "#E9EEF4"),
        ["CanvasAxis"] = Brush.Parse(dark ? "#A7B7D0" : "#46536A"),
        ["DangerText"] = Brush.Parse(dark ? "#FF9EAA" : "#8A3944"),
        ["DragSurface"] = Brush.Parse(dark ? "#30435C" : "#E5F1FF"),
        ["DragText"] = Brush.Parse(dark ? "#E0E8FF" : "#15365D"),
        ["DropSurface"] = Brush.Parse(dark ? "#283D55" : "#F0F7FF")
    };
    public static void Bind<T>(AvaloniaObject target, StyledProperty<T> property, string key)
        => target.Bind(property, new DynamicResourceExtension(key));
    public static IBrush BrushFor(Control control, string key) => (IBrush)control.FindResource(control.ActualThemeVariant, key)!;
    public static void SetHighlight(Control control, StyledProperty<IBrush?> property, bool selected, string key)
    {
        if (selected) Bind(control, property, key);
        else control.SetValue(property, Brushes.Transparent);
    }
}
