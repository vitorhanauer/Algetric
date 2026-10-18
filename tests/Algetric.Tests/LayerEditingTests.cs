using Algetric.Core;
using Xunit;

namespace Algetric.Tests;

public class LayerEditingTests
{
    [Fact]
    public void MovingAnObjectPreservesItsIdentityGeometrySelectionAndSupportsUndoRedo()
    {
        var editor = new ProjectEditor();
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
        var editor = new ProjectEditor();
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
        var editor = new ProjectEditor(); var target = editor.Document.Layers[1].Id;
        editor.SetActiveLayer(target);
        Assert.False(editor.CanUndo); Assert.False(editor.IsDirty);
        var shape = editor.Add(ShapeKind.Point, [new(0, 0)]);
        Assert.Equal(target, shape.LayerId);
        Assert.Throws<ArgumentException>(() => editor.SetActiveLayer(Guid.NewGuid()));
        Assert.Equal(target, editor.ActiveLayerId);
    }
}
