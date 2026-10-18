using Algetric.Core;
using Algetric.Desktop;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
        CheckLayer(!card.GetVisualDescendants().OfType<Button>().Any(button => Equals(button.Content, "⋯")), "Layers must not have an ellipsis button.");
        var select = LayerControl<Button>(window, $"SelectLayer_{layerId:N}");
        var position = select.TranslatePoint(new Point(12, 12), window)!.Value;
        var pointer = new Avalonia.Input.Pointer(401, PointerType.Mouse, true);
        select.RaiseEvent(new PointerPressedEventArgs(select, pointer, window, position, 0,
            new PointerPointProperties(Avalonia.Input.RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 2));
        await Task.Delay(100); return LayerDialog("Camada");
    }
    private static async Task VerifyLayerDeletionAsync(MainWindow window, EditorViewModel model)
    {
        await VerifyLayerContextMenuAsync(window, model);
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
    private static async Task<ContextMenu> OpenLayerContextMenuAsync(MainWindow window, Guid id)
    {
        var select = LayerControl<Button>(window, $"SelectLayer_{id:N}");
        select.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = InputElement.ContextRequestedEvent });
        await Task.Delay(50);
        var menu = select.ContextMenu;
        CheckLayer(menu?.IsOpen == true, "Requesting the layer context menu must open it.");
        CheckLayer(menu!.Items.OfType<MenuItem>().Select(item => item.Header).SequenceEqual(new[] { "Editar", "Excluir" }),
            "The layer context menu must offer Edit and Delete.");
        return menu;
    }
    private static void ClickLayerMenuItem(ContextMenu menu, string header)
    {
        menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, header)).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.Close();
    }
    private static async Task VerifyLayerContextMenuAsync(MainWindow window, EditorViewModel model)
    {
        var original = model.Editor.Document; var layer = original.Layers[0];
        var menu = await OpenLayerContextMenuAsync(window, layer.Id);
        ClickLayerMenuItem(menu, "Editar"); await Task.Delay(100);
        var dialog = LayerDialog("Camada");
        CheckLayer(dialog.GetVisualDescendants().OfType<TextBox>().Single(input => input.Name == "LayerName").Text == layer.Name,
            "Context editing must open the chosen layer.");
        dialog.Close();
        menu = await OpenLayerContextMenuAsync(window, layer.Id);
        ClickLayerMenuItem(menu, "Excluir"); await Task.Delay(50);
        var confirmation = LayerDialog("Excluir camada");
        confirmation.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Cancelar")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(50);
        CheckLayer(ReferenceEquals(original, model.Editor.Document), "Cancelling context deletion must preserve the project.");
        menu = await OpenLayerContextMenuAsync(window, layer.Id);
        ClickLayerMenuItem(menu, "Excluir"); await Task.Delay(50);
        confirmation = LayerDialog("Excluir camada");
        confirmation.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Excluir")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(50);
        CheckLayer(!model.Editor.Document.Layers.Any(item => item.Id == layer.Id), "Context deletion must remove the chosen layer.");
        model.Editor.Undo();
        CheckLayer(model.Editor.Document.Layers.Contains(layer) && model.Editor.Document.Objects.SequenceEqual(original.Objects), "Context deletion must undo in one step.");
        menu = await OpenLayerContextMenuAsync(window, original.Layers[1].Id);
        ClickLayerMenuItem(menu, "Excluir"); await Task.Delay(50);
        CheckLayer(model.Editor.Document.Layers.Length == original.Layers.Length - 1, "Empty layers must delete without confirmation.");
        model.Load(original); model.Editor.Select(original.Objects[0].Id);
        Console.WriteLine("LAYER CONTEXT PASS: edit, deletion confirmation, cancel, undo and empty-layer deletion.");
    }
    private static async Task VerifyLayerColorsAsync(MainWindow window, EditorViewModel model)
    {
        var original = model.Editor.Document; var layer = original.Layers[0];
        var dialog = await OpenLayerEditorAsync(window, layer.Id);
        var picker = dialog.GetVisualDescendants().OfType<ColorView>().Single(control => control.Name == "LayerColorPicker");
        CheckLayer(picker.Color == Color.Parse(layer.Color) && !picker.IsAlphaEnabled && !picker.IsHexInputVisible, "The square must load the existing opaque layer color without a hex field.");
        await VerifySquareInteractionAsync(dialog, picker);
        dialog.Close(); CheckLayer(ReferenceEquals(original, model.Editor.Document), "Closing the color dialog must discard edits.");
        dialog = await OpenLayerEditorAsync(window, layer.Id);
        picker = dialog.GetVisualDescendants().OfType<ColorView>().Single(); picker.Color = Colors.CornflowerBlue;
        dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Aplicar")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(50);
        CheckLayer(model.Editor.Document.Layers[0].Color == "#6495ED", "Applying the square must save an RGB layer color.");
        model.Editor.Undo(); CheckLayer(model.Editor.Document.Layers[0] == layer, "Changing a layer color must undo in one operation.");
        LayerControl<Button>(window, "AddLayer").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(100);
        dialog = LayerDialog("Camada");
        CheckLayer(!dialog.GetVisualDescendants().OfType<Button>().Any(button => button.Name == "DeleteLayer"), "New layers must not offer deletion before creation.");
        dialog.GetVisualDescendants().OfType<TextBox>().Single(input => input.Name == "LayerName").Text = "Camada colorida";
        dialog.GetVisualDescendants().OfType<ColorView>().Single().Color = Colors.Orange;
        dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Aplicar")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(50);
        CheckLayer(model.Editor.Document.Layers.Last().Color == "#FFA500", "Creating a layer must use its selected square color.");
        model.Load(original); model.Editor.Select(original.Objects[0].Id);
        Console.WriteLine("LAYER COLOR PASS: square pointer input, brightness, preview, cancel, edit, undo and creation.");
    }
    private static async Task VerifySquareInteractionAsync(Window dialog, ColorView picker)
    {
        var spectrum = dialog.GetVisualDescendants().OfType<ColorSpectrum>().Single(control => control.IsEffectivelyVisible);
        CheckLayer(spectrum.Shape == ColorSpectrumShape.Box && spectrum.Bounds.Width == 200 && spectrum.Bounds.Height == 200, "The color square must measure 200 by 200 pixels.");
        CheckLayer(!picker.GetVisualDescendants().OfType<TabControl>().Any(), "The picker must omit the top tab strip.");
        var preview = picker.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "LayerColorPreview");
        CheckLayer(preview.Bounds.Width == 48 && preview.Bounds.Height == 48 &&
            preview.TranslatePoint(default, picker)!.Value.X > spectrum.TranslatePoint(default, picker)!.Value.X + spectrum.Bounds.Width,
            "The color preview must be a square to the right of the spectrum.");
        var initial = picker.Color; var pointer = new Avalonia.Input.Pointer(301, PointerType.Mouse, true);
        var position = spectrum.TranslatePoint(new Point(spectrum.Bounds.Width * 0.85, spectrum.Bounds.Height * 0.5), dialog)!.Value;
        var target = (Control)dialog.InputHitTest(position)!;
        target.RaiseEvent(new PointerPressedEventArgs(target, pointer, dialog, position, 0,
            new PointerPointProperties(Avalonia.Input.RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1));
        target.RaiseEvent(new PointerReleasedEventArgs(target, pointer, dialog, position, 1, new PointerPointProperties(), KeyModifiers.None, MouseButton.Left));
        CheckLayer(picker.Color != initial, "Clicking the color square must select a different color.");
        var brightness = dialog.GetVisualDescendants().OfType<ColorSlider>().Single(control => control.IsEffectivelyVisible);
        var beforeBrightness = picker.Color; brightness.Value = brightness.Maximum * 0.85;
        CheckLayer(picker.Color != beforeBrightness, "The brightness slider must update the selected color.");
        CheckLayer(preview.Background is SolidColorBrush brush && brush.Color == picker.Color, "The preview must follow spectrum and brightness changes.");
        await Task.Delay(100);
        Directory.CreateDirectory("artifacts/smoke");
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)dialog.Bounds.Width, (int)dialog.Bounds.Height));
        bitmap.Render(dialog); bitmap.Save("artifacts/smoke/layer-color-square.png", PngBitmapEncoderOptions.Default);
    }
}
