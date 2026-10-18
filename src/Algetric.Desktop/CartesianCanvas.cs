using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Algetric.Core;
using System.Globalization;

namespace Algetric.Desktop;

public sealed class CartesianCanvas : Control
{
    private readonly EditorViewModel model;
    private readonly List<LocalPoint> draft = [];
    private LocalPoint[]? preview;
    private LocalPoint[]? dragVertices;
    private Point pressed;
    private Point previous;
    private int dragVertex = -1;
    private Guid? dragId;
    private bool panning;
    private EditorTool lastTool;
    public CartesianCanvas(EditorViewModel model)
    {
        this.model = model; Focusable = true; ClipToBounds = true; Cursor = new(StandardCursorType.Cross);
        model.Changed += RefreshTool;
    }
    private void RefreshTool()
    {
        if (lastTool != model.SelectedTool) CancelDraft();
        lastTool = model.SelectedTool;
        if (model.Tool == EditorTool.Move) { preview = null; dragId = null; dragVertices = null; }
        UpdateCursor(); InvalidateVisual();
    }
    private void UpdateCursor() => Cursor = new(model.Tool == EditorTool.Move || panning ? StandardCursorType.Hand : StandardCursorType.Cross);
    private ViewState View => model.Editor.Document.View;
    public Point ToScreen(LocalPoint point) => new((point.X - View.CenterX) * View.PixelsPerMeter + Bounds.Width / 2,
        Bounds.Height / 2 - (point.Y - View.CenterY) * View.PixelsPerMeter);
    private LocalPoint ToLocal(Point point) => new((point.X - Bounds.Width / 2) / View.PixelsPerMeter + View.CenterX,
        (Bounds.Height / 2 - point.Y) / View.PixelsPerMeter + View.CenterY);
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size)); DrawGrid(context);
        foreach (var shape in model.Editor.Document.Objects)
        {
            var layer = model.Editor.Document.Layers.First(l => l.Id == shape.LayerId);
            if (!layer.Visible) continue;
            var vertices = shape.Id == dragId && preview is not null ? preview : shape.Vertices.ToArray();
            DrawShape(context, vertices, shape.Kind, Color.Parse(layer.Color), shape.Id == model.Editor.SelectedId);
        }
        if (draft.Count > 0) DrawShape(context, draft.ToArray(), ShapeKind.Polyline, Color.Parse("#4141FF"), true);
    }
    private void DrawGrid(DrawingContext context)
    {
        var target = 70 / View.PixelsPerMeter; var power = Math.Pow(10, Math.Floor(Math.Log10(target)));
        var step = new[] { 1d, 2d, 5d, 10d }.Select(n => n * power).First(n => n >= target);
        var minimum = ToLocal(new(0, Bounds.Height)); var maximum = ToLocal(new(Bounds.Width, 0));
        var grid = new Pen(Brush.Parse("#E9EEF4"), 1); var axis = new Pen(Brush.Parse("#46536A"), 1.5);
        for (var x = Math.Ceiling(minimum.X / step) * step; x <= maximum.X; x += step)
        {
            var screen = ToScreen(new(x, 0)).X; context.DrawLine(grid, new(screen, 0), new(screen, Bounds.Height));
            Label(context, x.ToString("G5", CultureInfo.InvariantCulture), new(screen + 3, Bounds.Height - 22));
        }
        for (var y = Math.Ceiling(minimum.Y / step) * step; y <= maximum.Y; y += step)
        {
            var screen = ToScreen(new(0, y)).Y; context.DrawLine(grid, new(0, screen), new(Bounds.Width, screen));
            Label(context, y.ToString("G5", CultureInfo.InvariantCulture), new(6, screen + 3));
        }
        var origin = ToScreen(new(0, 0)); context.DrawLine(axis, new(origin.X, 0), new(origin.X, Bounds.Height));
        context.DrawLine(axis, new(0, origin.Y), new(Bounds.Width, origin.Y));
        Label(context, "X (m)", new(Bounds.Width - 55, Bounds.Height - 42)); Label(context, "Y (m)", new(8, 8));
    }
    private static void Label(DrawingContext context, string text, Point position) => context.DrawText(
        new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter"), 12, Brush.Parse("#46536A")), position);
    private void DrawShape(DrawingContext context, LocalPoint[] vertices, ShapeKind kind, Color color, bool selected)
    {
        if (vertices.Length == 0) return;
        var brush = new SolidColorBrush(color); var pen = new Pen(brush, selected ? 3 : 2);
        var points = vertices.Select(ToScreen).ToArray();
        if (kind == ShapeKind.Polygon && points.Length >= 3)
        {
            var geometry = new StreamGeometry();
            using (var path = geometry.Open()) { path.BeginFigure(points[0], true); foreach (var p in points.Skip(1)) path.LineTo(p); path.EndFigure(true); }
            context.DrawGeometry(new SolidColorBrush(color, 0.16), pen, geometry);
        }
        else for (var i = 1; i < points.Length; i++) context.DrawLine(pen, points[i - 1], points[i]);
        for (var i = 0; i < points.Length; i++)
        {
            var active = selected && i == model.VertexIndex;
            context.DrawEllipse(active ? Brushes.RoyalBlue : brush, new Pen(Brushes.White, 1.5), points[i], active ? 6 : 4, active ? 6 : 4);
            if (selected) Label(context, ((char)('A' + i % 26)).ToString(), points[i] + new Vector(7, -18));
        }
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus(); var position = e.GetPosition(this); var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsMiddleButtonPressed || properties.IsRightButtonPressed ||
            (properties.IsLeftButtonPressed && model.Tool == EditorTool.Move))
        { panning = true; previous = position; e.Pointer.Capture(this); UpdateCursor(); e.Handled = true; return; }
        if (!properties.IsLeftButtonPressed) return;
        if (model.Tool != EditorTool.Select) { CreateAt(position, e.ClickCount); return; }
        var hit = Hit(position); model.VertexIndex = Math.Max(0, hit.Vertex); model.Editor.Select(hit.Shape?.Id);
        if (hit.Shape is null) return;
        pressed = position; dragId = hit.Shape.Id; dragVertex = hit.Vertex; dragVertices = hit.Shape.Vertices.ToArray();
        e.Pointer.Capture(this);
    }
    private void CreateAt(Point position, int clickCount)
    {
        var point = ToLocal(position);
        if (model.Tool == EditorTool.Point) { model.Try(() => model.Add(ShapeKind.Point, [point])); return; }
        if (model.Tool is EditorTool.Origin or EditorTool.Move) return;
        if (draft.Count > 0 && Distance(ToScreen(draft[^1]), position) < 2 && clickCount > 1) { FinishDraft(); return; }
        if (model.Tool == EditorTool.Polygon && draft.Count >= 3 && Distance(ToScreen(draft[0]), position) < 10) { FinishDraft(); return; }
        draft.Add(point);
        if (model.Tool == EditorTool.Segment && draft.Count == 2) FinishDraft();
        else if (clickCount > 1) FinishDraft();
        InvalidateVisual();
    }
    public void FinishDraft()
    {
        if (draft.Count == 0 || model.Tool is < EditorTool.Segment or > EditorTool.Polygon) return;
        var points = draft.ToArray();
        model.Try(() => { model.Add((ShapeKind)((int)model.Tool - 1), points); draft.Clear(); }); InvalidateVisual();
    }
    public void CancelDraft() { draft.Clear(); preview = null; dragId = null; dragVertices = null; InvalidateVisual(); }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); var position = e.GetPosition(this);
        if (panning)
        {
            var delta = position - previous; previous = position;
            model.Editor.UpdateView(View with { CenterX = View.CenterX - delta.X / View.PixelsPerMeter, CenterY = View.CenterY + delta.Y / View.PixelsPerMeter });
        }
        else if (dragVertices is not null)
        {
            var delta = ToLocal(position); var start = ToLocal(pressed);
            preview = dragVertices.Select((p, i) => dragVertex < 0 || i == dragVertex ? new LocalPoint(p.X + delta.X - start.X, p.Y + delta.Y - start.Y) : p).ToArray();
        }
        InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (dragId is { } id && preview is not null) model.Try(() => model.Replace(id, preview));
        preview = null; dragVertices = null; dragId = null; panning = false; e.Pointer.Capture(null); UpdateCursor(); InvalidateVisual();
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    { base.OnPointerCaptureLost(e); preview = null; dragVertices = null; dragId = null; panning = false; UpdateCursor(); InvalidateVisual(); }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e); var position = e.GetPosition(this); var before = ToLocal(position);
        var scale = Math.Clamp(View.PixelsPerMeter * Math.Pow(1.2, e.Delta.Y), 0.002, 1000);
        model.Editor.UpdateView(View with
        {
            PixelsPerMeter = scale,
            CenterX = before.X - (position.X - Bounds.Width / 2) / scale,
            CenterY = before.Y + (position.Y - Bounds.Height / 2) / scale
        }); InvalidateVisual(); e.Handled = true;
    }
    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private (GeometryObject? Shape, int Vertex) Hit(Point position)
    {
        foreach (var shape in model.Editor.Document.Objects.Reverse())
        {
            if (!model.Editor.Document.Layers.First(l => l.Id == shape.LayerId).Visible) continue;
            var vertices = shape.Vertices;
            for (var i = 0; i < vertices.Length; i++) if (Distance(ToScreen(vertices[i]), position) <= 10) return (shape, i);
            var p = ToLocal(position); var point = new NetTopologySuite.Geometries.Point(p.X, p.Y);
            if (GeometryRules.ToGeometry(shape).Distance(point) * View.PixelsPerMeter <= 7) return (shape, -1);
        }
        return (null, -1);
    }
}
