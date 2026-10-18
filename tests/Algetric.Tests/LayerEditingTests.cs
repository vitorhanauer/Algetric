using Algetric.Core;
using Xunit;

namespace Algetric.Tests;

public class LayerEditingTests
{
    [Fact]
    public void MovingAnObjectPreservesItsIdentityGeometrySelectionAndSupportsUndoRedo()
    {
        var editor = TestProjects.WithLayers();
        var shape = editor.Add(ShapeKind.Point, [new(10, 20)]);
        var target = editor.Document.Layers[1].Id;
        editor.MoveObjectToLayer(shape.Id, target);
        var moved = Assert.Single(editor.Document.Objects);
        Assert.Equal(shape with { LayerId = target }, moved);
        Assert.Equal(shape.Id, editor.SelectedId);
        editor.Undo(); Assert.Equal(shape, Assert.Single(editor.Document.Objects));
        editor.Redo(); Assert.Equal(moved, Assert.Single(editor.Document.Objects));
    }
    [Fact]
    public void InvalidAndUnchangedDestinationsLeaveTheDocumentAndHistoryUntouched()
    {
        var editor = TestProjects.WithLayers();
        var shape = editor.Add(ShapeKind.Point, [new(10, 20)]);
        var before = editor.Document;
        Assert.Throws<ArgumentException>(() => editor.MoveObjectToLayer(shape.Id, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => editor.MoveObjectToLayer(Guid.NewGuid(), shape.LayerId));
        editor.MoveObjectToLayer(shape.Id, shape.LayerId);
        Assert.Same(before, editor.Document);
        editor.Undo(); Assert.Empty(editor.Document.Objects);
    }
    [Fact]
    public void ChoosingAnActiveLayerDoesNotCreateAnUndoStep()
    {
        var editor = TestProjects.WithLayers(); var target = editor.Document.Layers[1].Id;
        editor.SetActiveLayer(target);
        Assert.False(editor.CanUndo); Assert.False(editor.IsDirty);
        var shape = editor.Add(ShapeKind.Point, [new(0, 0)]);
        Assert.Equal(target, shape.LayerId);
        Assert.Throws<ArgumentException>(() => editor.SetActiveLayer(Guid.NewGuid()));
        Assert.Equal(target, editor.ActiveLayerId);
    }
    [Fact]
    public void DeletingALayerRemovesItsObjectsAndUndoRestoresTheirStylesAndGeometry()
    {
        var editor = TestProjects.WithLayers(); var layer = editor.Document.Layers[0];
        var removed = editor.Add(ShapeKind.Point, [new(10, 20)]);
        editor.SetActiveLayer(editor.Document.Layers[1].Id);
        var kept = editor.Add(ShapeKind.Point, [new(30, 40)]);
        editor.SetActiveLayer(layer.Id); editor.Select(removed.Id);
        editor.DeleteLayer(layer.Id);
        Assert.Equal(kept, Assert.Single(editor.Document.Objects));
        Assert.Equal(kept.LayerId, editor.ActiveLayerId); Assert.Null(editor.SelectedId);
        editor.Undo();
        Assert.Equal(layer, editor.Document.Layers[0]);
        Assert.Equal(new[] { removed, kept }, editor.Document.Objects.ToArray());
        editor.Redo(); Assert.Equal(kept, Assert.Single(editor.Document.Objects));
    }
    [Fact]
    public void DeletingTheLastLayerReturnsToEmptyAndDeletingMissingLayersIsANoOp()
    {
        var editor = new ProjectEditor(); editor.AddLayer("Only layer", "#112233");
        var layer = Assert.Single(editor.Document.Layers);
        editor.DeleteLayer(layer.Id);
        Assert.Empty(editor.Document.Layers); Assert.Null(editor.ActiveLayerId);
        var before = editor.Document; editor.DeleteLayer(layer.Id); Assert.Same(before, editor.Document);
        editor.Undo(); Assert.Equal(layer, Assert.Single(editor.Document.Layers));
    }
    [Fact]
    public void DeletingAnotherLayerKeepsTheActiveLayerAndUnrelatedSelection()
    {
        var editor = TestProjects.WithLayers();
        var shape = editor.Add(ShapeKind.Point, [new(0, 0)]);
        editor.DeleteLayer(editor.Document.Layers[1].Id);
        Assert.Equal(shape.LayerId, editor.ActiveLayerId); Assert.Equal(shape.Id, editor.SelectedId);
    }
}
