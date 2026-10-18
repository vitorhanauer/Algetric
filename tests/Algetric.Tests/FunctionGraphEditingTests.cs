using System.Text.Json;
using Algetric.Core;
using Algetric.Desktop;
using Algetric.Infrastructure;
using Xunit;

namespace Algetric.Tests;

public class FunctionGraphEditingTests
{
    [Fact]
    public void FunctionsBelongToTheActiveLayerAndSupportEditingVisibilityAndHistory()
    {
        var editor = TestProjects.WithLayers(); var original = editor.Document;
        var graph = editor.AddFunctionGraph("sin(x) + x^2", -5, 5);
        Assert.Equal(ShapeKind.FunctionGraph, graph.Kind); Assert.Equal(editor.ActiveLayerId, graph.LayerId);
        Assert.Equal(graph.Id, editor.SelectedId); Assert.NotNull(graph.Function); Assert.Empty(graph.Vertices);
        Assert.True(GeometryRules.Measure(graph).Length > 0);
        editor.Undo(); Assert.Equal(original.Objects, editor.Document.Objects);
        editor.Redo(); editor.SetObjectVisibility(graph.Id, false);
        editor.UpdateFunctionGraph(graph.Id, "Quadrática", "x^2", -10, 10);
        var updated = Assert.Single(editor.Document.Objects);
        Assert.Equal("x^2", updated.Function!.Expression); Assert.False(updated.Visible);
        editor.Undo(); Assert.Equal("sin(x) + x^2", editor.Document.Objects[0].Function!.Expression);
    }
    [Fact]
    public void InvalidExpressionsAndVertexEditsAreAtomic()
    {
        var editor = TestProjects.WithLayers(); var graph = editor.AddFunctionGraph("x", -5, 5);
        var before = editor.Document;
        Assert.Throws<ArgumentException>(() => editor.UpdateFunctionGraph(graph.Id, graph.Name, "sin(", -5, 5));
        Assert.Throws<ArgumentException>(() => editor.ReplaceVertices(graph.Id, [new(0, 0), new(1, 1)]));
        Assert.Same(before, editor.Document);
        Assert.Throws<InvalidOperationException>(() => new ProjectEditor().AddFunctionGraph("1", -5, 5));
    }
    [Fact]
    public async Task ExpressionsAndDomainSurviveSavingAndMapSegmentsRespectDiscontinuities()
    {
        var model = new EditorViewModel(); model.Load(TestProjects.WithLayers().Document);
        var graph = model.Editor.AddFunctionGraph("1/x", -5, 5);
        var path = Path.GetTempFileName(); var repository = new JsonProjectRepository();
        try
        {
            await repository.SaveAsync(path, model.Editor.Document);
            var loaded = (await repository.LoadAsync(path)).Objects.Single();
            Assert.Equal("1/x", loaded.Function!.Expression); Assert.Equal(-5, loaded.Function.MinimumX);
            Assert.Equal(5, loaded.Function.MaximumX); Assert.True(loaded.Function.GetSegments().Length >= 2);
        }
        finally { File.Delete(path); }
        using var state = JsonDocument.Parse(model.MapState());
        Assert.True(state.RootElement.GetProperty("objects")[0].GetProperty("paths").GetArrayLength() >= 2);
        model.Editor.SelectArea(new(100, 100), new(200, 200)); Assert.Empty(model.Editor.SelectedIds);
    }
}
