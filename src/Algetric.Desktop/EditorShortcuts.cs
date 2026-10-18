using Avalonia.Input;

namespace Algetric.Desktop;

internal sealed record ToolShortcut(EditorTool Tool, Key Key, string Label, string Icon);

internal static class EditorShortcuts
{
    public static IReadOnlyList<ToolShortcut> Tools { get; } = new ToolShortcut[]
    {
        new(EditorTool.Select, Key.V, "Selecionar", "mouse"),
        new(EditorTool.Move, Key.M, "Mover", "✥"),
        new(EditorTool.Point, Key.P, "Ponto", "•"),
        new(EditorTool.Polyline, Key.L, "Linha", "╱"),
        new(EditorTool.Polygon, Key.G, "Polígono", "⬡")
    };
}
