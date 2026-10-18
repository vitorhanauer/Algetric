using System.Collections.Immutable;
using Algetric.Core;
using Algetric.Infrastructure;
using NetTopologySuite.Geometries;
using Xunit;

namespace Algetric.Tests;

public class CoverageObstructionTests
{
    private static GeometryObject Rectangle(double x, double y, double width, double height) =>
        new(Guid.NewGuid(), "Obstrução", Guid.NewGuid(), ShapeKind.Polygon,
            new LocalPoint[] { new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height) }.ToImmutableArray());

    [Fact]
    public void RouteAvoidsMultipleInternalObstructions()
    {
        var area = Rectangle(0, 0, 100, 100);
        var obstacles = new[] { Rectangle(20, 20, 20, 20), Rectangle(60, 60, 20, 20) };
        var generator = new BoustrophedonPathGenerator();
        var options = new CoveragePathOptions(new(-10, 50), new(110, 50), 10, 0, obstacles);
        var route = generator.Generate(area, options);
        var line = new LineString(route.Select(point => new Coordinate(point.X, point.Y)).ToArray());
        Assert.All(obstacles, obstacle => Assert.True(GeometryRules.ToGeometry(obstacle).Buffer(-1e-7).Disjoint(line)));
        Assert.Equal(options.Start, route[0]); Assert.Equal(options.End, route[^1]);
    }

    [Fact]
    public void EmptyOutsidePartialAndBoundaryTouchingObstructionsAreIgnored()
    {
        var area = Rectangle(0, 0, 100, 100);
        var generator = new BoustrophedonPathGenerator();
        var options = new CoveragePathOptions(new(0, 0), new(100, 100), 10, 0);
        var expected = generator.Generate(area, options);
        Assert.Equal(expected, generator.Generate(area, options with { Obstructions = [] }));
        GeometryObject[] ignored = [Rectangle(200, 200, 20, 20), Rectangle(90, 40, 20, 20), Rectangle(0, 40, 10, 20)];
        Assert.Equal(expected, generator.Generate(area, options with { Obstructions = ignored }));
    }

    [Fact]
    public void EndpointInsideAnObstructionIsRejectedClearly()
    {
        var options = new CoveragePathOptions(new(30, 30), new(100, 100), 10, 0, [Rectangle(20, 20, 20, 20)]);
        var error = Assert.Throws<ArgumentException>(() => new BoustrophedonPathGenerator().Generate(Rectangle(0, 0, 100, 100), options));
        Assert.Contains("obstrução", error.Message);
    }
}
