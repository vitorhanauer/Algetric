using System.Collections.Immutable;

namespace Algetric.Core;

public sealed class ProjectEditor
{
    private readonly Stack<ProjectDocument> undo = new();
    private readonly Stack<ProjectDocument> redo = new();
    private ProjectDocument saved;
    public ProjectDocument Document { get; private set; }
    public ImmutableHashSet<Guid> SelectedIds { get; private set; } = [];
    public Guid? SelectedId => SelectedIds.Count == 1 ? SelectedIds.Single() : null;
    public Guid? ActiveLayerId { get; private set; }
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public bool IsDirty => Document != saved;
    public event Action? Changed;
    public event Action? ViewChanged;

    public ProjectEditor() { Document = ProjectDocument.Empty(); saved = Document; ActiveLayerId = Document.Layers.FirstOrDefault()?.Id; }
    public void Select(Guid? id) => SelectMany(id is { } selected ? [selected] : []);
    public void SelectMany(IEnumerable<Guid> ids)
    {
        SelectedIds = ids.Intersect(Document.Objects.Select(shape => shape.Id)).ToImmutableHashSet();
        Changed?.Invoke();
    }
    public void SelectArea(LocalPoint start, LocalPoint end)
    {
        var left = Math.Min(start.X, end.X); var right = Math.Max(start.X, end.X);
        var bottom = Math.Min(start.Y, end.Y); var top = Math.Max(start.Y, end.Y);
        var visibleLayers = Document.Layers.Where(layer => layer.Visible).Select(layer => layer.Id).ToHashSet();
        SelectMany(Document.Objects.Where(shape => visibleLayers.Contains(shape.LayerId) &&
            shape.Vertices.All(point => point.X >= left && point.X <= right && point.Y >= bottom && point.Y <= top))
            .Select(shape => shape.Id));
    }
    public void MarkSaved() { saved = Document; Changed?.Invoke(); }
    public void Load(ProjectDocument document)
    {
        GeometryRules.ValidateDocument(document);
        Document = saved = document with { OriginLocked = document.OriginLocked || document.Objects.Length > 0 };
        undo.Clear(); redo.Clear(); SelectedIds = []; ActiveLayerId = document.Layers.FirstOrDefault()?.Id; Changed?.Invoke();
    }
    public void SetOrigin(GeographicPoint origin)
    {
        if (Document.OriginLocked) throw new InvalidOperationException("A origem fica fixa após criar o primeiro objeto.");
        GeometryRules.ValidateOrigin(origin); Commit(Document with { Origin = origin });
    }
    public GeometryObject Add(ShapeKind kind, IEnumerable<LocalPoint> vertices)
    {
        if (ActiveLayerId is not { } layerId) throw new InvalidOperationException("Crie uma camada pelo botão + antes de criar objetos.");
        var shape = new GeometryObject(Guid.NewGuid(), $"{Name(kind)} {Document.Objects.Length + 1}",
            layerId, kind, vertices.ToImmutableArray());
        GeometryRules.Validate(shape);
        SelectedIds = [shape.Id]; Commit(Document with { Objects = Document.Objects.Add(shape), OriginLocked = true });
        return shape;
    }
    public void ReplaceVertices(Guid id, IEnumerable<LocalPoint> vertices)
    {
        var index = Document.Objects.FindIndex(o => o.Id == id);
        if (index < 0) throw new ArgumentException("Objeto inexistente.");
        var shape = Document.Objects[index] with { Vertices = vertices.ToImmutableArray() };
        GeometryRules.Validate(shape); Commit(Document with { Objects = Document.Objects.SetItem(index, shape) });
    }
    public void UpdateObject(Guid id, string name, IEnumerable<LocalPoint> vertices)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Informe um nome.");
        var index = Document.Objects.FindIndex(o => o.Id == id);
        if (index < 0) throw new ArgumentException("Objeto inexistente.");
        var shape = Document.Objects[index] with { Name = name.Trim(), Vertices = vertices.ToImmutableArray() };
        GeometryRules.Validate(shape);
        if (shape.Name == Document.Objects[index].Name && shape.Vertices.SequenceEqual(Document.Objects[index].Vertices)) return;
        Commit(Document with { Objects = Document.Objects.SetItem(index, shape) });
    }
    public void DeleteSelected()
    {
        if (SelectedIds.Count == 0) return;
        Commit(Document with { Objects = Document.Objects.RemoveAll(shape => SelectedIds.Contains(shape.Id)) });
    }
    public void Delete(Guid id)
    {
        if (!Document.Objects.Any(o => o.Id == id)) return;
        Commit(Document with { Objects = Document.Objects.RemoveAll(o => o.Id == id) });
    }
    public void AddLayer(string name, string color)
    {
        var layer = new Layer(Guid.NewGuid(), name, color);
        Commit(Document with { Layers = Document.Layers.Add(layer) }, layer.Id);
    }
    public void DeleteLayer(Guid layerId)
    {
        if (!Document.Layers.Any(layer => layer.Id == layerId)) return;
        var layers = Document.Layers.RemoveAll(layer => layer.Id == layerId);
        var objects = Document.Objects.RemoveAll(shape => shape.LayerId == layerId);
        if (ActiveLayerId == layerId) ActiveLayerId = layers.FirstOrDefault()?.Id;
        Commit(Document with { Layers = layers, Objects = objects });
    }
    public void MoveObjectToLayer(Guid objectId, Guid layerId)
    {
        var index = Document.Objects.FindIndex(shape => shape.Id == objectId);
        if (index < 0) throw new ArgumentException("Objeto inexistente.");
        if (!Document.Layers.Any(layer => layer.Id == layerId)) throw new ArgumentException("Camada inexistente.");
        var shape = Document.Objects[index];
        if (shape.LayerId == layerId) return;
        Commit(Document with { Objects = Document.Objects.SetItem(index, shape with { LayerId = layerId }) });
    }
    public void SetActiveLayer(Guid layerId)
    {
        if (!Document.Layers.Any(layer => layer.Id == layerId)) throw new ArgumentException("Camada inexistente.");
        if (ActiveLayerId == layerId) return;
        ActiveLayerId = layerId; Changed?.Invoke();
    }
    public void UpdateLayer(Layer layer)
    {
        var index = Document.Layers.FindIndex(l => l.Id == layer.Id);
        if (index >= 0) Commit(Document with { Layers = Document.Layers.SetItem(index, layer) });
    }
    public void UpdateView(ViewState view) { Document = Document with { View = view }; ViewChanged?.Invoke(); }
    public void Undo() => Restore(undo, redo);
    public void Redo() => Restore(redo, undo);
    private void Restore(Stack<ProjectDocument> source, Stack<ProjectDocument> target)
    {
        if (!source.TryPop(out var document)) return;
        target.Push(Document);
        Document = document with { OriginLocked = Document.OriginLocked || document.OriginLocked };
        PruneSelection();
        if (!Document.Layers.Any(l => l.Id == ActiveLayerId)) ActiveLayerId = Document.Layers.FirstOrDefault()?.Id;
        Changed?.Invoke();
    }
    private void Commit(ProjectDocument next, Guid? activeLayerId = null)
    {
        GeometryRules.ValidateDocument(next); undo.Push(Document); redo.Clear(); Document = next;
        PruneSelection();
        if (activeLayerId is { } layerId) ActiveLayerId = layerId;
        Changed?.Invoke();
    }
    private void PruneSelection() => SelectedIds = SelectedIds.Intersect(Document.Objects.Select(shape => shape.Id));
    private static string Name(ShapeKind kind) => kind switch
    { ShapeKind.Point => "Ponto", ShapeKind.Segment => "Segmento", ShapeKind.Polyline => "Linha", _ => "Polígono" };
}
internal static class ImmutableExtensions
{
    public static int FindIndex<T>(this ImmutableArray<T> items, Predicate<T> predicate)
    { for (var i = 0; i < items.Length; i++) if (predicate(items[i])) return i; return -1; }
}
