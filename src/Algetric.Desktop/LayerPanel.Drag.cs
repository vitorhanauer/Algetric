using Algetric.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Algetric.Desktop;

internal sealed partial class LayerPanel
{
    private readonly Border dragPreview = new()
    {
        Name = "LayerDragPreview",
        IsVisible = false,
        IsHitTestVisible = false,
        Background = Brush.Parse("#E5F1FF"),
        BorderBrush = Brush.Parse("#5497DC"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(10, 7),
        Opacity = 0.9
    };
    private Guid? pressedObject;
    private Guid? draggedObject;
    private Guid? dropLayer;
    private Point pressPosition;
    private IPointer? dragPointer;
    private ProjectDocument? dragDocument;

    private void PrepareDrag(Guid id, Button source, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(source).Properties.IsLeftButtonPressed) return;
        pressedObject = id; pressPosition = e.GetPosition(dragOverlay);
    }
    private void OnDragMoved(object? sender, PointerEventArgs e)
    {
        if (pressedObject is not { } id) return;
        var position = e.GetPosition(dragOverlay);
        if (draggedObject is null)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { pressedObject = null; return; }
            var delta = position - pressPosition;
            if (delta.X * delta.X + delta.Y * delta.Y < 25) return;
            BeginDrag(id, e.Pointer);
        }
        Canvas.SetLeft(dragPreview, position.X + 12); Canvas.SetTop(dragPreview, position.Y + 12);
        dropLayer = FindDropLayer(position); UpdateDropHighlight(); e.Handled = true;
    }
    private void BeginDrag(Guid id, IPointer pointer)
    {
        var shape = model.Editor.Document.Objects.First(shape => shape.Id == id);
        draggedObject = id; dragDocument = model.Editor.Document;
        model.VertexIndex = 0; model.Editor.Select(id);
        Focus(); dragPointer = pointer; pointer.Capture(this);
        // Hide only the list row; the project changes once, after a valid drop.
        objectRows[id].Row.IsVisible = false;
        var label = new TextBlock { Text = shape.Name, MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis };
        UiTheme.Bind(label, TextBlock.ForegroundProperty, "DragText");
        UiTheme.Bind(dragPreview, Border.BackgroundProperty, "DragSurface");
        dragPreview.Child = label;
        dragPreview.IsVisible = true; UpdateEmptyLayers();
    }
    private Guid? FindDropLayer(Point position)
    {
        var root = TopLevel.GetTopLevel(this);
        if (root is null || dragOverlay.TranslatePoint(position, root) is not { } rootPosition) return null;
        // Hit testing respects scrolling and clipping, so an offscreen layer cannot receive a drop.
        if (root.InputHitTest(rootPosition) is not Visual hit) return null;
        foreach (var (id, row) in layerRows)
        {
            if (hit == row.Card || hit.GetVisualAncestors().Contains(row.Card)) return id;
        }
        return null;
    }
    private void OnDragReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (dragPointer is not null && e.Pointer != dragPointer) return;
        var id = draggedObject;
        var target = id is not null ? FindDropLayer(e.GetPosition(dragOverlay)) : null;
        CancelDrag();
        if (id is null) return;
        e.Handled = true;
        if (target is { } layerId) model.Try(() => model.Editor.MoveObjectToLayer(id.Value, layerId));
    }
    public bool CancelDrag()
    {
        var wasDragging = draggedObject is not null;
        if (draggedObject is { } id && objectRows.TryGetValue(id, out var row)) row.Row.IsVisible = true;
        pressedObject = null; draggedObject = null; dropLayer = null; dragDocument = null; dragPreview.IsVisible = false;
        var pointer = dragPointer; dragPointer = null; pointer?.Capture(null);
        UpdateEmptyLayers(); UpdateDropHighlight(); return wasDragging;
    }
    private void UpdateDropHighlight()
    {
        var sourceLayer = model.Editor.Document.Objects.FirstOrDefault(shape => shape.Id == draggedObject)?.LayerId;
        foreach (var (id, row) in layerRows)
        {
            var validTarget = draggedObject is not null && dropLayer == id && sourceLayer != id;
            row.Card.BorderBrush = validTarget ? Brush.Parse("#5497DC") : Brushes.Transparent;
            UiTheme.SetHighlight(row.Card, Border.BackgroundProperty, validTarget, "DropSurface");
        }
    }
}
