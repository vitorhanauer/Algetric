using Algetric.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Algetric.Desktop;

internal static partial class Dialogs
{
    public static async Task<GeometryObject?> EditFunction(Window owner, EditorViewModel model, Guid? id)
    {
        var existing = model.Editor.Document.Objects.FirstOrDefault(shape => shape.Id == id && shape.Kind == ShapeKind.FunctionGraph);
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(20) };
        var name = Field(panel, "Nome", existing?.Name ?? ""); name.Name = "FunctionName";
        name.PlaceholderText = "f(x) = …";
        var expression = Field(panel, "Função f(x)", existing?.Function?.Expression ?? "x^2"); expression.Name = "FunctionExpression";
        var range = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 8 };
        var left = new StackPanel { Spacing = 4 }; var right = new StackPanel { Spacing = 4 };
        var minimum = Field(left, "X mínimo", Number(existing?.Function?.MinimumX ?? -10)); minimum.Name = "FunctionMinimumX";
        var maximum = Field(right, "X máximo", Number(existing?.Function?.MaximumX ?? 10)); maximum.Name = "FunctionMaximumX";
        Grid.SetColumn(right, 1); range.Children.Add(left); range.Children.Add(right); panel.Children.Add(range);
        panel.Children.Add(new TextBlock { [!TextBlock.TextProperty] = UiText.Binding("Exemplos: 5; 2*x^2 - 3*x + 1; 2^x; exp(x); ln(x); log(x); sin(x) + cos(2*x)."), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { [!TextBlock.TextProperty] = UiText.Binding("Combine funções com +, -, *, /, ^ e parênteses. Trigonometria em radianos. log(x) usa base 10; ln(x) é natural; log(x, b) usa base b. Use ponto decimal. Constantes: pi, π e e."), TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var window = Window(id is null ? "Desenhar função" : "Editar função", panel, 560);
        UiTheme.Bind(window, Avalonia.Controls.Window.BackgroundProperty, "PanelSurface");
        AddFunctionActions(panel, window, model, id, name, expression, minimum, maximum);
        window.Opened += (_, _) => { expression.Focus(); expression.SelectAll(); };
        return await window.ShowDialog<GeometryObject?>(owner);
    }
    private static void AddFunctionActions(StackPanel panel, Window window, EditorViewModel model, Guid? id,
        TextBox name, TextBox expression, TextBox minimum, TextBox maximum)
    {
        var error = new TextBlock { Name = "FunctionError", TextWrapping = TextWrapping.Wrap };
        UiTheme.Bind(error, TextBlock.ForegroundProperty, "DangerText"); panel.Children.Add(error);
        foreach (var input in new[] { name, expression, minimum, maximum }) input.TextChanged += (_, _) => error.Text = "";
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var cancel = new Button { Name = "CancelFunctionGraph", [!Button.ContentProperty] = UiText.Binding("Cancelar") };
        cancel.Click += (_, _) => window.Close();
        var apply = new Button { Name = "SaveFunctionGraph", [!Button.ContentProperty] = UiText.Binding(id is null ? "Desenhar gráfico" : "Aplicar") };
        void Save()
        {
            try { window.Close(model.SaveFunctionGraph(id, name.Text ?? "", expression.Text ?? "", Parse(minimum), Parse(maximum))); }
            catch (ArgumentException exception) { error.Text = UiText.T(exception.Message); }
        }
        apply.Click += (_, _) => Save(); actions.Children.Add(cancel); actions.Children.Add(apply); panel.Children.Add(actions);
        window.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape) { window.Close(); args.Handled = true; }
            if (args.Key == Key.Enter) { Save(); args.Handled = true; }
        };
    }
}
