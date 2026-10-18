using Algetric.Core;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Algetric.Desktop;

internal sealed class ObjectPropertiesPanel : StackPanel
{
    private readonly EditorViewModel model;
    private readonly Action vertexChanged;
    private readonly TextBox name;
    private readonly TextBox x;
    private readonly TextBox y;
    private readonly ComboBox vertex = new() { Name = "PropertyVertex", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock measurements = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private Guid? objectId;
    private int vertexIndex;
    private bool refreshing;
    private bool committing;

    public ObjectPropertiesPanel(EditorViewModel model, Action vertexChanged)
    {
        this.model = model; this.vertexChanged = vertexChanged; Spacing = 8;
        var identity = Row("*,110"); var coordinates = Row("*,*");
        name = Field(identity, 0, "Nome", "PropertyName");
        var vertexField = new StackPanel { Spacing = 4 };
        vertexField.Children.Add(new TextBlock { Text = "Vértice", FontSize = 12 });
        vertexField.Children.Add(vertex); Grid.SetColumn(vertexField, 1); identity.Children.Add(vertexField);
        x = Field(coordinates, 0, "X (m)", "PropertyX"); y = Field(coordinates, 1, "Y (m)", "PropertyY");
        Children.Add(identity); Children.Add(coordinates); Children.Add(measurements);
        vertex.SelectionChanged += (_, _) => SelectVertex();
    }
    public void Refresh()
    {
        // A confirmation must leave the same controls alive so Tab and pointer focus can finish moving.
        if (committing) return;
        refreshing = true;
        try
        {
            var shape = model.Selected; objectId = shape?.Id; IsVisible = shape is not null;
            if (shape is null) return;
            name.Text = shape.Name;
            vertex.ItemsSource = Enumerable.Range(1, shape.Vertices.Length).Select(i => $"Vértice {i}").ToArray();
            vertexIndex = Math.Clamp(model.VertexIndex, 0, shape.Vertices.Length - 1);
            vertex.SelectedIndex = vertexIndex; RefreshCoordinates(shape); RefreshMeasurements(shape);
        }
        finally { refreshing = false; }
    }
    private void SelectVertex()
    {
        if (refreshing || vertex.SelectedIndex < 0) return;
        Commit();
        vertexIndex = vertex.SelectedIndex; model.VertexIndex = vertexIndex;
        if (model.Selected is { } shape) RefreshCoordinates(shape);
        vertexChanged();
    }
    private void Commit()
    {
        if (refreshing || committing || model.Selected is not { } shape || shape.Id != objectId) return;
        committing = true;
        try
        {
            model.Try(() =>
            {
                var points = shape.Vertices.ToArray(); var current = points[vertexIndex];
                // Keep full precision when the user has not changed the formatted coordinate.
                points[vertexIndex] = new(ParseChanged(x, current.X), ParseChanged(y, current.Y));
                model.UpdateObject(shape.Id, name.Text ?? "", points);
            });
            if (model.Selected is { } updated) RefreshMeasurements(updated);
        }
        finally { committing = false; }
    }
    private static double ParseChanged(TextBox input, double current)
        => input.Text == Dialogs.Number(current) ? current : Dialogs.Parse(input);
    private void RefreshCoordinates(GeometryObject shape)
    {
        var point = shape.Vertices[vertexIndex]; x.Text = Dialogs.Number(point.X); y.Text = Dialogs.Number(point.Y);
    }
    private void RefreshMeasurements(GeometryObject shape)
    {
        var measure = GeometryRules.Measure(shape);
        measurements.Text = $"Comprimento/perímetro: {measure.Length:N2} m\nÁrea: {measure.Area:N2} m²\nMedidas planimétricas";
    }
    private TextBox Field(Grid row, int column, string label, string controlName)
    {
        var panel = new StackPanel { Spacing = 4 }; Grid.SetColumn(panel, column); row.Children.Add(panel);
        var field = Dialogs.Field(panel, label, ""); field.Name = controlName;
        field.LostFocus += (_, _) => Commit();
        field.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Commit(); } };
        return field;
    }
    private static Grid Row(string columns) => new() { ColumnDefinitions = new ColumnDefinitions(columns), ColumnSpacing = 8 };
}
