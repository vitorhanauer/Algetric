using System.Runtime.InteropServices;
using Algetric.Core;
using BoustrophedonDotnetWrapper;
using NetTopologySuite.Geometries;
using NativePoint = BoustrophedonDotnetWrapper.Point;

namespace Algetric.Infrastructure;

public sealed class BoustrophedonPathGenerator : ICoveragePathGenerator
{
    public LocalPoint[] Generate(GeometryObject polygon, CoveragePathOptions options)
    {
        var area = Validate(polygon, options);
        var obstructions = CoverageObstructions.Filter(area, options);
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("A geração de caminhos deste pacote está disponível apenas no Linux x64.");
        var interiorOptions = options with { Start = EntryPoint(area, options.Start), End = EntryPoint(area, options.End) };
        var coverage = GenerateInterior(polygon, area, interiorOptions, obstructions);
        var freeArea = obstructions.Aggregate(area, (free, obstruction) => free.Difference(GeometryRules.ToGeometry(obstruction)));
        ValidateRoute(freeArea, coverage);
        var route = new List<LocalPoint>();
        AppendPoint(route, options.Start);
        foreach (var point in coverage) AppendPoint(route, point);
        AppendPoint(route, options.End);
        return route.ToArray();
    }
    private static LocalPoint EntryPoint(Geometry area, LocalPoint requested)
    {
        var point = new NetTopologySuite.Geometries.Point(requested.X, requested.Y);
        if (area.Covers(point)) return requested;
        var nearest = NetTopologySuite.Operation.Distance.DistanceOp.NearestPoints(area.Boundary, point)[0];
        return new(nearest.X, nearest.Y);
    }
    private static List<LocalPoint> GenerateInterior(GeometryObject polygon, Geometry area, CoveragePathOptions options, GeometryObject[] obstructions)
    {
        var center = area.EnvelopeInternal.Centre;
        NativePoint Native(LocalPoint point) => new(point.X - center.X, point.Y - center.Y);
        var result = Boustrophedon.GenerateWaypoints(Native(options.Start), Native(options.End),
            polygon.Vertices.Select(Native).ToArray(), obstructions.Select(shape => shape.Vertices.Select(Native).ToArray()).ToArray(),
            options.Width, options.OverlapPercent);
        var route = new List<LocalPoint>();
        foreach (var point in result)
        {
            var local = new LocalPoint(point.X + center.X, point.Y + center.Y);
            AppendPoint(route, local);
        }
        if (route.Count < 2) throw new InvalidOperationException("Não foi possível gerar um caminho para esta área.");
        route[0] = options.Start; route[^1] = options.End;
        return route;
    }
    private static void AppendPoint(List<LocalPoint> route, LocalPoint point)
    {
        if (route.Count == 0 || route[^1] != point) route.Add(point);
    }
    private static Geometry Validate(GeometryObject polygon, CoveragePathOptions options)
    {
        if (polygon.Kind != ShapeKind.Polygon) throw new ArgumentException("Selecione um polígono.");
        GeometryRules.Validate(polygon);
        if (!double.IsFinite(options.Width) || options.Width <= 0)
            throw new ArgumentException("A largura deve ser maior que zero.");
        if (!double.IsFinite(options.OverlapPercent) || options.OverlapPercent < 0 || options.OverlapPercent >= 100)
            throw new ArgumentException("A sobreposição deve ser de 0% a menos de 100%.");
        var area = GeometryRules.ToGeometry(polygon);
        ValidateEndpoint(options.Start, "inicial"); ValidateEndpoint(options.End, "final");
        var spacing = options.Width * (1 - options.OverlapPercent / 100);
        if (spacing <= 0 || area.EnvelopeInternal.Width / spacing > 10_000)
            throw new ArgumentException("O caminho teria faixas demais. Aumente a largura ou reduza a sobreposição.");
        return area;
    }
    private static void ValidateEndpoint(LocalPoint point, string label)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || Math.Sqrt(point.X * point.X + point.Y * point.Y) > 50_000)
            throw new ArgumentException($"O ponto {label} deve ter coordenadas finitas e estar a até 50 km da origem.");
    }
    private static void ValidateRoute(Geometry area, List<LocalPoint> route)
    {
        if (route.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            throw new InvalidOperationException("O gerador retornou coordenadas inválidas.");
        var line = new LineString(route.Select(point => new Coordinate(point.X, point.Y)).ToArray());
        var tolerance = Math.Max(1e-7, area.EnvelopeInternal.Diameter * 1e-9);
        if (!area.Buffer(tolerance).Covers(line))
            throw new InvalidOperationException("O gerador retornou um trecho fora da área livre. Tente outra largura ou pontos de entrada e saída.");
    }
}
