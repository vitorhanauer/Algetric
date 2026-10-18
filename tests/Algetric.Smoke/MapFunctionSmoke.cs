using Algetric.Desktop;
using Avalonia.Controls;

public sealed partial class SmokeApplication
{
    private static async Task VerifyMapFunctionsAsync(NativeWebView map, EditorViewModel model)
    {
        var original = model.Editor.Document;
        var graph = model.Editor.AddFunctionGraph("1/(x-0.12345)", -2000, 2000);
        var expected = graph.Function!.GetSegments().Length;
        await WaitForFunctionSegmentsAsync(map, expected);
        model.Editor.SetObjectVisibility(graph.Id, false);
        await WaitForFunctionSegmentsAsync(map, 0);
        await map.InvokeScript("window.dispatchEvent(new KeyboardEvent('keydown',{key:'f',bubbles:true}))"); await Task.Delay(200);
        LayerDialog("Desenhar função").Close();
        model.Load(original); model.Editor.Select(original.Objects[0].Id); await Task.Delay(200);
        Console.WriteLine("MAP FUNCTION PASS: separate Cesium curves, visibility and function shortcut.");
    }
    private static async Task WaitForFunctionSegmentsAsync(NativeWebView map, int expected)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(100);
            if ((await map.InvokeScript($"window.algetric.diagnostics().functionSegments==={expected}"))?.Contains("true") == true) return;
        }
        throw new InvalidOperationException($"Expected {expected} separate function segments in the real Cesium viewer.");
    }
}
