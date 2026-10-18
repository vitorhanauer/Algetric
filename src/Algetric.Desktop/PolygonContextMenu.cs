using Avalonia.Controls;

namespace Algetric.Desktop;

internal static class PolygonContextMenu
{
    public static ContextMenu Create(EditorViewModel model, Guid polygonId)
    {
        var generate = new MenuItem { Header = "Gerar caminho de pontos…" };
        generate.Click += (_, _) => model.Try(() => model.RequestCoveragePath(polygonId));
        return new ContextMenu { Name = "PolygonContextMenu", Items = { generate } };
    }
}
