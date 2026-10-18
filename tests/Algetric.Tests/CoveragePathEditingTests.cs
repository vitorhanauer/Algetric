using Algetric.Core;
using Algetric.Infrastructure;
using Xunit;

namespace Algetric.Tests;

public class CoveragePathEditingTests
{
    [Fact]
    public void RouteUsesThePolygonLayerAndIsOneUndoableOperation()
    {
        var editor = TestProjects.WithLayers();
        var polygon = editor.Add(ShapeKind.Polygon, [new(0, 0), new(20, 0), new(20, 20), new(0, 20)]);
        editor.AddLayer("Outra camada", "#FF0000"); var active = editor.ActiveLayerId;
        var original = editor.Document;
        var route = editor.AddCoveragePath(polygon.Id, [new(0, 0), new(0, 20), new(5, 20), new(5, 0), new(0, 0)]);
        Assert.Equal(ShapeKind.CoveragePath, route.Kind);
        Assert.Equal(polygon.LayerId, route.LayerId); Assert.Equal(active, editor.ActiveLayerId);
        Assert.Equal(route.Id, editor.SelectedId);
        Assert.Equal(polygon, editor.Document.Objects[0]); GeometryRules.Validate(route);
        editor.Undo(); Assert.Equal(original.Objects, editor.Document.Objects);
        editor.Redo(); Assert.Equal(route, editor.Document.Objects.Last());
    }
    [Fact]
    public void MissingPolygonAndInvalidRouteLeaveTheDocumentUnchanged()
    {
        var editor = TestProjects.WithLayers();
        var polygon = editor.Add(ShapeKind.Polygon, [new(0, 0), new(10, 0), new(10, 10)]);
        var original = editor.Document;
        Assert.Throws<ArgumentException>(() => editor.AddCoveragePath(Guid.NewGuid(), [new(0, 0), new(1, 1)]));
        Assert.Throws<ArgumentException>(() => editor.AddCoveragePath(polygon.Id, [new(0, 0)]));
        Assert.Same(original, editor.Document); Assert.Equal(polygon.Id, editor.SelectedId);
    }
    [Fact]
    public async Task APathWithRevisitedWaypointsCanBeSavedAndReloaded()
    {
        var editor = TestProjects.WithLayers();
        var polygon = editor.Add(ShapeKind.Polygon, [new(0, 0), new(10, 0), new(10, 10)]);
        var path = editor.AddCoveragePath(polygon.Id, [new(0, 0), new(10, 0), new(10, 10), new(0, 0)]);
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".algetric");
        try
        {
            var repository = new JsonProjectRepository(); await repository.SaveAsync(file, editor.Document);
            var saved = (await repository.LoadAsync(file)).Objects.Last();
            Assert.Equal(path.Kind, saved.Kind); Assert.True(path.Vertices.SequenceEqual(saved.Vertices));
        }
        finally { File.Delete(file); }
    }
}
