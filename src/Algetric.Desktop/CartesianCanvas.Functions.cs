using Algetric.Core;
using Avalonia.Media;

namespace Algetric.Desktop;

public sealed partial class CartesianCanvas
{
    private void DrawFunction(DrawingContext context, GeometryObject shape, Color color)
    {
        var pen = new Pen(new SolidColorBrush(color), model.Editor.SelectedIds.Contains(shape.Id) ? 3 : 2);
        foreach (var segment in shape.Function!.GetSegments())
            for (var index = 1; index < segment.Length; index++) context.DrawLine(pen, ToScreen(segment[index - 1]), ToScreen(segment[index]));
    }
    public void FocusFunction(FunctionDefinition function)
    {
        var yValues = FocusValues(function);
        var minimumY = yValues[(int)((yValues.Length - 1) * 0.05)];
        var maximumY = yValues[(int)((yValues.Length - 1) * 0.95)];
        if (yValues[^1] - yValues[0] <= 4 * Math.Max(2, maximumY - minimumY))
        { minimumY = yValues[0]; maximumY = yValues[^1]; }
        var domainWidth = function.MaximumX - function.MinimumX;
        if (maximumY - minimumY > 10 * domainWidth)
        { minimumY = yValues[yValues.Length / 2] - domainWidth / 2; maximumY = minimumY + domainWidth; }
        var scale = Math.Min(Bounds.Width * 0.85 / (function.MaximumX - function.MinimumX),
            Bounds.Height * 0.85 / Math.Max(2, maximumY - minimumY));
        model.Editor.UpdateView(View with
        {
            CenterX = (function.MinimumX + function.MaximumX) / 2, CenterY = (minimumY + maximumY) / 2,
            PixelsPerMeter = Math.Clamp(scale, 0.002, 1000)
        });
        InvalidateVisual();
    }
    private static double[] FocusValues(FunctionDefinition function)
    {
        var expression = FunctionExpression.Parse(function.Expression);
        // Uniform X samples keep adaptive refinement near a pole from dominating the zoom bounds.
        var values = Enumerable.Range(0, 257).Select(index => expression.Evaluate(function.MinimumX +
            (function.MaximumX - function.MinimumX) * index / 256)).Where(y => double.IsFinite(y) && Math.Abs(y) <= 50_000).ToArray();
        return (values.Length > 0 ? values : function.GetSegments().SelectMany(segment => segment).Select(point => point.Y)).Order().ToArray();
    }
}
