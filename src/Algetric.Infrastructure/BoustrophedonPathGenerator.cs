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
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("A geração de caminhos deste pacote está disponível apenas no Linux x64.");
        var center = area.EnvelopeInternal.Centre;
        NativePoint Native(LocalPoint point) => new(point.X - center.X, point.Y - center.Y);
        var result = Boustrophedon.GenerateWaypoints(Native(options.Start), Native(options.End),
            polygon.Vertices.Select(Native).ToArray(), [], options.Width, options.OverlapPercent);
        var route = new List<LocalPoint>();
        foreach (var point in result)
        {
            var local = new LocalPoint(point.X + center.X, point.Y + center.Y);
            if (route.Count == 0 || route[^1] != local) route.Add(local);
        }
        if (route.Count < 2) throw new InvalidOperationException("Não foi possível gerar um caminho para esta área.");
        route[0] = options.Start; route[^1] = options.End;
        ValidateRoute(area, route);
        return route.ToArray();
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
        ValidateEndpoint(area, options.Start, "inicial"); ValidateEndpoint(area, options.End, "final");
        var spacing = options.Width * (1 - options.OverlapPercent / 100);
        if (spacing <= 0 || area.EnvelopeInternal.Width / spacing > 10_000)
            throw new ArgumentException("O caminho teria faixas demais. Aumente a largura ou reduza a sobreposição.");
        return area;
    }
    private static void ValidateEndpoint(Geometry area, LocalPoint point, string label)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !area.Covers(new NetTopologySuite.Geometries.Point(point.X, point.Y)))
            throw new ArgumentException($"O ponto {label} deve estar dentro do polígono ou na sua borda.");
    }
    private static void ValidateRoute(Geometry area, List<LocalPoint> route)
    {
        if (route.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            throw new InvalidOperationException("O gerador retornou coordenadas inválidas.");
        var line = new LineString(route.Select(point => new Coordinate(point.X, point.Y)).ToArray());
        var tolerance = Math.Max(1e-7, area.EnvelopeInternal.Diameter * 1e-9);
        if (!area.Buffer(tolerance).Covers(line))
            throw new InvalidOperationException("O gerador retornou um trecho fora do polígono. Tente outra largura ou pontos de entrada e saída.");
    }
}
