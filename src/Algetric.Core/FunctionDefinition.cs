using System.Collections.Immutable;

namespace Algetric.Core;

public sealed class FunctionDefinition
{
    public string Expression { get; }
    public double MinimumX { get; }
    public double MaximumX { get; }
    private readonly ImmutableArray<ImmutableArray<LocalPoint>> segments;
    public FunctionDefinition(string expression, double minimumX, double maximumX)
    {
        if (!double.IsFinite(minimumX) || !double.IsFinite(maximumX) || minimumX >= maximumX || minimumX < -50_000 || maximumX > 50_000)
            throw new ArgumentException("O intervalo de X deve ser crescente e estar entre -50000 e 50000.");
        var function = FunctionExpression.Parse(expression);
        Expression = function.Expression; MinimumX = minimumX; MaximumX = maximumX;
        segments = new FunctionSampler(function, minimumX, maximumX).Sample();
        if (segments.IsEmpty) throw new ArgumentException("A função não possui trechos válidos nesse intervalo.");
    }
    public ImmutableArray<ImmutableArray<LocalPoint>> GetSegments() => segments;
}
