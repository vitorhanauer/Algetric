using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Algetric.Core;
using Algetric.Infrastructure;
using System.Globalization;

namespace Algetric.Desktop;

internal static class Dialogs
{
    public static string Number(double value) => value.ToString("G12", CultureInfo.InvariantCulture);
    public static double Parse(TextBox field)
    {
        if (!double.TryParse(field.Text?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new ArgumentException("Informe um número válido.");
        return value;
    }
    public static TextBox Field(StackPanel panel, string label, string value)
    {
        panel.Children.Add(new TextBlock { Text = label, FontSize = 12 });
        var input = new TextBox { Text = value }; panel.Children.Add(input); return input;
    }
    public static async Task Message(Window owner, string title, string message)
        => await Choose(owner, title, message, ["OK"]);
    public static async Task<string?> Choose(Window owner, string title, string message, string[] answers)
    {
        var panel = new StackPanel { Spacing = 16, Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        var window = Window(title, panel, 520); var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var answer in answers)
        { var button = new Button { Content = answer, Margin = new Thickness(4) }; button.Click += (_, _) => window.Close(answer); buttons.Children.Add(button); }
        panel.Children.Add(buttons); return await window.ShowDialog<string?>(owner);
    }
    public static async Task EditOrigin(Window owner, EditorViewModel model)
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(20) };
        var latitude = Field(panel, "Latitude (graus)", Number(model.Editor.Document.Origin.Latitude));
        var longitude = Field(panel, "Longitude (graus)", Number(model.Editor.Document.Origin.Longitude));
        var window = Window("Origem geográfica", panel);
        AddSubmit(panel, window, () => { model.Editor.SetOrigin(new(Parse(latitude), Parse(longitude))); return Task.CompletedTask; });
        await window.ShowDialog(owner);
    }
    public static async Task EditLayer(Window owner, EditorViewModel model, Layer? layer)
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(20) };
        var name = Field(panel, "Nome", layer?.Name ?? "Nova camada"); name.Name = "LayerName";
        panel.Children.Add(new TextBlock { Text = "Cor", FontSize = 12 });
        var color = LayerColorPicker.Create(layer?.Color ?? "#009CA6");
        panel.Children.Add(color);
        panel.Children.Add(new TextBlock { Text = "Escolha no quadrado; ajuste o brilho na barra.", FontSize = 12 });
        var window = Window("Camada", panel);
        AddSubmit(panel, window, () =>
        {
            var selected = color.Color; var hex = $"#{selected.R:X2}{selected.G:X2}{selected.B:X2}";
            if (layer is null) model.Editor.AddLayer(name.Text ?? "", hex);
            else model.Editor.UpdateLayer(layer with { Name = name.Text ?? "", Color = hex });
            return Task.CompletedTask;
        });
        if (layer is not null) AddLayerDeletion(panel, window, model, layer);
        await window.ShowDialog(owner);
    }
    private static void AddLayerDeletion(StackPanel panel, Window window, EditorViewModel model, Layer layer)
    {
        var delete = new Button { Name = "DeleteLayer", Content = "Excluir camada", Foreground = Brushes.DarkRed };
        delete.Click += async (_, _) =>
        {
            if (await DeleteLayer(window, model, layer)) window.Close();
        };
        panel.Children.Add(delete);
    }
    public static async Task<bool> DeleteLayer(Window owner, EditorViewModel model, Layer layer)
    {
        var count = model.Editor.Document.Objects.Count(shape => shape.LayerId == layer.Id);
        if (count > 0 && await Choose(owner, "Excluir camada",
            $"Excluir a camada \"{layer.Name}\" e seus {count} objeto(s)?\nVocê pode desfazer esta ação.", ["Excluir", "Cancelar"]) != "Excluir") return false;
        model.Editor.DeleteLayer(layer.Id);
        return true;
    }
    public static async Task<bool> EditProvider(Window owner, ProviderSettings settings, TileProvider? provider)
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(20) };
        var name = Field(panel, "Nome", provider?.Name ?? ""); var url = Field(panel, "URL HTTPS XYZ (sem credenciais/query)", provider?.UrlTemplate ?? "https://servidor.exemplo/{z}/{x}/{y}.png");
        var min = Field(panel, "Zoom mínimo", (provider?.MinZoom ?? 0).ToString()); var max = Field(panel, "Zoom máximo", (provider?.MaxZoom ?? 18).ToString());
        var attribution = Field(panel, "Atribuição obrigatória", provider?.Attribution ?? "");
        var permission = new CheckBox { Content = "O provedor autoriza download e uso offline", IsChecked = provider?.AllowsOffline ?? false }; panel.Children.Add(permission);
        var token = Field(panel, "Token Bearer (opcional; vazio mantém o existente)", ""); token.PasswordChar = '●';
        var window = Window("Provedor de mapas", panel, 600);
        AddSubmit(panel, window, async () =>
        {
            var template = url.Text ?? "";
            var value = new TileProvider(ProviderSettings.Identifier(template), name.Text ?? "", template,
                checked((int)Parse(min)), checked((int)Parse(max)), attribution.Text ?? "", permission.IsChecked == true);
            value.Validate();
            if (!string.IsNullOrEmpty(token.Text)) await ProviderSecretStore.Create().SetAsync(value.Id, token.Text);
            await settings.SaveAsync(value);
        }); return await window.ShowDialog<bool>(owner);
    }
    private static Window Window(string title, Control content, double width = 420) => new()
    {
        Title = title,
        Width = width,
        SizeToContent = SizeToContent.Height,
        MaxHeight = 800,
        Content = new ScrollViewer { Content = content },
        WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
    private static void AddSubmit(StackPanel panel, Window window, Func<Task> action)
    {
        var error = new TextBlock { Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap }; panel.Children.Add(error);
        var button = new Button { Content = "Aplicar", HorizontalAlignment = HorizontalAlignment.Right }; panel.Children.Add(button);
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await action(); window.Close(true); } catch (Exception e) { error.Text = e.Message; } finally { button.IsEnabled = true; }
        };
    }
}
