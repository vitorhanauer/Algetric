using Algetric.Desktop;
using Avalonia.Controls;
using Avalonia.Interactivity;

public sealed partial class SmokeApplication
{
    private static async Task VerifyMapLanguageAsync(MainWindow window, NativeWebView map)
    {
        LanguageChoice(window, "EnglishLanguage").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        try
        {
            var translated = false;
            for (var attempt = 0; attempt < 30 && !translated; attempt++)
            {
                await Task.Delay(100);
                translated = (await map.InvokeScript("document.documentElement.lang==='en' && document.querySelector('#home').textContent==='Origin'"))?.Contains("true") == true;
            }
            CheckLayer(translated, "Map controls must update to English without restarting the WebView.");
        }
        finally { LanguageChoice(window, "PortugueseLanguage").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Task.Delay(200); }
        CheckLayer((await map.InvokeScript("document.documentElement.lang==='pt-BR' && document.querySelector('#home').textContent==='Origem'"))?.Contains("true") == true,
            "Map controls must return to Portuguese.");
        Console.WriteLine("MAP LANGUAGE PASS: live English and Portuguese controls.");
    }
}
