using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace Algetric.Desktop;

internal static class LayerColorPicker
{
    public static ColorView Create(string initialColor) => new()
    {
        Name = "LayerColorPicker", Color = Color.Parse(initialColor),
        ColorSpectrumShape = ColorSpectrumShape.Box, ColorSpectrumComponents = ColorSpectrumComponents.HueSaturation,
        IsAlphaEnabled = false, IsAlphaVisible = false, IsColorPaletteVisible = false,
        IsColorModelVisible = false, IsColorComponentsVisible = false, IsHexInputVisible = false,
        IsAccentColorsVisible = false, HorizontalAlignment = HorizontalAlignment.Left,
        Template = new FuncControlTemplate<ColorView>((picker, _) => BuildContent(picker))
    };

    private static Grid BuildContent(ColorView picker)
    {
        var spectrum = new ColorSpectrum
        {
            Width = 200, Height = 200, Shape = ColorSpectrumShape.Box,
            Components = ColorSpectrumComponents.HueSaturation
        };
        spectrum.Bind(ColorSpectrum.HsvColorProperty, new Binding(nameof(ColorView.HsvColor)) { Source = picker, Mode = BindingMode.TwoWay });
        AutomationProperties.SetName(spectrum, "Cor e saturação");
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,12,Auto,12,Auto") };
        layout.Children.Add(CreateBrightnessSlider(picker));
        Grid.SetColumn(spectrum, 2); layout.Children.Add(spectrum);
        var preview = CreatePreview(picker);
        Grid.SetColumn(preview, 4); layout.Children.Add(preview);
        return layout;
    }

    private static ColorSlider CreateBrightnessSlider(ColorView picker)
    {
        var slider = new ColorSlider
        {
            Width = 24, Height = 200, Orientation = Orientation.Vertical,
            ColorModel = ColorModel.Hsva, ColorComponent = ColorComponent.Component3,
            IsAlphaVisible = false, IsPerceptive = true
        };
        slider.Bind(ColorSlider.HsvColorProperty, new Binding(nameof(ColorView.HsvColor)) { Source = picker, Mode = BindingMode.TwoWay });
        AutomationProperties.SetName(slider, "Brilho");
        return slider;
    }

    private static Border CreatePreview(ColorView picker)
    {
        var preview = new Border
        {
            Name = "LayerColorPreview", Width = 48, Height = 48,
            VerticalAlignment = VerticalAlignment.Top, CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(picker.Color)
        };
        picker.ColorChanged += (_, args) => preview.Background = new SolidColorBrush(args.NewColor);
        AutomationProperties.SetName(preview, "Cor selecionada");
        return preview;
    }
}
