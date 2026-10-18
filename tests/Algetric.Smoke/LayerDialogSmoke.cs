using Algetric.Core;
using Algetric.Desktop;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

public sealed partial class SmokeApplication
{
    private static Window LayerDialog(string title) => ((IClassicDesktopStyleApplicationLifetime)Current!.ApplicationLifetime!).Windows.Single(window => window.Title == title);
    private static void ClickDialogButton(Window window, string name)
        => window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static async Task<Window> OpenLayerEditorAsync(MainWindow window, Guid layerId)
    {
        var card = LayerControl<Border>(window, $"Layer_{layerId:N}");
        card.GetVisualDescendants().OfType<Button>().Single(button => button.Content is string text && text == "⋯").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(100); return LayerDialog("Camada");
    }
    private static async Task VerifyLayerDeletionAsync(MainWindow window, EditorViewModel model)
    {
        var original = model.Editor.Document; var layer = original.Layers[0];
        var dialog = await OpenLayerEditorAsync(window, layer.Id);
        ClickDialogButton(dialog, "DeleteLayer"); await Task.Delay(50);
        var confirmation = LayerDialog("Excluir camada");
        confirmation.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Cancelar")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(50); CheckLayer(ReferenceEquals(original, model.Editor.Document), "Cancelling layer deletion must preserve the project.");
        ClickDialogButton(dialog, "DeleteLayer"); await Task.Delay(50);
        confirmation = LayerDialog("Excluir camada");
        confirmation.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Excluir")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(50);
        CheckLayer(!model.Editor.Document.Layers.Any(item => item.Id == layer.Id) && !model.Editor.Document.Objects.Any(item => item.LayerId == layer.Id), "Layer deletion must remove its rows and objects.");
        model.Editor.Undo();
        CheckLayer(model.Editor.Document.Layers.Contains(layer) && model.Editor.Document.Objects.Length == original.Objects.Length, "Layer deletion must undo in one step.");
        model.Load(original); model.Editor.Select(original.Objects[0].Id);
        Console.WriteLine("LAYER DELETE PASS: populated-layer confirmation, cancel, deletion and undo.");
    }
}
