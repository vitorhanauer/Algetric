using NetTopologySuite.Geometries;

namespace Algetric.Core;

public static class GeometryRules
{
    public static void ValidateOrigin(GeographicPoint origin)
    {
        if (!double.IsFinite(origin.Latitude) || !double.IsFinite(origin.Longitude)
            || Math.Abs(origin.Latitude) > 80 || Math.Abs(origin.Longitude) > 180)
            throw new ArgumentException("A origem deve estar entre ±80° de latitude e ±180° de longitude.");
    }

    public static void Validate(GeometryObject shape)
    {
        if (shape.Vertices.IsDefault || shape.Vertices.Any(p => !double.IsFinite(p.X)
            || !double.IsFinite(p.Y) || Math.Sqrt(p.X * p.X + p.Y * p.Y) > 50_000))
            throw new ArgumentException("As coordenadas devem ser finitas e estar a até 50 km da origem.");
        var count = shape.Vertices.Length;
        if (!Enum.IsDefined(shape.Kind) || (shape.Kind == ShapeKind.Point ? count != 1
            : shape.Kind == ShapeKind.Segment ? count != 2
            : shape.Kind == ShapeKind.Polyline ? count < 2 : count < 3))
            throw new ArgumentException("Quantidade de vértices incompatível com o objeto.");
        if (shape.Vertices.Distinct().Count() != count)
            throw new ArgumentException("Vértices repetidos não são permitidos.");
        var geometry = ToGeometry(shape);
        if (!geometry.IsValid || (shape.Kind == ShapeKind.Polygon && geometry.Area <= 0))
            throw new ArgumentException("Polígono degenerado ou com autointerseção.");
    }

    public static Geometry ToGeometry(GeometryObject shape)
    {
        var coordinates = shape.Vertices.Select(p => new Coordinate(p.X, p.Y)).ToArray();
        var factory = new GeometryFactory();
        return shape.Kind switch
        {
            ShapeKind.Point => factory.CreatePoint(coordinates[0]),
            ShapeKind.Polygon => factory.CreatePolygon([.. coordinates, coordinates[0].Copy()]),
            _ => factory.CreateLineString(coordinates)
        };
    }

    public static (double Length, double Area) Measure(GeometryObject shape)
    {
        var geometry = ToGeometry(shape);
        return (geometry.Length, geometry.Area);
    }

    public static void ValidateDocument(ProjectDocument document)
    {
        if (document.Version != 1) throw new ArgumentException("Versão de projeto incompatível.");
        ValidateOrigin(document.Origin);
        if (document.Layers.IsDefaultOrEmpty || document.Objects.IsDefault || document.View is null)
            throw new ArgumentException("Projeto incompleto.");
        if (document.Layers.Select(l => l.Id).Distinct().Count() != document.Layers.Length
            || document.Objects.Select(o => o.Id).Distinct().Count() != document.Objects.Length)
            throw new ArgumentException("Identificadores duplicados.");
        foreach (var layer in document.Layers)
            if (string.IsNullOrWhiteSpace(layer.Name) || !System.Text.RegularExpressions.Regex.IsMatch(layer.Color ?? "", "^#[0-9a-fA-F]{6}$"))
                throw new ArgumentException("Camada inválida.");
        foreach (var shape in document.Objects)
        {
            if (!document.Layers.Any(l => l.Id == shape.LayerId)) throw new ArgumentException("Camada inexistente.");
            Validate(shape);
        }
        var v = document.View;
        if (!double.IsFinite(v.CenterX) || !double.IsFinite(v.CenterY) || !double.IsFinite(v.PixelsPerMeter)
            || v.PixelsPerMeter <= 0 || !double.IsFinite(v.Longitude) || !double.IsFinite(v.Latitude)
            || Math.Abs(v.Latitude) > 90 || Math.Abs(v.Longitude) > 180 || !double.IsFinite(v.Height) || v.Height <= 0)
            throw new ArgumentException("Visualização inválida.");
    }
}
