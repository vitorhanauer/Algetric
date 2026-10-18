using Avalonia;
using Algetric.Core;

namespace Algetric.Desktop;

public sealed partial class CartesianCanvas
{
    private GeometryObject? PolygonAt(Point position)
    {
        var local = ToLocal(position);
        var point = new NetTopologySuite.Geometries.Point(local.X, local.Y);
        return model.Editor.Document.Objects.Reverse().FirstOrDefault(shape => shape.Kind == ShapeKind.Polygon &&
            model.Editor.Document.Layers.Any(layer => layer.Id == shape.LayerId && layer.Visible) &&
            GeometryRules.ToGeometry(shape).Covers(point));
    }
}
