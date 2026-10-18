using System.Collections.Immutable;

namespace Algetric.Core;

internal sealed class FunctionSampler(FunctionExpression function, double minimum, double maximum)
{
    private readonly List<List<LocalPoint>> segments = [];
    private List<LocalPoint>? current;
    private int evaluations;
    private double Tolerance => Math.Max(1e-7, (maximum - minimum) / 65536);
    public ImmutableArray<ImmutableArray<LocalPoint>> Sample()
    {
        const int intervals = 256;
        var left = Evaluate(minimum);
        for (var index = 1; index <= intervals; index++)
        {
            var x = minimum + (maximum - minimum) * index / intervals;
            var right = Evaluate(x); Subdivide(left, right, 0); left = right;
        }
        return segments.Where(segment => segment.Count >= 2).Select(segment => segment.ToImmutableArray()).ToImmutableArray();
    }
    private void Subdivide(LocalPoint left, LocalPoint right, int depth)
    {
        var middle = Evaluate(left.X + (right.X - left.X) / 2);
        if (Valid(left) && Valid(middle) && Valid(right) && Math.Abs(middle.Y - (left.Y + right.Y) / 2) <= Tolerance)
        { Append(left, right); return; }
        // Unresolved curvature or undefined values must leave a gap, never bridge a singularity.
        if (depth >= 10 || evaluations >= 32_768 || middle.X == left.X || middle.X == right.X)
        { current = null; return; }
        if (!Valid(left) && !Valid(middle) && !Valid(right)) { current = null; return; }
        Subdivide(left, middle, depth + 1); Subdivide(middle, right, depth + 1);
    }
    private LocalPoint Evaluate(double x)
    { evaluations++; return new(x, function.Evaluate(x)); }
    private static bool Valid(LocalPoint point) => double.IsFinite(point.Y) && point.X * point.X + point.Y * point.Y <= 2_500_000_000;
    private void Append(LocalPoint left, LocalPoint right)
    {
        if (current is null || current[^1] != left) { current = [left]; segments.Add(current); }
        current.Add(right);
    }
}
