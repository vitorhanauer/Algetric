using Algetric.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Algetric.Desktop;

internal static partial class Dialogs
{
    private sealed record PolygonChoice(GeometryObject Shape, string Label)
    {
        public Guid Id => Shape.Id;
        public override string ToString() => Label;
    }
    public static async Task GenerateCoveragePath(Window owner, EditorViewModel model, Guid? polygonId)
    {
        var document = model.Editor.Document;
        var choices = document.Objects.Where(shape => shape.Kind == ShapeKind.Polygon)
            .Select(shape => new PolygonChoice(shape, $"{shape.Name} — {document.Layers.First(layer => layer.Id == shape.LayerId).Name}")).ToArray();
        if (choices.Length == 0) { model.Try(() => throw new InvalidOperationException("Crie um polígono antes de gerar um caminho.")); return; }
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(20) };
        var inputs = new StackPanel { Spacing = 12 };
        inputs.Children.Add(new TextBlock { Text = "Polígono", FontWeight = FontWeight.SemiBold });
        var selector = new ComboBox { Name = "CoveragePolygon", ItemsSource = choices, SelectedValueBinding = new Binding(nameof(PolygonChoice.Id)), HorizontalAlignment = HorizontalAlignment.Stretch };
        inputs.Children.Add(selector);
        var points = document.Objects.Where(shape => shape.Kind == ShapeKind.Point).ToArray();
        var start = new CoveragePointInput("Ponto inicial", "Start", points); var end = new CoveragePointInput("Ponto final", "End", points);
        inputs.Children.Add(start); inputs.Children.Add(end);
        var (width, overlap) = CoverageParameters(inputs);
        selector.SelectionChanged += (_, _) => { if (selector.SelectedItem is PolygonChoice choice) { start.SetPolygon(choice.Shape, 0); end.SetPolygon(choice.Shape, choice.Shape.Vertices.Length - 1); } };
        selector.SelectedItem = choices.FirstOrDefault(choice => choice.Id == polygonId) ?? choices[0];
        panel.Children.Add(inputs);
        var window = Window("Gerar caminho de pontos", panel, 500);
        UiTheme.Bind(window, Avalonia.Controls.Window.BackgroundProperty, "PanelSurface");
        using var cancellation = new CancellationTokenSource(); window.Closed += (_, _) => cancellation.Cancel();
        AddCoverageActions(panel, inputs, window, model, selector, start, end, width, overlap, cancellation.Token);
        await window.ShowDialog(owner);
    }
    private static (TextBox Width, TextBox Overlap) CoverageParameters(StackPanel inputs)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 8 };
        var first = new StackPanel { Spacing = 4 }; var second = new StackPanel { Spacing = 4 };
        var width = Field(first, "Largura de cobertura (m)", "10"); width.Name = "CoverageWidth";
        var overlap = Field(second, "Sobreposição (%)", "0"); overlap.Name = "CoverageOverlap";
        Grid.SetColumn(second, 1); row.Children.Add(first); row.Children.Add(second); inputs.Children.Add(row);
        inputs.Children.Add(new TextBlock { Text = "Os pontos podem estar dentro ou fora do polígono. Sobreposição de 0% a menos de 100%.", TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        return (width, overlap);
    }
    private static void AddCoverageActions(StackPanel panel, StackPanel inputs, Window window, EditorViewModel model,
        ComboBox polygon, CoveragePointInput start, CoveragePointInput end, TextBox width, TextBox overlap, CancellationToken token)
    {
        var error = new TextBlock { Name = "CoverageError", TextWrapping = TextWrapping.Wrap };
        UiTheme.Bind(error, TextBlock.ForegroundProperty, "DangerText"); panel.Children.Add(error);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var cancel = new Button { Content = "Cancelar" }; cancel.Click += (_, _) => window.Close();
        var generate = new Button { Name = "GenerateCoveragePath", Content = "Gerar caminho" };
        generate.Click += async (_, _) =>
        {
            inputs.IsEnabled = generate.IsEnabled = false; error.Text = ""; generate.Content = "Gerando…";
            try
            {
                var selected = polygon.SelectedItem as PolygonChoice ?? throw new ArgumentException("Selecione um polígono.");
                await model.GenerateCoveragePathAsync(selected.Id, new(start.Value, end.Value, Parse(width), Parse(overlap)), token);
                if (!token.IsCancellationRequested) window.Close();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { error.Text = exception.Message; }
            finally { inputs.IsEnabled = generate.IsEnabled = true; generate.Content = "Gerar caminho"; }
        };
        footer.Children.Add(cancel); footer.Children.Add(generate); panel.Children.Add(footer);
        window.KeyDown += (_, args) => { if (args.Key == Key.Escape) { window.Close(); args.Handled = true; } };
    }
}
