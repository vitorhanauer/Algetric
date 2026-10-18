using Algetric.Core;
using NetTopologySuite.Geometries;

namespace Algetric.Infrastructure;

internal static class CoverageObstructions
{
    public static GeometryObject[] Filter(Geometry area, CoveragePathOptions options)
    {
        var accepted = new List<GeometryObject>();
        var geometries = new List<Geometry>();
        foreach (var obstruction in (options.Obstructions ?? []).DistinctBy(shape => shape.Id))
        {
            if (obstruction.Kind != ShapeKind.Polygon) throw new ArgumentException("As obstruções devem ser polígonos.");
            GeometryRules.Validate(obstruction);
            var geometry = GeometryRules.ToGeometry(obstruction);
            // The native planner requires strictly internal, mutually disjoint obstacles.
            if (!area.Contains(geometry) || !area.Boundary.Disjoint(geometry)) continue;
            if (geometries.Any(other => !other.Disjoint(geometry)))
                throw new ArgumentException("As obstruções internas não podem se sobrepor nem se tocar.");
            if (geometry.Contains(new Point(options.Start.X, options.Start.Y)) || geometry.Contains(new Point(options.End.X, options.End.Y)))
                throw new ArgumentException("Os pontos inicial e final não podem estar dentro de uma obstrução.");
            accepted.Add(obstruction); geometries.Add(geometry);
        }
        return accepted.ToArray();
    }
}
