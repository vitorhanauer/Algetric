using Algetric.Core;
using Xunit;

namespace Algetric.Tests;

public class AreaSelectionTests
{
    [Fact]
    public void AreaSelectsFullyContainedVisibleObjectsInEitherDragDirection()
    {
        var editor = TestProjects.WithLayers();
        var point = editor.Add(ShapeKind.Point, [new(0, 0)]);
        var line = editor.Add(ShapeKind.Polyline, [new(2, 2), new(10, 10)]);
        var polygon = editor.Add(ShapeKind.Polygon, [new(1, 1), new(5, 1), new(5, 5)]);
        editor.Add(ShapeKind.Segment, [new(5, 5), new(20, 20)]);
        editor.AddLayer("Hidden", "#FFFFFF");
        editor.Add(ShapeKind.Point, [new(3, 3)]);
        editor.UpdateLayer(editor.Document.Layers.Last() with { Visible = false });
        var original = editor.Document; editor.MarkSaved();
        editor.SelectArea(new(10, 10), new(0, 0));
        Assert.True(editor.SelectedIds.SetEquals([point.Id, line.Id, polygon.Id]));
        Assert.Null(editor.SelectedId);
        Assert.Same(original, editor.Document);
        Assert.False(editor.IsDirty);
        editor.Undo(); Assert.True(editor.Document.Layers.Last().Visible);
    }
    [Fact]
    public void EmptyAreaClearsSelectionAndSingleClickReplacesMultipleSelection()
    {
        var editor = TestProjects.WithLayers();
        var first = editor.Add(ShapeKind.Point, [new(1, 1)]);
        editor.Add(ShapeKind.Point, [new(2, 2)]);
        editor.SelectArea(new(0, 0), new(10, 10));
        editor.Select(first.Id);
        Assert.Equal(first.Id, editor.SelectedId);
        Assert.Single(editor.SelectedIds);
        editor.SelectArea(new(20, 20), new(30, 30));
        Assert.Empty(editor.SelectedIds);
        Assert.Null(editor.SelectedId);
    }
    [Fact]
    public void DeletingMultipleSelectedObjectsIsOneUndoOperation()
    {
        var editor = TestProjects.WithLayers();
        editor.Add(ShapeKind.Point, [new(1, 1)]);
        editor.Add(ShapeKind.Point, [new(2, 2)]);
        var outside = editor.Add(ShapeKind.Point, [new(20, 20)]);
        var original = editor.Document;
        editor.SelectArea(new(0, 0), new(10, 10)); editor.DeleteSelected();
        Assert.Equal(outside, Assert.Single(editor.Document.Objects));
        Assert.Empty(editor.SelectedIds);
        editor.Undo(); Assert.Equal(original.Objects, editor.Document.Objects);
        editor.Redo(); Assert.Single(editor.Document.Objects);
    }
    [Fact]
    public void DeletionUndoAndLoadingPruneStaleSelectionIds()
    {
        var editor = TestProjects.WithLayers();
        var first = editor.Add(ShapeKind.Point, [new(1, 1)]);
        var second = editor.Add(ShapeKind.Point, [new(2, 2)]);
        editor.SelectMany([first.Id, second.Id, Guid.NewGuid()]);
        Assert.Equal(2, editor.SelectedIds.Count);
        editor.Undo(); Assert.Equal(first.Id, editor.SelectedId);
        editor.DeleteLayer(first.LayerId); Assert.Empty(editor.SelectedIds);
        editor.Undo(); editor.Select(first.Id);
        editor.Load(editor.Document); Assert.Empty(editor.SelectedIds);
    }
}
