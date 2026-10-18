namespace Algetric.Core;

public sealed record CoveragePathOptions(LocalPoint Start, LocalPoint End, double Width, double OverlapPercent,
    IReadOnlyList<GeometryObject>? Obstructions = null);

public interface ICoveragePathGenerator
{
    LocalPoint[] Generate(GeometryObject polygon, CoveragePathOptions options);
}
