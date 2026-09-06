using System.Reflection;
using System.Runtime.InteropServices;
using NAudio.Lame;

namespace Linkpearl.Audio;

// LameDLLWrap P/Invokes "libmp3lame.64" by name. .NET 5+ LoadLibrary search is the
// game folder, not the plugin folder, so we load the file ourselves and resolve
// every lame import to that handle.
internal static class LameNative
{
    private static readonly object Gate = new();
    private static IntPtr handle;
    private static string pluginDir = string.Empty;
    private static bool watching;

    public static string Notice { get; private set; } = string.Empty;

    public static void Bind(string? extraDirectory = null)
    {
        lock (Gate)
        {
            if (!string.IsNullOrWhiteSpace(extraDirectory))
            {
                pluginDir = extraDirectory.Trim();
            }

            WatchWrapLoads();
            if (handle != IntPtr.Zero)
            {
                return;
            }

            var file = Environment.Is64BitProcess ? "libmp3lame.64.dll" : "libmp3lame.32.dll";
            foreach (var dir in SearchDirs())
            {
                var path = Path.Combine(dir, file);
                if (!File.Exists(path))
                {
                    continue;
                }

                if (NativeLibrary.TryLoad(path, out handle) && handle != IntPtr.Zero)
                {
                    try
                    {
                        NativeLibrary.SetDllImportResolver(typeof(LameMP3FileWriter).Assembly, Resolve);
                    }
                    catch (InvalidOperationException)
                    {
                        // Dalamud reload can keep NAudio.Lame loaded; a resolver may already be set.
                    }

                    LameDLL.LoadNativeDLL(dir);
                    StageBesideGame(path, file);
                    Notice = string.Empty;
                    return;
                }

                handle = IntPtr.Zero;
            }

            Notice = "MP3 encoder DLL was not next to the plugin (" + file + ").";
        }
    }

    private static void WatchWrapLoads()
    {
        if (watching)
        {
            return;
        }

        watching = true;
        AppDomain.CurrentDomain.AssemblyLoad += (_, args) =>
        {
            var name = args.LoadedAssembly.GetName().Name ?? string.Empty;
            if (name.Contains("LameDLLWrap", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("NAudio.Lame", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    NativeLibrary.SetDllImportResolver(args.LoadedAssembly, Resolve);
                }
                catch (InvalidOperationException)
                {
                }
            }
        };
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName.Contains("mp3lame", StringComparison.OrdinalIgnoreCase))
        {
            return handle;
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> SearchDirs()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in new[]
                 {
                     pluginDir,
                     Path.GetDirectoryName(typeof(LameNative).Assembly.Location),
                     Path.GetDirectoryName(typeof(LameMP3FileWriter).Assembly.Location),
                     AppContext.BaseDirectory,
                     Path.GetDirectoryName(Environment.ProcessPath),
                 })
        {
            var dir = (raw ?? string.Empty).Trim();
            if (dir.Length > 0 && seen.Add(dir))
            {
                yield return dir;
            }
        }
    }

    private static void StageBesideGame(string source, string file)
    {
        var gameDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (string.IsNullOrWhiteSpace(gameDir))
        {
            return;
        }

        try
        {
            CopyIfMissing(source, Path.Combine(gameDir, file));
            CopyIfMissing(source, Path.Combine(gameDir, "libmp3lame.dll"));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void CopyIfMissing(string source, string dest)
    {
        if (File.Exists(dest))
        {
            return;
        }

        File.Copy(source, dest, overwrite: false);
    }
}
