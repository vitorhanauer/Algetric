using Algetric.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Algetric.Desktop;

internal sealed partial class LayerPanel : StackPanel
{
    private sealed record LayerRow(Border Card, Button Select, TextBlock Name, CheckBox Visible, StackPanel Objects, TextBlock Empty);
    private sealed record ObjectRow(Grid Row, Button Select, TextBlock Name, Button Delete, CheckBox? Visible);
    private readonly EditorViewModel model;
    private readonly Action<Layer> editLayer;
    private readonly Action<Layer> deleteLayer;
    private readonly Canvas dragOverlay;
    private readonly Dictionary<Guid, LayerRow> layerRows = [];
    private readonly Dictionary<Guid, ObjectRow> objectRows = [];
    private bool refreshing;

    public LayerPanel(EditorViewModel model, Canvas dragOverlay, Action<Layer> editLayer, Action<Layer> deleteLayer)
    {
        this.model = model; this.dragOverlay = dragOverlay; this.editLayer = editLayer; this.deleteLayer = deleteLayer;
        Name = "LayerTree"; Spacing = 6; Focusable = true;
        dragOverlay.Children.Add(dragPreview);
        AddHandler(PointerMovedEvent, OnDragMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnDragReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        PointerCaptureLost += (_, e) => { if (e.Source == this) CancelDrag(); };
        DetachedFromVisualTree += (_, _) => CancelDrag();
    }
    public void Refresh()
    {
        if (dragDocument is not null && dragDocument != model.Editor.Document) CancelDrag();
        refreshing = true;
        try
        {
            RefreshLayerRows(); RemoveDeletedObjects();
            foreach (var layer in model.Editor.Document.Layers)
            {
                var row = layerRows[layer.Id]; row.Name.Text = layer.Name; row.Name.Foreground = Brush.Parse(layer.Color);
                row.Visible.IsChecked = layer.Visible;
                UiTheme.SetHighlight(row.Select, Button.BackgroundProperty, layer.Id == model.Editor.ActiveLayerId, "ActiveLayer");
                RefreshObjectRows(layer, row.Objects);
            }
            UpdateEmptyLayers(); UpdateDropHighlight();
        }
        finally { refreshing = false; }
    }
    private void RefreshLayerRows()
    {
        if (layerRows.Keys.SequenceEqual(model.Editor.Document.Layers.Select(layer => layer.Id))) return;
        CancelDrag(); Children.Clear(); layerRows.Clear();
        foreach (var layer in model.Editor.Document.Layers)
        {
            var row = CreateLayerRow(layer.Id); layerRows.Add(layer.Id, row); Children.Add(row.Card);
        }
    }
    private void RemoveDeletedObjects()
    {
        foreach (var id in objectRows.Keys.Where(id => !model.Editor.Document.Objects.Any(shape => shape.Id == id)).ToArray())
        {
            var row = objectRows[id].Row; (row.Parent as Panel)?.Children.Remove(row); objectRows.Remove(id);
        }
    }
    private void RefreshObjectRows(Layer layer, StackPanel container)
    {
        var index = 0;
        foreach (var shape in model.Editor.Document.Objects.Where(shape => shape.LayerId == layer.Id))
        {
            if (!objectRows.TryGetValue(shape.Id, out var row)) { row = CreateObjectRow(shape.Id); objectRows.Add(shape.Id, row); }
            if (row.Row.Parent != container || container.Children.IndexOf(row.Row) != index)
            {
                (row.Row.Parent as Panel)?.Children.Remove(row.Row); container.Children.Insert(index, row.Row);
            }
            row.Name.Text = shape.Name;
            if (row.Visible is { } visible) visible.IsChecked = shape.Visible;
            UiTheme.SetHighlight(row.Select, Button.BackgroundProperty, model.Editor.SelectedIds.Contains(shape.Id), "SelectedObject");
            Avalonia.Automation.AutomationProperties.SetName(row.Delete, UiText.F("Excluir {0}", shape.Name));
            ToolTip.SetTip(row.Delete, UiText.F("Excluir {0}", shape.Name)); index++;
        }
    }
    private LayerRow CreateLayerRow(Guid id)
    {
        var name = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = FontWeight.SemiBold };
        var select = ActionButton(name, () => model.Editor.SetActiveLayer(id)); select.Name = $"SelectLayer_{id:N}";
        select.Bind(ToolTip.TipProperty, UiText.Binding("Clique para ativar; duplo clique para editar; botão direito para opções"));
        ConfigureLayerActions(select, id);
        var visible = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
        visible.Bind(ToolTip.TipProperty, UiText.Binding("Mostrar ou ocultar camada"));
        visible.IsCheckedChanged += (_, _) => { if (!refreshing) model.Try(() => model.Editor.UpdateLayer(CurrentLayer(id) with { Visible = visible.IsChecked == true })); };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(select, 1);
        header.Children.Add(visible); header.Children.Add(select);
        var objects = new StackPanel { Name = $"LayerObjects_{id:N}", Margin = new Thickness(18, 0, 0, 0), Spacing = 2 };
        var empty = new TextBlock { [!TextBlock.TextProperty] = UiText.Binding("Sem objetos"), Foreground = Brushes.Gray, FontSize = 11, Margin = new Thickness(24, 4, 0, 6) };
        var content = new StackPanel { Spacing = 2 }; content.Children.Add(header); content.Children.Add(objects); content.Children.Add(empty);
        var card = new Border { Name = $"Layer_{id:N}", Child = content, Padding = new Thickness(4), CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), Background = Brushes.Transparent };
        return new(card, select, name, visible, objects, empty);
    }
    private void ConfigureLayerActions(Button select, Guid id)
    {
        select.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.ClickCount != 2 || !e.GetCurrentPoint(select).Properties.IsLeftButtonPressed) return;
            e.Handled = true;
            editLayer(CurrentLayer(id));
        }, RoutingStrategies.Tunnel);
        var edit = new MenuItem { [!MenuItem.HeaderProperty] = UiText.Binding("Editar") };
        edit.Click += (_, _) => editLayer(CurrentLayer(id));
        var delete = new MenuItem { [!MenuItem.HeaderProperty] = UiText.Binding("Excluir") };
        delete.Click += (_, _) => deleteLayer(CurrentLayer(id));
        select.ContextMenu = new ContextMenu { Items = { edit, delete } };
    }
    private ObjectRow CreateObjectRow(Guid id)
    {
        var name = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        var select = ActionButton(name, () => { model.VertexIndex = 0; model.Editor.Select(id); });
        select.Name = $"ObjectSelect_{id:N}";
        if (model.Editor.Document.Objects.First(shape => shape.Id == id).Kind == ShapeKind.Polygon)
            select.ContextMenu = PolygonContextMenu.Create(model, id);
        select.AddHandler(PointerPressedEvent, (_, e) => PrepareDrag(id, select, e), RoutingStrategies.Tunnel);
        var delete = ActionButton(new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M3,5 L13,5 M6,5 L6,3 L10,3 L10,5 M4,5 L5,15 L11,15 L12,5 M7,7 L7,12 M9,7 L9,12"),
            Stroke = Brush.Parse("#8A3944"),
            StrokeThickness = 1.5,
            Width = 16,
            Height = 18
        }, () => model.Editor.Delete(id));
        UiTheme.Bind((Avalonia.Controls.Shapes.Path)delete.Content!, Avalonia.Controls.Shapes.Shape.StrokeProperty, "DangerText");
        delete.Name = $"DeleteObject_{id:N}";
        var row = new Grid { Name = $"Object_{id:N}", ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(select, 1); Grid.SetColumn(delete, 2); row.Children.Add(select); row.Children.Add(delete);
        var visible = AddPolygonVisibility(row, id);
        return new(row, select, name, delete, visible);
    }
    private CheckBox? AddPolygonVisibility(Grid row, Guid id)
    {
        if (model.Editor.Document.Objects.First(shape => shape.Id == id).Kind != ShapeKind.Polygon) return null;
        var visible = new CheckBox { Name = $"ObjectVisible_{id:N}", VerticalAlignment = VerticalAlignment.Center };
        visible.Bind(ToolTip.TipProperty, UiText.Binding("Mostrar ou ocultar polígono"));
        visible.Bind(Avalonia.Automation.AutomationProperties.NameProperty, UiText.Binding("Mostrar ou ocultar polígono"));
        visible.IsCheckedChanged += (_, _) =>
        {
            if (!refreshing) model.Try(() => model.Editor.SetObjectVisibility(id, visible.IsChecked == true));
        };
        row.Children.Add(visible);
        return visible;
    }
    private Layer CurrentLayer(Guid id) => model.Editor.Document.Layers.First(layer => layer.Id == id);
    private void UpdateEmptyLayers()
    {
        foreach (var row in layerRows.Values) row.Empty.IsVisible = !row.Objects.Children.Any(child => child.IsVisible);
    }
    private Button ActionButton(object content, Action action)
    {
        var button = new Button { Content = content, Margin = new Thickness(1), Padding = new Thickness(6, 5), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => model.Try(action); return button;
    }
}
