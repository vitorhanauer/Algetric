using System.Text.Json;
using System.Text.Json.Nodes;
using Algetric.Core;
using Algetric.Desktop;
using Algetric.Infrastructure;
using Xunit;

namespace Algetric.Tests;

public class ObjectVisibilityTests
{
    private static GeometryObject AddPolygon(ProjectEditor editor) =>
        editor.Add(ShapeKind.Polygon, [new(0, 0), new(100, 0), new(100, 100), new(0, 100)]);

    [Fact]
    public void HidingOnePolygonPreservesItsGeometryAndOtherObjectsAndSupportsHistory()
    {
        var editor = TestProjects.WithLayers(); var polygon = AddPolygon(editor);
        var other = AddPolygon(editor); editor.MarkSaved();
        var before = editor.Document;
        editor.SetObjectVisibility(polygon.Id, false);
        Assert.False(editor.Document.Objects[0].Visible);
        Assert.Equal(polygon with { Visible = false }, editor.Document.Objects[0]);
        Assert.Equal(other, editor.Document.Objects[1]); Assert.True(editor.Document.Layers[0].Visible);
        Assert.True(editor.IsDirty);
        editor.SetObjectVisibility(polygon.Id, false);
        editor.Undo(); Assert.Equal(before.Objects, editor.Document.Objects); Assert.False(editor.IsDirty);
        editor.Redo(); Assert.False(editor.Document.Objects[0].Visible);
        editor.SetObjectVisibility(polygon.Id, true); Assert.True(editor.Document.Objects[0].Visible);
    }

    [Fact]
    public void HiddenPolygonsAreExcludedFromAreaSelectionAndMapState()
    {
        var model = new EditorViewModel(); model.Load(TestProjects.WithLayers().Document);
        var hidden = AddPolygon(model.Editor); var visible = AddPolygon(model.Editor);
        model.Editor.SetObjectVisibility(hidden.Id, false);
        model.Editor.SelectArea(new(-1, -1), new(101, 101));
        Assert.Equal(visible.Id, model.Editor.SelectedId);
        using var state = JsonDocument.Parse(model.MapState());
        var objects = state.RootElement.GetProperty("objects");
        Assert.Equal(1, objects.GetArrayLength());
        Assert.Equal(visible.Id, objects[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public void ShowingALayerDoesNotOverrideThePolygonsOwnVisibility()
    {
        var model = new EditorViewModel(); model.Load(TestProjects.WithLayers().Document);
        var polygon = AddPolygon(model.Editor); var other = AddPolygon(model.Editor);
        model.Editor.SetObjectVisibility(polygon.Id, false);
        var layer = model.Editor.Document.Layers[0];
        model.Editor.UpdateLayer(layer with { Visible = false });
        using (var state = JsonDocument.Parse(model.MapState())) Assert.Empty(state.RootElement.GetProperty("objects").EnumerateArray());
        model.Editor.UpdateLayer(layer with { Visible = true });
        Assert.False(model.Editor.Document.Objects[0].Visible);
        using var shown = JsonDocument.Parse(model.MapState());
        Assert.Equal(other.Id, Assert.Single(shown.RootElement.GetProperty("objects").EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData(ShapeKind.Point)]
    [InlineData(ShapeKind.Segment)]
    [InlineData(ShapeKind.Polyline)]
    [InlineData(ShapeKind.Polygon)]
    [InlineData(ShapeKind.CoveragePath)]
    public async Task VisibilityIsSavedAndLegacyObjectsDefaultToVisible(ShapeKind kind)
    {
        var editor = TestProjects.WithLayers();
        LocalPoint[] vertices = [new(0, 0), new(100, 0), new(100, 100), new(0, 100)];
        var shape = editor.Add(kind, kind == ShapeKind.Point ? vertices.Take(1) : kind == ShapeKind.Segment ? vertices.Take(2) : vertices);
        editor.SetObjectVisibility(shape.Id, false);
        var path = Path.GetTempFileName(); var repository = new JsonProjectRepository();
        try
        {
            await repository.SaveAsync(path, editor.Document);
            Assert.False((await repository.LoadAsync(path)).Objects[0].Visible);
            var legacy = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            legacy["objects"]![0]!.AsObject().Remove("visible");
            await File.WriteAllTextAsync(path, legacy.ToJsonString());
            Assert.True((await repository.LoadAsync(path)).Objects[0].Visible);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MissingObjectIsRejectedWithoutChangingHistory()
    {
        var editor = TestProjects.WithLayers(); var before = editor.Document;
        Assert.Throws<ArgumentException>(() => editor.SetObjectVisibility(Guid.NewGuid(), false));
        Assert.Same(before, editor.Document); Assert.False(editor.CanUndo);
    }
}
