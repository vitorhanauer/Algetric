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
}
