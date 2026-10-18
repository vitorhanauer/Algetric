using System.Globalization;
using System.Runtime.InteropServices;
using Algetric.Core;

namespace Algetric.Infrastructure;

public sealed class ProjCoordinateTransform : ICoordinateTransform
{
    private readonly object gate = new();
    private IntPtr context;
    private IntPtr projection;
    static ProjCoordinateTransform() => ProjNativeRuntime.Register();
    public ProjCoordinateTransform(GeographicPoint origin)
    {
        GeometryRules.ValidateOrigin(origin);
        context = Native.proj_context_create();
        try
        {
            ProjNativeRuntime.ConfigureDatabase(context);
            var target = FormattableString.Invariant($"+proj=aeqd +lat_0={origin.Latitude} +lon_0={origin.Longitude} +datum=WGS84 +units=m +type=crs");
            var raw = Native.proj_create_crs_to_crs(context, "EPSG:4326", target, IntPtr.Zero);
            if (raw != IntPtr.Zero) { projection = Native.proj_normalize_for_visualization(context, raw); Native.proj_destroy(raw); }
            if (projection == IntPtr.Zero) throw new InvalidOperationException("Não foi possível inicializar PROJ.");
        }
        catch { Dispose(); throw; }
    }
    public LocalPoint ToLocal(GeographicPoint point)
    {
        GeometryRules.ValidateOrigin(point);
        var value = Transform(1, point.Longitude, point.Latitude);
        var result = new LocalPoint(value.X, value.Y);
        if (Math.Sqrt(result.X * result.X + result.Y * result.Y) > 50_000)
            throw new ArgumentException("Edição limitada a 50 km da origem.");
        return result;
    }
    public GeographicPoint ToGeographic(LocalPoint point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || Math.Sqrt(point.X * point.X + point.Y * point.Y) > 50_000)
            throw new ArgumentException("Coordenada local inválida.");
        var value = Transform(-1, point.X, point.Y);
        var result = new GeographicPoint(value.Y, value.X); GeometryRules.ValidateOrigin(result); return result;
    }
    private Native.Coordinate Transform(int direction, double x, double y)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(projection == IntPtr.Zero, this);
            var result = Native.proj_trans(projection, direction, new() { X = x, Y = y });
            if (!double.IsFinite(result.X) || !double.IsFinite(result.Y)) throw new ArgumentException("Falha na transformação de coordenadas.");
            return result;
        }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (projection != IntPtr.Zero) Native.proj_destroy(projection);
            if (context != IntPtr.Zero) Native.proj_context_destroy(context);
            projection = context = IntPtr.Zero;
        }
    }
    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Coordinate { public double X, Y, Z, T; }
        [DllImport(ProjNativeRuntime.LibraryName, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr proj_context_create();
        [DllImport(ProjNativeRuntime.LibraryName, CallingConvention = CallingConvention.Cdecl)] internal static extern void proj_context_destroy(IntPtr context);
        [DllImport(ProjNativeRuntime.LibraryName, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr proj_create_crs_to_crs(IntPtr context, [MarshalAs(UnmanagedType.LPUTF8Str)] string source, [MarshalAs(UnmanagedType.LPUTF8Str)] string target, IntPtr area);
        [DllImport(ProjNativeRuntime.LibraryName, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr proj_normalize_for_visualization(IntPtr context, IntPtr projection);
        [DllImport(ProjNativeRuntime.LibraryName, CallingConvention = CallingConvention.Cdecl)] internal static extern void proj_destroy(IntPtr projection);
        [DllImport(ProjNativeRuntime.LibraryName, CallingConvention = CallingConvention.Cdecl)] internal static extern Coordinate proj_trans(IntPtr projection, int direction, Coordinate coordinate);
    }
}
