using System.Runtime.InteropServices;

namespace EchoMix.AudioHost;

/// Wine's WASAPI session manager cannot unregister COM notifications. NAudio's
/// <c>AudioSessionManager</c> finalizer then throws during GC and takes AudioHost down.
internal static class WineProbe
{
    public static bool IsWine { get; } = Detect();

    private static bool Detect()
    {
        try
        {
            if (!NativeLibrary.TryLoad("ntdll", out var ntdll) &&
                !NativeLibrary.TryLoad("ntdll.dll", out ntdll))
            {
                return false;
            }

            return NativeLibrary.TryGetExport(ntdll, "wine_get_version", out _);
        }
        catch
        {
            return false;
        }
    }
}
