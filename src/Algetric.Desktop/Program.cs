using Avalonia;
using System.Runtime.InteropServices;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace Algetric.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--diagnostics"))
        {
            Console.WriteLine($"Algetric • {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            var resourcesPresent = File.Exists(Path.Combine(AppContext.BaseDirectory, "Web/vendor/Cesium/Cesium.js"));
            Console.WriteLine($"Web: {resourcesPresent}");
            var webkitPresent = NativeLibrary.TryLoad("libwebkit2gtk-4.1.so.0", out var webkit);
            Console.WriteLine($"WebKitGTK: {webkitPresent}");
            if (webkitPresent) NativeLibrary.Free(webkit);
            if (!resourcesPresent || !webkitPresent) Environment.ExitCode = 1;
            try { using var p = new Infrastructure.ProjCoordinateTransform(new(0, 0)); Console.WriteLine($"PROJ: {p.ToLocal(new(0, 0.1))}"); }
            catch (Exception e) { Console.WriteLine($"PROJ: {e.Message}"); Environment.ExitCode = 1; }
            return;
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
public sealed class App : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
