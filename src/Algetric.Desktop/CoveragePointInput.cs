using Algetric.Core;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Algetric.Desktop;

internal sealed class CoveragePointInput : StackPanel
{
    private sealed record PointChoice(string Label, LocalPoint? Point)
    { public override string ToString() => Label; }
    private readonly PointChoice[] coordinateAndObjectChoices;
    private readonly ComboBox selector;
    private readonly TextBox x;
    private readonly TextBox y;
    public CoveragePointInput(string title, string prefix, IEnumerable<GeometryObject> points)
    {
        Spacing = 6;
        Children.Add(new TextBlock { Text = UiText.T(title), FontWeight = FontWeight.SemiBold });
        var choices = new List<PointChoice> { new(UiText.T("Informar coordenadas"), null) };
        choices.AddRange(points.Select(point => new PointChoice($"{point.Name} ({Dialogs.Number(point.Vertices[0].X)}; {Dialogs.Number(point.Vertices[0].Y)})", point.Vertices[0])));
        coordinateAndObjectChoices = choices.ToArray();
        selector = new ComboBox { Name = $"Coverage{prefix}Point", ItemsSource = coordinateAndObjectChoices, HorizontalAlignment = HorizontalAlignment.Stretch };
        Children.Add(selector);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 8 };
        var first = new StackPanel { Spacing = 4 }; var second = new StackPanel { Spacing = 4 };
        x = Dialogs.Field(first, "X (m)", "0"); x.Name = $"Coverage{prefix}X";
        y = Dialogs.Field(second, "Y (m)", "0"); y.Name = $"Coverage{prefix}Y";
        Grid.SetColumn(second, 1); row.Children.Add(first); row.Children.Add(second); Children.Add(row);
        selector.SelectionChanged += (_, _) => UpdateSelection(); selector.SelectedIndex = 0;
    }
    public LocalPoint Value => new(Dialogs.Parse(x), Dialogs.Parse(y));
    public void SetPolygon(GeometryObject polygon, int defaultVertex)
    {
        var vertices = polygon.Vertices.Select((point, index) => new PointChoice(
            UiText.F("Vértice {0} — {1} ({2}; {3})", index + 1, polygon.Name, Dialogs.Number(point.X), Dialogs.Number(point.Y)), point));
        selector.ItemsSource = coordinateAndObjectChoices.Concat(vertices).ToArray();
        selector.SelectedIndex = coordinateAndObjectChoices.Length + defaultVertex;
    }
    private void UpdateSelection()
    {
        var point = (selector.SelectedItem as PointChoice)?.Point;
        x.IsEnabled = y.IsEnabled = point is null;
        if (point is not { } selected) return;
        x.Text = Dialogs.Number(selected.X); y.Text = Dialogs.Number(selected.Y);
    }
}
