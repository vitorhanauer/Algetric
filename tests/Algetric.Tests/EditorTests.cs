using Algetric.Core;
using Algetric.Infrastructure;
using Xunit;

namespace Algetric.Tests;

public class EditorTests
{
    [Fact]
    public void InvalidPolygonLeavesDocumentAndHistoryUntouched()
    {
        var editor = TestProjects.WithLayers();
        Assert.Throws<ArgumentException>(() => editor.Add(ShapeKind.Polygon,
            [new(0, 0), new(10, 10), new(0, 10), new(10, 0)]));
        Assert.Empty(editor.Document.Objects);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void SquareMeasurementsAndUndoRedoAreStable()
    {
        var editor = TestProjects.WithLayers();
        var shape = editor.Add(ShapeKind.Polygon, [new(0, 0), new(10, 0), new(10, 10), new(0, 10)]);
        Assert.Equal(100, GeometryRules.Measure(shape).Area);
        Assert.Equal(40, GeometryRules.Measure(shape).Length);
        editor.Undo(); Assert.Empty(editor.Document.Objects);
        editor.Redo(); Assert.Equal(shape.Id, Assert.Single(editor.Document.Objects).Id);
    }

    [Fact]
    public void MoveIsAtomicAndOriginLocksAfterFirstObject()
    {
        var editor = TestProjects.WithLayers();
        var shape = editor.Add(ShapeKind.Point, [new(1, 2)]);
        editor.ReplaceVertices(shape.Id, [new(3, 4)]);
        editor.Undo(); Assert.Equal(new LocalPoint(1, 2), editor.Document.Objects[0].Vertices[0]);
        Assert.Throws<InvalidOperationException>(() => editor.SetOrigin(new(-22, -43)));
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(50_001, 0)]
    [InlineData(40_000, 40_000)]
    public void InvalidCoordinatesAreRejected(double x, double y)
        => Assert.Throws<ArgumentException>(() => TestProjects.WithLayers().Add(ShapeKind.Point, [new(x, y)]));

    [Fact]
    public void ProjMatchesKnownEquatorialDistanceAndRoundTrips()
    {
        using var transform = new ProjCoordinateTransform(new(0, 0));
        var local = transform.ToLocal(new(0, 0.1));
        Assert.InRange(local.X, 11131.949, 11131.950);
        Assert.InRange(Math.Abs(local.Y), 0, 0.001);
        var geographic = transform.ToGeographic(local);
        Assert.InRange(Math.Abs(geographic.Longitude - 0.1), 0, 0.00000001);
    }

    [Fact]
    public void ProjectionRoundTripsAtBrazilianOrigin()
    {
        using var transform = new ProjCoordinateTransform(new(-23.55, -46.63));
        var start = new LocalPoint(30000, -20000);
        var end = transform.ToLocal(transform.ToGeographic(start));
        Assert.InRange(Math.Abs(start.X - end.X), 0, 0.01);
        Assert.InRange(Math.Abs(start.Y - end.Y), 0, 0.01);
    }

    [Fact]
    public async Task ProjectRoundTripAndUnsupportedFormat()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "example.algetric");
            var editor = TestProjects.WithLayers(); editor.Add(ShapeKind.Point, [new(12, 34)]);
            var repository = new JsonProjectRepository();
            await repository.SaveAsync(path, editor.Document);
            var loaded = await repository.LoadAsync(path);
            Assert.Equal(editor.Document.Objects[0], loaded.Objects[0], ShapeComparer.Instance);
            await File.WriteAllTextAsync(path, "{\"version\":999}");
            await Assert.ThrowsAsync<InvalidDataException>(() => repository.LoadAsync(path));
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class ShapeComparer : IEqualityComparer<GeometryObject>
    {
        public static ShapeComparer Instance { get; } = new();
        public bool Equals(GeometryObject? x, GeometryObject? y) => x?.Id == y?.Id && x!.Vertices.SequenceEqual(y!.Vertices);
        public int GetHashCode(GeometryObject obj) => obj.Id.GetHashCode();
    }
}
