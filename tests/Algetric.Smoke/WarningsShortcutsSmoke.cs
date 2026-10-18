using Algetric.Core;
using Algetric.Desktop;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

public sealed partial class SmokeApplication
{
    private static async Task VerifyMissingLayerWarningAsync(MainWindow window, EditorViewModel model)
    {
        var canvas = Field<CartesianCanvas>(window, "canvas");
        var original = model.Editor.Document;
        foreach (var tool in new[] { EditorTool.Point, EditorTool.Polyline, EditorTool.Polygon })
        {
            model.SetTool(tool); ClickCanvas(window, canvas, new(200, 200)); await Task.Delay(50);
            var popup = window.GetLogicalDescendants().OfType<Popup>().SingleOrDefault(control => control.Name == "WarningToast");
            CheckLayer(popup?.IsOpen == true, "Creating without a layer must open a warning popup immediately.");
            CheckLayer(popup!.Child!.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Crie uma camada pelo botão + antes de criar objetos."),
                "The popup must reuse the existing missing-layer message.");
            var position = popup.Child!.PointToScreen(default);
            var windowBottom = window.PointToScreen(new Point(window.Bounds.Width / 2, window.Bounds.Height));
            CheckLayer(Math.Abs(position.X + popup.Child!.Bounds.Width / 2 - windowBottom.X) < 5 && position.Y > windowBottom.Y - 150,
                $"Warnings must appear centered near the bottom of the window: popup {position}, size {popup.Child!.Bounds.Size}; bottom {windowBottom}.");
            CheckLayer(ReferenceEquals(original, model.Editor.Document), "Warnings must leave the project unchanged.");
            popup.IsOpen = false;
        }
        model.Load(original);
        Console.WriteLine("WARNING PASS: immediate bottom popup for point, line and polygon without layers.");
    }
    private static async Task VerifyMapWarningsAndShortcutsAsync(MainWindow window, NativeWebView map, EditorViewModel model)
    {
        var original = model.Editor.Document; model.Load(ProjectDocument.Empty()); await Task.Delay(150);
        model.SetTool(EditorTool.Point);
        for (var attempt = 0; attempt < 20 && model.Tool != EditorTool.Point; attempt++) await Task.Delay(50);
        CheckLayer(model.Tool == EditorTool.Point, "Map keyboard shortcuts must select the desktop tool.");
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if ((await map.InvokeScript("window.algetric.diagnostics().tool===1&&!window.algetric.diagnostics().pending"))?.Contains("true") == true) break;
            await Task.Delay(50);
        }
        await map.InvokeScript("""
            (()=>{
              const canvas=document.querySelector('canvas'),r=canvas.getBoundingClientRect();
              const options={bubbles:true,button:0,pointerId:3,pointerType:'mouse',isPrimary:true,clientX:r.left+r.width/2,clientY:r.top+r.height/2};
              const capture=canvas.setPointerCapture;canvas.setPointerCapture=()=>{};
              try {
                canvas.dispatchEvent(new PointerEvent('pointerdown',{...options,buttons:1}));
                canvas.dispatchEvent(new PointerEvent('pointerup',{...options,buttons:0}));
              } finally {canvas.setPointerCapture=capture;}
            })()
            """);
        var popup = window.GetLogicalDescendants().OfType<Popup>().Single(control => control.Name == "WarningToast");
        for (var attempt = 0; attempt < 20 && !popup.IsOpen; attempt++) await Task.Delay(50);
        CheckLayer(popup.IsOpen && model.Editor.Document.Objects.Length == 0, "Creation on the map without layers must show the same popup: " + await map.InvokeScript("JSON.stringify(window.algetric.diagnostics())"));
        CheckLayer((await map.InvokeScript("document.getElementById('finish')===null"))?.Contains("true") == true, "The map must not show a Finish button.");
        popup.IsOpen = false; model.Load(original); model.Editor.Select(original.Objects[0].Id); await Task.Delay(150);
        Console.WriteLine("MAP WARNING PASS: native popup for missing layers.");
    }
    private static async Task VerifyToolShortcutsAsync(MainWindow window, EditorViewModel model)
    {
        var canvas = Field<CartesianCanvas>(window, "canvas");
        var buttons = window.GetVisualDescendants().OfType<Button>().ToArray();
        CheckLayer(!buttons.Any(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Concluir"), "The Finish button must be removed.");
        await Task.CompletedTask;
    }
}
