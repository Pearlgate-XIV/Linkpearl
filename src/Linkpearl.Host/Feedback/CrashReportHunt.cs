using Linkpearl.Feedback;

namespace Linkpearl.Host.Feedback;

internal static class CrashReportHunt
{
    private const int Cap = 14;

    public static CrashPick[] Recent()
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in Roots())
        {
            AddFolder(folder, groups);
        }

        var picks = new List<CrashPick>();
        foreach (var pair in groups)
        {
            var files = pair.Value
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
                .ToArray();
            if (files.Length == 0)
            {
                continue;
            }

            var newest = files[0];
            var at = File.GetLastWriteTimeUtc(newest);
            var bytes = files.Sum(path => new FileInfo(path).Length);
            picks.Add(new CrashPick(Label(files), SizeStamp(bytes, at), files));
        }

        return picks
            .OrderByDescending(pick => File.GetLastWriteTimeUtc(pick.Paths[0]))
            .Take(Cap)
            .ToArray();
    }

    private static IEnumerable<string> Roots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        yield return Path.Combine(home, ".xlcore", "logs");
        yield return Path.Combine(home, ".xlcore", "ffxivConfig");
        yield return Path.Combine(app, "XIVLauncher", "dalamudLogs");
        yield return Path.Combine(app, "XIVLauncher");
        yield return Path.Combine(docs, "My Games", "FINAL FANTASY XIV - A Realm Reborn");
        var xlcoreDocs = Path.Combine(home, ".xlcore", "ffxivConfig");
        yield return xlcoreDocs;
    }

    private static void AddFolder(string folder, Dictionary<string, List<string>> groups)
    {
        if (folder.Length == 0 || !Directory.Exists(folder))
        {
            return;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(folder);
        }
        catch
        {
            return;
        }

        for (var index = 0; index < files.Length; index++)
        {
            var path = files[index];
            var name = Path.GetFileName(path);
            if (!IsCrashFile(name))
            {
                continue;
            }

            var key = GroupKey(name);
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(path);
            var twin = Twin(path);
            if (twin.Length > 0 && File.Exists(twin))
            {
                list.Add(twin);
            }
        }
    }

    private static bool IsCrashFile(string name)
    {
        if (name.StartsWith("dalamud_appcrash_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.StartsWith("dalamud.crashhandler", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(name, "dalamud.troubleshooting.json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ext = Path.GetExtension(name);
        if (ext.Equals(".dmp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return name.Contains("crash", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("exception", StringComparison.OrdinalIgnoreCase);
    }

    private static string GroupKey(string name)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        if (stem.StartsWith("dalamud_appcrash_", StringComparison.OrdinalIgnoreCase) &&
            stem.Length > "dalamud_appcrash_".Length)
        {
            return stem;
        }

        return name.StartsWith("dalamud.crashhandler", StringComparison.OrdinalIgnoreCase)
            ? "dalamud.crashhandler"
            : stem;
    }

    private static string Twin(string path)
    {
        var ext = Path.GetExtension(path);
        if (ext.Equals(".log", StringComparison.OrdinalIgnoreCase))
        {
            return Path.ChangeExtension(path, ".dmp");
        }

        if (ext.Equals(".dmp", StringComparison.OrdinalIgnoreCase))
        {
            return Path.ChangeExtension(path, ".log");
        }

        return string.Empty;
    }

    private static string Label(IReadOnlyList<string> files)
    {
        var name = Path.GetFileName(files[0]);
        if (name.StartsWith("dalamud_appcrash_", StringComparison.OrdinalIgnoreCase))
        {
            return "Dalamud crash " + Path.GetFileNameWithoutExtension(name)["dalamud_appcrash_".Length..];
        }

        if (name.StartsWith("dalamud.crashhandler", StringComparison.OrdinalIgnoreCase))
        {
            return "Dalamud crash handler";
        }

        if (name.Contains("ffxiv", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("exception", StringComparison.OrdinalIgnoreCase))
        {
            return "FFXIV " + name;
        }

        return name;
    }

    private static string SizeStamp(long bytes, DateTime at)
    {
        var size = bytes < 1024 * 1024
            ? Math.Max(1, bytes / 1024) + " KB"
            : (bytes / (1024f * 1024f)).ToString("0.0") + " MB";
        return size + " · " + at.ToLocalTime().ToString("MMM d h:mm tt");
    }
}
