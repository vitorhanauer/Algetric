using Algetric.Core;
using Algetric.Desktop;
using System.Text.Json;
using Xunit;

namespace Algetric.Tests;

public class CreationWarningTests
{
    [Fact]
    public void MissingLayerWarningRepeatsWithoutChangingTheDocument()
    {
        var model = new EditorViewModel(); var original = model.Editor.Document;
        var warnings = new List<string>(); model.WarningRequested += warnings.Add;
        Assert.False(model.EnsureActiveLayer()); Assert.False(model.EnsureActiveLayer());
        Assert.Equal(new[] { ProjectEditor.MissingLayerMessage, ProjectEditor.MissingLayerMessage }, warnings);
        Assert.Same(original, model.Editor.Document); Assert.False(model.Editor.CanUndo);
        Assert.DoesNotContain(ProjectEditor.MissingLayerMessage, model.Status);
        model.Editor.AddLayer("Camada", "#009CA6");
        Assert.True(model.EnsureActiveLayer()); Assert.Equal(2, warnings.Count);
    }
    [Fact]
    public void FailedCreationPublishesTheExistingMessageAsAWarning()
    {
        var model = new EditorViewModel(); string? warning = null;
        model.WarningRequested += value => warning = value;
        model.Try(() => model.Add(ShapeKind.Point, [new(0, 0)]));
        Assert.Equal(ProjectEditor.MissingLayerMessage, warning);
        Assert.Empty(model.Editor.Document.Objects);
    }
    [Fact]
    public void MapLayerRequestsUseTheSameWarningAndExposeCreationAvailability()
    {
        var model = new EditorViewModel(); string? warning = null;
        model.WarningRequested += value => warning = value;
        using var state = JsonDocument.Parse(model.MapState());
        Assert.False(state.RootElement.GetProperty("canCreateObject").GetBoolean());
        model.HandleMessage(JsonSerializer.Serialize(new { type = "requireLayer", sequence = 1, revision = model.Revision,
            session = state.RootElement.GetProperty("session").GetString() }));
        Assert.Equal(ProjectEditor.MissingLayerMessage, warning);
    }
}
