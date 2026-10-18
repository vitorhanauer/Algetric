using Algetric.Core;
using Algetric.Desktop;
using Avalonia.Controls;

public sealed partial class SmokeApplication
{
    private static async Task VerifyMapAreaSelectionAsync(NativeWebView map, EditorViewModel model)
    {
        var original = model.Editor.Document;
        model.SetTool(EditorTool.Select);
        model.Editor.Add(ShapeKind.Point, [new(30, 30)]);
        model.Editor.Select(null); await Task.Delay(300);
        await map.InvokeScript("""
            (()=>{
              globalThis.areaStartHeight=window.algetric.diagnostics().height;
              const canvas=document.querySelector('canvas'),r=canvas.getBoundingClientRect();
              const options={bubbles:true,button:0,pointerId:2,pointerType:'mouse',isPrimary:true};
              // Synthetic pointers have no browser capture state.
              const capture=canvas.setPointerCapture;canvas.setPointerCapture=()=>{};
              try {
              canvas.dispatchEvent(new PointerEvent('pointerdown',{...options,buttons:1,clientX:r.left+25,clientY:r.top+25}));
              canvas.dispatchEvent(new PointerEvent('pointermove',{...options,buttons:1,clientX:r.right-25,clientY:r.bottom-25}));
              canvas.dispatchEvent(new PointerEvent('pointerup',{...options,buttons:0,clientX:r.right-25,clientY:r.bottom-25}));
              } finally {canvas.setPointerCapture=capture;}
            })()
            """);
        for (var attempt = 0; attempt < 20 && model.Editor.SelectedIds.Count != 2; attempt++) await Task.Delay(100);
        CheckLayer(model.Editor.SelectedIds.Count == 2, "Dragging an area on the globe must select multiple objects in the editor.");
        CheckLayer((await map.InvokeScript("Math.abs(window.algetric.diagnostics().height-globalThis.areaStartHeight)<1"))?.Contains("true") == true,
            "Area selection must not move the globe camera.");
        model.Load(original); model.Editor.Select(original.Objects[0].Id);
        await Task.Delay(300);
        Console.WriteLine("MAP AREA PASS: globe drag rectangle, multiple selection and bridge synchronization.");
    }
}
