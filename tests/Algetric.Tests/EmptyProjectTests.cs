using Algetric.Core;
using Algetric.Infrastructure;
using Xunit;

namespace Algetric.Tests;

public class EmptyProjectTests
{
    [Fact]
    public void NewProjectsStartWithoutLayersOrObjects()
    {
        var editor = new ProjectEditor();
        Assert.Empty(editor.Document.Layers); Assert.Empty(editor.Document.Objects);
        Assert.Null(editor.ActiveLayerId); Assert.False(editor.IsDirty); Assert.False(editor.CanUndo);
        GeometryRules.ValidateDocument(editor.Document);
    }
    [Fact]
    public void CreatingAnObjectWithoutALayerLeavesTheProjectUntouched()
    {
        var editor = new ProjectEditor(); var original = editor.Document;
        var error = Assert.Throws<InvalidOperationException>(() => editor.Add(ShapeKind.Point, [new(0, 0)]));
        Assert.Contains("camada", error.Message);
        Assert.Same(original, editor.Document); Assert.Null(editor.SelectedId); Assert.False(editor.CanUndo);
    }
    [Fact]
    public void UndoingTheFirstLayerReturnsToAnEmptyProjectAndRedoRestoresIt()
    {
        var editor = new ProjectEditor(); editor.AddLayer("Minha camada", "#009CA6");
        var layer = Assert.Single(editor.Document.Layers);
        editor.Undo(); Assert.Empty(editor.Document.Layers); Assert.Null(editor.ActiveLayerId);
        editor.Redo(); Assert.Equal(layer.Id, editor.ActiveLayerId);
        Assert.Equal(layer.Id, editor.Add(ShapeKind.Point, [new(0, 0)]).LayerId);
        editor.Load(ProjectDocument.Empty());
        Assert.Empty(editor.Document.Layers); Assert.Null(editor.ActiveLayerId); Assert.False(editor.CanUndo);
    }
    [Fact]
    public async Task EmptyProjectsCanBeSavedAndReopened()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".algetric");
        try
        {
            var repository = new JsonProjectRepository();
            await repository.SaveAsync(path, ProjectDocument.Empty());
            var editor = new ProjectEditor(); editor.Load(await repository.LoadAsync(path));
            Assert.Empty(editor.Document.Layers); Assert.Empty(editor.Document.Objects); Assert.Null(editor.ActiveLayerId);
        }
        finally { File.Delete(path); }
    }
}
