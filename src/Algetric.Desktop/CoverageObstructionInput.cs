using Algetric.Core;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Layout;
using Avalonia.Media;

namespace Algetric.Desktop;

internal sealed class CoverageObstructionInput : StackPanel
{
    private sealed record Choice(GeometryObject Shape, string Label)
    {
        public override string ToString() => Label;
    }
    private readonly ListBox selector = new()
    {
        Name = "CoverageObstructions", SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle,
        MaxHeight = 120, HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly TextBlock empty = new() { Text = "Não há outros polígonos disponíveis.", FontSize = 12 };
    private readonly Choice[] choices;

    public CoverageObstructionInput(ProjectDocument document)
    {
        choices = document.Objects.Where(shape => shape.Kind == ShapeKind.Polygon)
            .Select(shape => new Choice(shape, $"{shape.Name} — {document.Layers.First(layer => layer.Id == shape.LayerId).Name}")).ToArray();
        Spacing = 4;
        Children.Add(new TextBlock { Text = "Obstruções (opcional)", FontWeight = FontWeight.SemiBold });
        Children.Add(selector); Children.Add(empty);
        Children.Add(new TextBlock { Text = "Clique para marcar ou desmarcar vários polígonos. Sem seleção, não há obstruções. Apenas as totalmente internas serão usadas.",
            TextWrapping = TextWrapping.Wrap, FontSize = 12 });
    }

    public GeometryObject[] Selected => selector.SelectedItems?.Cast<Choice>().Select(choice => choice.Shape).ToArray() ?? [];

    public void SetPolygon(Guid polygonId)
    {
        var selectedIds = Selected.Select(shape => shape.Id).ToHashSet();
        var available = choices.Where(choice => choice.Shape.Id != polygonId).ToArray();
        selector.ItemsSource = available;
        selector.SelectedItems = available.Where(choice => selectedIds.Contains(choice.Shape.Id)).ToList();
        selector.IsVisible = available.Length > 0; empty.IsVisible = available.Length == 0;
    }
}
