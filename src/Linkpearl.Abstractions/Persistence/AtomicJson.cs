using System.Text;
using System.Text.Json;
using Linkpearl.Diagnostics;

namespace Linkpearl.Persistence;

// Completes serialize first, then replaces via a unique temp in the same directory.
// Never deletes the destination first. Failed serialize or write leaves the last valid file.
public static class AtomicJson
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static bool TryRead<T>(string path, JsonSerializerOptions? options, out T? document,
        ref JsonCorruptHold corrupt, ILinkpearlLog? log)
        where T : class
    {
        document = null;
        if (path.Length == 0 || !File.Exists(path))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            document = JsonSerializer.Deserialize<T>(json, options);
            if (document is not null)
            {
                return true;
            }

            corrupt.Mark();
            Note(log, path, new JsonException());
            return false;
        }
        catch (JsonException failure)
        {
            corrupt.Mark();
            Note(log, path, failure);
            return false;
        }
        catch (NotSupportedException failure)
        {
            corrupt.Mark();
            Note(log, path, failure);
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool TrySave<T>(string path, T document, JsonSerializerOptions? options,
        ref JsonCorruptHold corrupt, ILinkpearlLog? log, CancellationToken cancellation = default)
    {
        if (!corrupt.PrepareWrite(path, log))
        {
            return false;
        }

        if (!TryWrite(path, document, options, log, cancellation))
        {
            return false;
        }

        corrupt.Clear();
        return true;
    }

    public static bool TryWrite<T>(string path, T document, JsonSerializerOptions? options = null,
        ILinkpearlLog? log = null, CancellationToken cancellation = default)
    {
        string json;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            json = JsonSerializer.Serialize(document, options);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception failure) when (failure is JsonException or NotSupportedException or ArgumentException)
        {
            Note(log, path, failure);
            return false;
        }

        return TryWriteText(path, json, log, cancellation);
    }

    public static bool TryWriteText(string path, string text, ILinkpearlLog? log = null,
        CancellationToken cancellation = default)
    {
        if (path.Length == 0)
        {
            return false;
        }

        var folder = Path.GetDirectoryName(path);
        if (folder is { Length: > 0 })
        {
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                Note(log, path, failure);
                return false;
            }
        }

        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            cancellation.ThrowIfCancellationRequested();
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                       FileOptions.None))
            {
                var bytes = Utf8.GetBytes(text);
                cancellation.ThrowIfCancellationRequested();
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            cancellation.ThrowIfCancellationRequested();
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (OperationCanceledException)
        {
            TryDelete(temp);
            throw;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
        {
            TryDelete(temp);
            Note(log, path, failure);
            return false;
        }
    }

    public static bool ParkCorrupt(string path, ILinkpearlLog? log = null)
    {
        if (path.Length == 0 || !File.Exists(path))
        {
            return true;
        }

        var sidecar = path + ".corrupt";
        if (File.Exists(sidecar))
        {
            sidecar = path + ".corrupt.1";
        }

        if (File.Exists(sidecar))
        {
            return true;
        }

        try
        {
            File.Copy(path, sidecar, overwrite: false);
            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            Note(log, path, failure);
            return File.Exists(path + ".corrupt") || File.Exists(path + ".corrupt.1");
        }
    }

    private static void Note(ILinkpearlLog? log, string path, Exception failure) =>
        log?.Write(LogSeverity.Warning, Path.GetFileName(path) + " " + failure.GetType().Name);

    private static void TryDelete(string path)
    {
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

public struct JsonCorruptHold
{
    private bool pending;

    public void Mark() => pending = true;

    public readonly bool IsPending => pending;

    public readonly bool PrepareWrite(string path, ILinkpearlLog? log = null) =>
        !pending || AtomicJson.ParkCorrupt(path, log);

    public void Clear() => pending = false;
}
