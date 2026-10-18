namespace Algetric.Core;

public sealed partial class ProjectEditor
{
    public GeometryObject AddFunctionGraph(string expression, double minimumX, double maximumX, string? name = null)
    {
        if (ActiveLayerId is not { } layerId) throw new InvalidOperationException(MissingLayerMessage);
        var function = new FunctionDefinition(expression, minimumX, maximumX);
        var shape = new GeometryObject(Guid.NewGuid(), string.IsNullOrWhiteSpace(name) ? $"f(x) = {function.Expression}" : name.Trim(),
            layerId, ShapeKind.FunctionGraph, [], Function: function);
        GeometryRules.Validate(shape); SelectedIds = [shape.Id];
        Commit(Document with { Objects = Document.Objects.Add(shape), OriginLocked = true }); return shape;
    }
    public void UpdateFunctionGraph(Guid id, string name, string expression, double minimumX, double maximumX)
    {
        var index = Document.Objects.FindIndex(shape => shape.Id == id && shape.Kind == ShapeKind.FunctionGraph);
        if (index < 0) throw new ArgumentException("Selecione um gráfico de função existente.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Informe um nome.");
        var function = new FunctionDefinition(expression, minimumX, maximumX);
        var previous = Document.Objects[index];
        if (name.Trim() == $"f(x) = {previous.Function!.Expression}") name = $"f(x) = {function.Expression}";
        if (previous.Name == name.Trim() && previous.Function!.Expression == function.Expression &&
            previous.Function.MinimumX == minimumX && previous.Function.MaximumX == maximumX) return;
        Commit(Document with { Objects = Document.Objects.SetItem(index, previous with { Name = name.Trim(), Function = function }) });
    }
}
