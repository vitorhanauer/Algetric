using System.Collections.Immutable;
using Algetric.Core;
using Algetric.Infrastructure;
using Xunit;

namespace Algetric.Tests;

public class CoveragePathTests
{
    private static GeometryObject Polygon(params LocalPoint[] points) => new(Guid.NewGuid(), "Área", Guid.NewGuid(), ShapeKind.Polygon, points.ToImmutableArray());
    private static GeometryObject Square() => Polygon(new(0, 0), new(40, 0), new(40, 30), new(0, 30));
    [Fact]
    public void NativePlannerProducesAnInteriorRouteWithRequestedEndpoints()
    {
        if (!OperatingSystem.IsLinux()) return;
        var polygon = Square(); var options = new CoveragePathOptions(new(0, 0), new(40, 30), 5, 20);
        var route = new BoustrophedonPathGenerator().Generate(polygon, options);
        Assert.Equal(options.Start, route[0]); Assert.Equal(options.End, route[^1]);
        Assert.True(route.Length > 4);
        var line = new NetTopologySuite.Geometries.LineString(route.Select(p => new NetTopologySuite.Geometries.Coordinate(p.X, p.Y)).ToArray());
        Assert.True(GeometryRules.ToGeometry(polygon).Buffer(1e-7).Covers(line));
        var denser = new BoustrophedonPathGenerator().Generate(polygon, options with { OverlapPercent = 50 });
        Assert.True(denser.Length > route.Length);
    }
    [Fact]
    public void ConcavePolygonConnectorsRemainInsideTheArea()
    {
        if (!OperatingSystem.IsLinux()) return;
        var polygon = Polygon(new(0, 0), new(40, 0), new(40, 10), new(10, 10), new(10, 30), new(0, 30));
        var route = new BoustrophedonPathGenerator().Generate(polygon, new(new(0, 0), new(0, 30), 4, 10));
        var line = new NetTopologySuite.Geometries.LineString(route.Select(p => new NetTopologySuite.Geometries.Coordinate(p.X, p.Y)).ToArray());
        Assert.True(GeometryRules.ToGeometry(polygon).Buffer(1e-7).Covers(line));
    }
    [Theory]
    [InlineData(0, 0)] [InlineData(-1, 0)] [InlineData(double.NaN, 0)]
    [InlineData(5, -1)] [InlineData(5, 100)] [InlineData(5, double.PositiveInfinity)]
    public void InvalidWidthOrOverlapIsRejected(double width, double overlap)
        => Assert.ThrowsAny<ArgumentException>(() => new BoustrophedonPathGenerator().Generate(Square(), new(new(0, 0), new(10, 10), width, overlap)));
    [Fact]
    public void NonFiniteEndpointsAndNonPolygonsAreRejected()
    {
        var generator = new BoustrophedonPathGenerator();
        Assert.Throws<ArgumentException>(() => generator.Generate(Square(), new(new(double.NaN, 0), new(10, 10), 5, 0)));
        Assert.Throws<ArgumentException>(() => generator.Generate(Square(), new(new(0, 0), new(double.PositiveInfinity, 100), 5, 0)));
        var point = Square() with { Kind = ShapeKind.Point, Vertices = [new(0, 0)] };
        Assert.Throws<ArgumentException>(() => generator.Generate(point, new(new(0, 0), new(0, 0), 5, 0)));
    }
    [Theory]
    [InlineData(-10, 15, 50, 15)]
    [InlineData(-10, 15, 20, 15)]
    [InlineData(20, 15, 50, 15)]
    [InlineData(-10, 15, -20, 15)]
    public void OutsideEndpointsAreConnectedToAnInteriorCoveragePath(double startX, double startY, double endX, double endY)
    {
        if (!OperatingSystem.IsLinux()) return;
        var polygon = Square(); var options = new CoveragePathOptions(new(startX, startY), new(endX, endY), 5, 20);
        var route = new BoustrophedonPathGenerator().Generate(polygon, options);
        Assert.Equal(options.Start, route[0]); Assert.Equal(options.End, route[^1]);
        var startOffset = startX < 0 ? 1 : 0; var endOffset = endX < 0 || endX > 40 ? 1 : 0;
        var interior = route.Skip(startOffset).Take(route.Length - startOffset - endOffset).ToArray();
        Assert.True(interior.Length > 4);
        var line = new NetTopologySuite.Geometries.LineString(interior.Select(p => new NetTopologySuite.Geometries.Coordinate(p.X, p.Y)).ToArray());
        Assert.True(GeometryRules.ToGeometry(polygon).Buffer(1e-7).Covers(line));
    }
    [Fact]
    public void OutsideEndpointsAlsoWorkWithSlopingPolygonEdges()
    {
        if (!OperatingSystem.IsLinux()) return;
        var polygon = Polygon(new(0, 0), new(40, 0), new(20, 30));
        var options = new CoveragePathOptions(new(35, 30), new(-10, 15), 4, 10);
        var route = new BoustrophedonPathGenerator().Generate(polygon, options);
        Assert.Equal(options.Start, route[0]); Assert.Equal(options.End, route[^1]);
        var interior = route.Skip(1).Take(route.Length - 2).Select(p => new NetTopologySuite.Geometries.Coordinate(p.X, p.Y)).ToArray();
        Assert.True(GeometryRules.ToGeometry(polygon).Buffer(1e-7).Covers(new NetTopologySuite.Geometries.LineString(interior)));
    }

}
