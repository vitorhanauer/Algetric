using Algetric.Core;

namespace Algetric.Desktop;

public sealed partial class MainWindow
{
    private async Task EditFunctionAsync(Guid? id)
    {
        var shape = await Dialogs.EditFunction(this, model, id);
        if (shape?.Function is not { } function) return;
        canvas.FocusFunction(function); model.SetTool(EditorTool.Select);
    }
}
