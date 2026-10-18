using Algetric.Core;
using Algetric.Desktop;
using Avalonia.Controls;
using Avalonia.VisualTree;

public sealed partial class SmokeApplication
{
    private static async Task VerifyMapCoverageMenuAsync(MainWindow window, NativeWebView map, EditorViewModel model)
    {
        var original = model.Editor.Document;
        var polygon = model.Editor.Add(ShapeKind.Polygon, [new(-2000, -2000), new(2000, -2000), new(2000, 2000), new(-2000, 2000)]);
        await Task.Delay(200); await map.InvokeScript("document.querySelector('#home').click()");
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if ((await map.InvokeScript("window.algetric.diagnostics().mode==='2D'&&!window.algetric.diagnostics().morphing&&!window.algetric.diagnostics().pending"))?.Contains("true") == true) break;
            await Task.Delay(150);
        }
        await map.InvokeScript("""
            (()=>{const canvas=document.querySelector('canvas'),r=canvas.getBoundingClientRect();
            canvas.dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,button:2,clientX:r.left+r.width/2+10,clientY:r.top+r.height/2-10}));})()
            """);
        CheckLayer((await map.InvokeScript("Boolean(document.querySelector('#polygon-menu button'))"))?.Contains("true") == true,
            "Right-clicking a polygon on the map must show Generate point path.");
        await map.InvokeScript("document.querySelector('#polygon-menu button').click()"); await Task.Delay(200);
        var dialog = LayerDialog("Gerar caminho de pontos");
        CheckLayer(Equals(dialog.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "CoveragePolygon").SelectedValue, polygon.Id),
            "The map context polygon must be preselected in the native modal.");
        dialog.Close(); model.Load(original); model.Editor.Select(original.Objects[0].Id); await Task.Delay(300);
        Console.WriteLine("MAP COVERAGE PASS: real Cesium polygon context menu and preselected native modal.");
    }
}
