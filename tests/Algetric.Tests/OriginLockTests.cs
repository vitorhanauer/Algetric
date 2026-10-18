using Algetric.Core;
using Xunit;
namespace Algetric.Tests;

public class OriginLockTests
{
    [Fact]
    public void OriginRemainsLockedAfterDeletingAllObjectsAndReopening()
    {
        var editor = TestProjects.WithLayers(); editor.Add(ShapeKind.Point, [new(0, 0)]); editor.DeleteSelected();
        var reopened = new ProjectEditor(); reopened.Load(editor.Document);
        Assert.Throws<InvalidOperationException>(() => reopened.SetOrigin(new(0, 0)));
    }
}
