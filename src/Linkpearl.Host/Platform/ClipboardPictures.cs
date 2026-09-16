using System.Collections.Specialized;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Linkpearl.Host.Platform;

internal static class ClipboardPictures
{
    internal const string TempPrefix = "lp-clip-";

    internal static IReadOnlyList<string> ExportOnSta()
    {
        IReadOnlyList<string> paths = [];
        var worker = new Thread(() => paths = Export())
        {
            IsBackground = true,
            Name = "Linkpearl-clipboard",
        };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
        if (!worker.Join(TimeSpan.FromSeconds(2)))
        {
            return [];
        }

        return paths;
    }

    internal static void ForgetTemps(IReadOnlyList<string> paths)
    {
        var temp = Path.GetTempPath();
        for (var index = 0; index < paths.Count; index++)
        {
            var path = paths[index];
            var name = Path.GetFileName(path);
            if (!name.StartsWith(TempPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!path.StartsWith(temp, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static List<string> Export()
    {
        try
        {
            var files = FromDropList();
            if (files.Count > 0)
            {
                return files;
            }

            var png = FromPng();
            if (png.Length > 0)
            {
                return [png];
            }

            var bitmap = FromBitmap();
            return bitmap.Length > 0 ? [bitmap] : [];
        }
        catch (ExternalException)
        {
            return [];
        }
        catch (ThreadStateException)
        {
            return [];
        }
    }

    private static List<string> FromDropList()
    {
        if (!Clipboard.ContainsFileDropList())
        {
            return [];
        }

        StringCollection? dropped;
        try
        {
            dropped = Clipboard.GetFileDropList();
        }
        catch (ExternalException)
        {
            return [];
        }

        if (dropped is null || dropped.Count == 0)
        {
            return [];
        }

        var pictures = new List<string>(dropped.Count);
        for (var index = 0; index < dropped.Count; index++)
        {
            var path = dropped[index];
            if (path is { Length: > 0 } && File.Exists(path) && PhotoKind(path))
            {
                pictures.Add(path);
            }
        }

        return pictures;
    }

    private static string FromPng()
    {
        var blob = ReadFormat("PNG") ?? ReadFormat("image/png");
        if (blob is null || blob.Length == 0)
        {
            return string.Empty;
        }

        return WriteTemp(".png", blob);
    }

    private static string FromBitmap()
    {
        if (!Clipboard.ContainsImage())
        {
            return string.Empty;
        }

        using var image = Clipboard.GetImage();
        if (image is null)
        {
            return string.Empty;
        }

        var dest = TempPath(".png");
        try
        {
            image.Save(dest, ImageFormat.Png);
            return dest;
        }
        catch (ExternalException)
        {
            TryDelete(dest);
            return string.Empty;
        }
        catch (IOException)
        {
            TryDelete(dest);
            return string.Empty;
        }
    }

    private static byte[]? ReadFormat(string format)
    {
        if (Clipboard.TryGetData<MemoryStream>(format, out var stream) && stream is not null)
        {
            return ReadAll(stream);
        }

        if (Clipboard.TryGetData<byte[]>(format, out var bytes) && bytes is { Length: > 0 })
        {
            return bytes;
        }

        return null;
    }

    private static byte[] ReadAll(MemoryStream stream)
    {
        if (stream.TryGetBuffer(out var segment) && segment.Array is not null)
        {
            var copy = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array, segment.Offset, copy, 0, segment.Count);
            return copy;
        }

        return stream.ToArray();
    }

    private static string WriteTemp(string ext, byte[] bytes)
    {
        var dest = TempPath(ext);
        try
        {
            File.WriteAllBytes(dest, bytes);
            return dest;
        }
        catch (IOException)
        {
            TryDelete(dest);
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            TryDelete(dest);
            return string.Empty;
        }
    }

    private static string TempPath(string ext) =>
        Path.Combine(Path.GetTempPath(), TempPrefix + Guid.NewGuid().ToString("N") + ext);

    private static bool PhotoKind(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (path.Length > 0 && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
