using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Algetric.Desktop;

public sealed partial class MainWindow
{
    private MenuItem BuildLanguageMenu()
    {
        var menu = new MenuItem { Name = "LanguageMenu", [!MenuItem.HeaderProperty] = UiText.Binding("Idioma") };
        menu.Items.Add(LanguageItem("Português", "PortugueseLanguage", AppLanguage.Portuguese));
        menu.Items.Add(LanguageItem("English", "EnglishLanguage", AppLanguage.English));
        return menu;
    }
    private MenuItem LanguageItem(string label, string name, AppLanguage language)
    {
        var item = Item(label, () => { UiText.Current.Select(language); return Task.CompletedTask; });
        item.Name = name;
        item.Bind(MenuItem.IconProperty, new Binding(nameof(LanguagePreferences.Language))
        {
            Source = UiText.Current, Converter = new FuncValueConverter<AppLanguage, string>(selected => selected == language ? "✓" : "")
        });
        return item;
    }
    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs args)
    {
        Refresh(); canvas.InvalidateVisual();
    }
    private void RefreshTooltips()
    {
        foreach (var (button, tool) in tools)
        {
            var shortcut = EditorShortcuts.Tools.Single(value => value.Tool == tool);
            var label = UiText.T(shortcut.Label);
            var hint = $"{label} ({shortcut.Key})";
            if (tool == EditorTool.Move) hint += UiText.T(" • segure Espaço para mover temporariamente");
            if (tool is EditorTool.Polyline or EditorTool.Polygon) hint += UiText.T(" • Enter ou duplo clique para concluir");
            ToolTip.SetTip(button, hint); AutomationProperties.SetName(button, label);
        }
    }
}
