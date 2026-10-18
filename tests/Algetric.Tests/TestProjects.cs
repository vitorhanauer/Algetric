using Algetric.Core;

namespace Algetric.Tests;

internal static class TestProjects
{
    public static ProjectEditor WithLayers()
    {
        var editor = new ProjectEditor();
        editor.Load(ProjectDocument.Empty() with
        {
            Layers = [new(Guid.NewGuid(), "Layer 1", "#009CA6"), new(Guid.NewGuid(), "Layer 2", "#FF791F")]
        });
        return editor;
    }
}
