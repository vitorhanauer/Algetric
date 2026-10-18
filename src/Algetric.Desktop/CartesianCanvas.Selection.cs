using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Algetric.Desktop;

public sealed partial class CartesianCanvas
{
    private Point? selectionStart;
    private Point selectionEnd;
    private IPointer? selectionPointer;
    private void BeginAreaSelection(Point position, IPointer pointer)
    {
        selectionStart = selectionEnd = position;
        selectionPointer = pointer; pointer.Capture(this);
    }
    private void UpdateAreaSelection(Point position)
    {
        selectionEnd = position;
        InvalidateVisual();
    }
    private void DrawAreaSelection(DrawingContext context)
    {
        if (selectionStart is not { } start) return;
        var bounds = new Rect(start, selectionEnd).Normalize();
        context.DrawRectangle(new SolidColorBrush(Colors.DodgerBlue, 0.15), new Pen(Brushes.DodgerBlue, 1.5), bounds);
    }
    private void FinishAreaSelection(Point position)
    {
        if (selectionStart is not { } start) return;
        CancelAreaSelection();
        if (Distance(start, position) < 4) return;
        model.Editor.SelectArea(ToLocal(start), ToLocal(position));
    }
    private void CancelAreaSelection()
    {
        selectionStart = null;
        var pointer = selectionPointer; selectionPointer = null;
        pointer?.Capture(null);
        InvalidateVisual();
    }
}
