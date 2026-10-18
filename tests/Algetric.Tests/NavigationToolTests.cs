using Algetric.Core;
using Algetric.Desktop;
using Xunit;

namespace Algetric.Tests;

public class NavigationToolTests
{
    [Theory]
    [InlineData(EditorTool.Select)]
    [InlineData(EditorTool.Point)]
    [InlineData(EditorTool.Segment)]
    [InlineData(EditorTool.Polyline)]
    [InlineData(EditorTool.Polygon)]
    [InlineData(EditorTool.Move)]
    public void HoldingSpaceRestoresTheSelectedToolEvenAfterKeyRepeat(EditorTool selected)
    {
        var model = new EditorViewModel(); model.SetTool(selected);
        var document = model.Editor.Document;
        model.BeginTemporaryMove(); model.BeginTemporaryMove();
        Assert.Equal(EditorTool.Move, model.Tool);
        Assert.Equal(selected, model.SelectedTool);
        model.EndTemporaryMove(); model.EndTemporaryMove();
        Assert.Equal(selected, model.Tool);
        Assert.Same(document, model.Editor.Document);
        Assert.False(model.Editor.CanUndo);
    }
    [Fact]
    public void ExplicitToolSelectionAndLoadingCancelTemporaryMove()
    {
        var model = new EditorViewModel(); model.SetTool(EditorTool.Polygon);
        model.BeginTemporaryMove(); model.SetTool(EditorTool.Point); model.EndTemporaryMove();
        Assert.Equal(EditorTool.Point, model.Tool);
        model.BeginTemporaryMove(); model.Load(ProjectDocument.Empty()); model.EndTemporaryMove();
        Assert.Equal(EditorTool.Select, model.Tool);
    }
}
