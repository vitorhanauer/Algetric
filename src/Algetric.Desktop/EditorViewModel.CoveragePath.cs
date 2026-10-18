using Algetric.Core;

namespace Algetric.Desktop;

public sealed partial class EditorViewModel
{
    public void RequestCoveragePath(Guid? polygonId)
    {
        if (polygonId is not null && !Editor.Document.Objects.Any(shape => shape.Id == polygonId && shape.Kind == ShapeKind.Polygon))
            throw new ArgumentException("Selecione um polígono existente.");
        CoveragePathRequested?.Invoke(polygonId);
    }
    public async Task<GeometryObject> GenerateCoveragePathAsync(Guid polygonId, CoveragePathOptions options, CancellationToken cancellationToken = default)
    {
        var session = gate.Session;
        var polygon = Editor.Document.Objects.FirstOrDefault(shape => shape.Id == polygonId && shape.Kind == ShapeKind.Polygon)
            ?? throw new ArgumentException("Selecione um polígono existente.");
        var route = await Task.Run(() => pathGenerator.Generate(polygon, options), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (gate.Session != session || !Editor.Document.Objects.Contains(polygon)) throw new InvalidOperationException("O polígono foi alterado durante a geração. Tente novamente.");
        ValidateGeographic(route);
        VertexIndex = 0;
        return Editor.AddCoveragePath(polygonId, route);
    }
}
