using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Algetric.Infrastructure;

public static class WebRuntimeDiagnostics
{
    public static string RuntimeName => OperatingSystem.IsWindows() ? "WebView2" : "WebKitGTK";
    public static bool IsAvailable()
    {
        if (OperatingSystem.IsWindows())
        {
            const string client = @"Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
            return ValidVersion(Registry.GetValue(@"HKEY_CURRENT_USER\Software\" + client, "pv", null))
                || ValidVersion(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\" + client, "pv", null));
        }
        if (!NativeLibrary.TryLoad("libwebkit2gtk-4.1.so.0", out var library)) return false;
        NativeLibrary.Free(library); return true;
    }
    private static bool ValidVersion(object? value) => Version.TryParse(value as string, out var version) && version > new Version(0, 0, 0, 0);
}
