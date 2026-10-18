using System.Reflection;
using System.Runtime.InteropServices;

namespace Algetric.Infrastructure;

internal static class ProjNativeRuntime
{
    internal const string LibraryName = "algetric-proj";
    internal static void Register() => NativeLibrary.SetDllImportResolver(typeof(ProjNativeRuntime).Assembly, Resolve);
    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != LibraryName) return IntPtr.Zero;
        if (!OperatingSystem.IsWindows()) return NativeLibrary.Load("libproj.so.25", assembly, searchPath);
        var candidates = Directory.GetFiles(AppContext.BaseDirectory, "proj*.dll");
        if (candidates.Length != 1) throw new DllNotFoundException("Expected one bundled PROJ DLL beside the application.");
        return NativeLibrary.Load(candidates[0], assembly,
            DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.SafeDirectories);
    }
    internal static void ConfigureDatabase(IntPtr context)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "proj-data", "proj.db");
        if (!File.Exists(path))
        {
            if (OperatingSystem.IsWindows()) throw new FileNotFoundException("The Windows package is missing proj-data/proj.db.", path);
            return;
        }
        if (proj_context_set_database_path(context, path, IntPtr.Zero, IntPtr.Zero) == 0)
            throw new InvalidOperationException("Unable to load the bundled PROJ database.");
    }
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int proj_context_set_database_path(IntPtr context,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string databasePath, IntPtr auxiliaryPaths, IntPtr options);
}
