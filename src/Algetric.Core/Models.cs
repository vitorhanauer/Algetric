using System.Collections.Immutable;

namespace Algetric.Core;

public readonly record struct LocalPoint(double X, double Y);
public readonly record struct GeographicPoint(double Latitude, double Longitude);
public enum ShapeKind { Point, Segment, Polyline, Polygon }
public sealed record Layer(Guid Id, string Name, string Color, bool Visible = true);
public sealed record GeometryObject(Guid Id, string Name, Guid LayerId, ShapeKind Kind,
    ImmutableArray<LocalPoint> Vertices);
public sealed record ViewState(double CenterX = 150, double CenterY = 150, double PixelsPerMeter = 2,
    double Longitude = -46.63, double Latitude = -23.55, double Height = 20_000_000);
public sealed record ProjectDocument(int Version, GeographicPoint Origin, ImmutableArray<Layer> Layers,
    ImmutableArray<GeometryObject> Objects, ViewState View, bool OriginLocked = false)
{
    public static ProjectDocument Empty() => new(1, new(-23.55, -46.63), [], [], new());
}
public interface ICoordinateTransform : IDisposable
{
    LocalPoint ToLocal(GeographicPoint point);
    GeographicPoint ToGeographic(LocalPoint point);
}
public interface IProjectRepository
{
    Task<ProjectDocument> LoadAsync(string path);
    Task SaveAsync(string path, ProjectDocument document);
}
