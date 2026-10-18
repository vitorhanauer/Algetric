using Algetric.Core;

namespace Algetric.Desktop;

public sealed partial class EditorViewModel
{
    public event Action<Guid?>? FunctionGraphRequested;
    public void RequestFunctionGraph(Guid? id = null)
    {
        if (id is null && !EnsureActiveLayer()) return;
        if (id is not null && !Editor.Document.Objects.Any(shape => shape.Id == id && shape.Kind == ShapeKind.FunctionGraph))
            throw new ArgumentException("Selecione um gráfico de função existente.");
        FunctionGraphRequested?.Invoke(id);
    }
    public GeometryObject SaveFunctionGraph(Guid? id, string name, string expression, double minimumX, double maximumX)
    {
        var definition = new FunctionDefinition(expression, minimumX, maximumX);
        ValidateGeographic(definition.GetSegments().SelectMany(segment => segment));
        VertexIndex = 0;
        if (id is null) return Editor.AddFunctionGraph(expression, minimumX, maximumX, name);
        Editor.UpdateFunctionGraph(id.Value, name, expression, minimumX, maximumX);
        return Editor.Document.Objects.First(shape => shape.Id == id);
    }
}
