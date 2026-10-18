using Algetric.Core;
using Xunit;
namespace Algetric.Tests;

public class AtomicEditTests
{
    [Fact]
    public void PropertiesAreChangedWithOneUndoAndInvalidNamesDoNotMoveVertices()
    {
        var editor = new ProjectEditor(); var shape = editor.Add(ShapeKind.Point, [new(1, 2)]);
        Assert.Throws<ArgumentException>(() => editor.UpdateObject(shape.Id, "", [new(5, 6)]));
        Assert.Equal(new LocalPoint(1, 2), editor.Document.Objects[0].Vertices[0]);
        editor.UpdateObject(shape.Id, "Novo", [new(5, 6)]); editor.Undo();
        Assert.Equal(shape.Name, editor.Document.Objects[0].Name);
        Assert.Equal(new LocalPoint(1, 2), editor.Document.Objects[0].Vertices[0]);
    }
    [Fact]
    public void RepeatedPropertyConfirmationDoesNotAddAnUndoStepOrClearRedo()
    {
        var editor = new ProjectEditor();
        var shape = editor.Add(ShapeKind.Point, [new(1, 2)]);
        editor.UpdateObject(shape.Id, "Renamed", [new(5, 6)]);
        editor.UpdateObject(shape.Id, "Renamed", [new(5, 6)]);
        editor.Undo();
        Assert.Equal(shape, editor.Document.Objects[0]);
        editor.UpdateObject(shape.Id, shape.Name, shape.Vertices);
        Assert.True(editor.CanRedo);
        editor.Redo();
        Assert.Equal("Renamed", editor.Document.Objects[0].Name);
    }
    [Fact]
    public void DeletingARowPreservesOtherSelectionAndMissingRowsDoNotChangeHistory()
    {
        var editor = new ProjectEditor();
        var first = editor.Add(ShapeKind.Point, [new(1, 2)]);
        var second = editor.Add(ShapeKind.Point, [new(3, 4)]);
        editor.Delete(first.Id);
        Assert.Equal(second.Id, editor.SelectedId);
        Assert.Equal(second, Assert.Single(editor.Document.Objects));
        editor.Delete(first.Id);
        editor.Undo();
        Assert.Equal(new[] { first, second }, editor.Document.Objects.ToArray());
        editor.Select(first.Id); editor.Delete(first.Id);
        Assert.Null(editor.SelectedId);
    }
}
