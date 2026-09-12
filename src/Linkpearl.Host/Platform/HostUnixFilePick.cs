using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Linkpearl.Host.Platform;

internal static class HostUnixFilePick
{
    public static bool OnUnixHost => IsWine() || Directory.Exists("/usr/bin");

    public static bool TryPick(bool folder, bool attach, string wineStart, out IReadOnlyList<string> files,
        out string pickedFolder)
    {
        files = [];
        pickedFolder = string.Empty;
        var start = PreferredUnixStart(wineStart);
        var title = folder ? "Choose folder" : attach ? "Attach files" : "Upload pictures";
        var kdialog = FirstTool(
            "/usr/bin/kdialog",
            "/usr/local/bin/kdialog",
            "/bin/kdialog");
        if (kdialog is not null)
        {
            return folder
                ? TryKdialogFolder(kdialog, title, start, out pickedFolder)
                : TryKdialogFiles(kdialog, title, start, attach, out files);
        }

        var zenity = FirstTool(
            "/usr/bin/zenity",
            "/usr/local/bin/zenity",
            "/bin/zenity");
        if (zenity is null)
        {
            return false;
        }

        return folder
            ? TryZenityFolder(zenity, title, start, out pickedFolder)
            : TryZenityFiles(zenity, title, start, attach, out files);
    }

    private static bool TryKdialogFiles(string tool, string title, string start, bool attach,
        out IReadOnlyList<string> files)
    {
        files = [];
        var args = new List<string>
        {
            "--title",
            title,
            "--geometry",
            "1280x800+80+60",
            "--multiple",
            "--separate-output",
            "--getopenfilename",
            start,
            attach
                ? "*.png *.jpg *.jpeg *.webp *.gif *.txt *.log *.dmp *.pdf *.doc *.docx *.tspack|Crash, pictures, documents, and packs"
                : "*.png *.jpg *.jpeg *.bmp *.webp *.gif|Pictures",
        };
        if (!TryRun(tool, args, out var stdout, out var code) || code != 0)
        {
            return code is 0 or 1;
        }

        files = SplitUnixLines(stdout, pipes: false);
        return true;
    }

    private static bool TryKdialogFolder(string tool, string title, string start, out string pickedFolder)
    {
        pickedFolder = string.Empty;
        var args = new List<string>
        {
            "--title",
            title,
            "--geometry",
            "1280x800+80+60",
            "--getexistingdirectory",
            start,
        };
        if (!TryRun(tool, args, out var stdout, out var code) || code != 0)
        {
            return code is 0 or 1;
        }

        var line = FirstLine(stdout);
        if (line.Length > 0)
        {
            pickedFolder = ForPlugin(line);
        }

        return true;
    }

    private static bool TryZenityFiles(string tool, string title, string start, bool attach,
        out IReadOnlyList<string> files)
    {
        files = [];
        var args = new List<string>
        {
            "--file-selection",
            "--multiple",
            "--separator=|",
            "--width=1280",
            "--height=800",
            "--title=" + title,
            "--filename=" + TrailingSlash(start),
            attach
                ? "--file-filter=Crash, pictures, documents, and packs | *.png *.jpg *.jpeg *.webp *.gif *.txt *.log *.dmp *.pdf *.doc *.docx *.tspack"
                : "--file-filter=Pictures | *.png *.jpg *.jpeg *.bmp *.webp *.gif",
        };
        if (!TryRun(tool, args, out var stdout, out var code) || code != 0)
        {
            return code is 0 or 1;
        }

        files = SplitUnixLines(stdout, pipes: true);
        return true;
    }

    private static bool TryZenityFolder(string tool, string title, string start, out string pickedFolder)
    {
        pickedFolder = string.Empty;
        var args = new List<string>
        {
            "--file-selection",
            "--directory",
            "--width=1280",
            "--height=800",
            "--title=" + title,
            "--filename=" + TrailingSlash(start),
        };
        if (!TryRun(tool, args, out var stdout, out var code) || code != 0)
        {
            return code is 0 or 1;
        }

        var line = FirstLine(stdout);
        if (line.Length > 0)
        {
            pickedFolder = ForPlugin(line);
        }

        return true;
    }

    private static string ForPlugin(string unixOrWine)
    {
        var trimmed = unixOrWine.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        if (LooksWindows(trimmed) && (File.Exists(trimmed) || Directory.Exists(trimmed)))
        {
            return trimmed;
        }

        if (trimmed.StartsWith('/'))
        {
            if (File.Exists(trimmed) || Directory.Exists(trimmed))
            {
                return trimmed;
            }

            return ToWine(trimmed);
        }

        return trimmed;
    }

    private static List<string> SplitUnixLines(string stdout, bool pipes)
    {
        var lines = pipes
            ? stdout.Split(['\n', '\r', '|'], StringSplitOptions.RemoveEmptyEntries)
            : stdout.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var mapped = new List<string>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var path = ForPlugin(lines[i]);
            if (path.Length > 0)
            {
                mapped.Add(path);
            }
        }

        return mapped;
    }

    private static string FirstLine(string stdout)
    {
        var lines = stdout.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        return lines.Length == 0 ? string.Empty : lines[0].Trim();
    }

    private static bool TryRun(string unixTool, List<string> args, out string stdout, out int code)
    {
        stdout = string.Empty;
        code = -1;
        try
        {
            if (IsWine())
            {
                return TryRunWineScript(unixTool, args, out stdout, out code);
            }

            return TryRunDirect(unixTool, args, out stdout, out code);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryRunWineScript(string unixTool, List<string> args, out string stdout, out int code)
    {
        stdout = string.Empty;
        code = -1;
        var id = Guid.NewGuid().ToString("N");
        var wineDir = PickWineTemp();
        var scriptWine = Path.Combine(wineDir, "lp-pick-" + id + ".sh");
        var outWine = Path.Combine(wineDir, "lp-pick-" + id + ".out");
        var rcWine = Path.Combine(wineDir, "lp-pick-" + id + ".rc");
        var readyWine = Path.Combine(wineDir, "lp-pick-" + id + ".go");
        var scriptUnix = ToUnix(scriptWine);
        var outUnix = ToUnix(outWine);
        var rcUnix = ToUnix(rcWine);
        var readyUnix = ToUnix(readyWine);
        if (scriptUnix.Length == 0 || outUnix.Length == 0 || rcUnix.Length == 0 || readyUnix.Length == 0)
        {
            return false;
        }

        try
        {
            File.WriteAllText(scriptWine, BuildUnixScript(unixTool, args, outUnix, rcUnix, readyUnix), Utf8NoBom);
            if (!TryFireUnix("/bin/sh", scriptUnix))
            {
                return false;
            }

            if (!WaitForFile(readyWine, TimeSpan.FromSeconds(4)))
            {
                return false;
            }

            if (!WaitForFile(rcWine, TimeSpan.FromMinutes(15)))
            {
                return false;
            }

            var rcText = File.ReadAllText(rcWine).Trim();
            if (!int.TryParse(rcText, out code))
            {
                code = 1;
            }

            stdout = File.Exists(outWine) ? File.ReadAllText(outWine) : string.Empty;
            return true;
        }
        finally
        {
            TryDelete(scriptWine);
            TryDelete(outWine);
            TryDelete(rcWine);
            TryDelete(readyWine);
        }
    }

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static string BuildUnixScript(string unixTool, List<string> args, string outUnix, string rcUnix,
        string readyUnix)
    {
        var sh = new StringBuilder();
        sh.Append("#!/bin/sh\n");
        AppendExport(sh, "DISPLAY");
        AppendExport(sh, "XAUTHORITY");
        AppendExport(sh, "XDG_RUNTIME_DIR");
        AppendExport(sh, "DBUS_SESSION_BUS_ADDRESS");
        sh.Append("if [ -z \"$DISPLAY\" ]; then export DISPLAY=:0; fi\n");
        sh.Append("if [ -z \"$XDG_RUNTIME_DIR\" ]; then export XDG_RUNTIME_DIR=/run/user/$(id -u); fi\n");
        sh.Append("if [ -z \"$DBUS_SESSION_BUS_ADDRESS\" ]; then export DBUS_SESSION_BUS_ADDRESS=unix:path=$XDG_RUNTIME_DIR/bus; fi\n");
        sh.Append("export QT_QPA_PLATFORM=xcb\n");
        sh.Append("export GDK_BACKEND=x11\n");
        sh.Append("unset WAYLAND_DISPLAY\n");
        sh.Append("export XDG_CURRENT_DESKTOP=KDE\n");
        sh.Append("echo 1 > ");
        sh.Append(ShQuote(readyUnix));
        sh.Append('\n');
        sh.Append(ShQuote(unixTool));
        for (var i = 0; i < args.Count; i++)
        {
            sh.Append(' ');
            sh.Append(ShQuote(args[i]));
        }

        sh.Append(" > ");
        sh.Append(ShQuote(outUnix));
        sh.Append(" 2>/dev/null\n");
        sh.Append("echo $? > ");
        sh.Append(ShQuote(rcUnix));
        sh.Append('\n');
        return sh.ToString();
    }

    private static void AppendExport(StringBuilder sh, string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value) || value.Contains('\n', StringComparison.Ordinal))
        {
            return;
        }

        sh.Append("export ");
        sh.Append(name);
        sh.Append('=');
        sh.Append(ShQuote(value));
        sh.Append('\n');
    }

    private static bool TryFireUnix(string unixFile, string scriptUnix)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var start = Path.Combine(windows, "system32", "start.exe");
        var cmd = Path.Combine(windows, "system32", "cmd.exe");
        var unix = "/unix " + unixFile + " " + QuoteCmd(scriptUnix);
        return TryProcess(start, unix, wait: false, shell: true)
            || TryProcess(start, unix, wait: false, shell: false)
            || TryProcess(cmd, "/c start " + unix, wait: false, shell: false);
    }

    private static bool TryProcess(string fileName, string arguments, bool wait, bool shell)
    {
        if (!File.Exists(fileName))
        {
            return false;
        }

        try
        {
            using var process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = shell;
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.RedirectStandardOutput = false;
            process.StartInfo.RedirectStandardError = false;
            if (!process.Start())
            {
                return false;
            }

            if (wait)
            {
                process.WaitForExit();
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool WaitForFile(string path, TimeSpan limit)
    {
        var deadline = DateTime.UtcNow + limit;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return File.Exists(path);
    }

    private static bool TryRunDirect(string unixTool, List<string> args, out string stdout, out int code)
    {
        stdout = string.Empty;
        code = -1;
        using var process = new Process();
        process.StartInfo.FileName = unixTool;
        for (var i = 0; i < args.Count; i++)
        {
            process.StartInfo.ArgumentList.Add(args[i]);
        }

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        var builder = new StringBuilder();
        process.ErrorDataReceived += (_, _) =>
        {
        };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(e.Data);
            }
        };
        if (!process.Start())
        {
            return false;
        }

        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        process.WaitForExit();
        code = process.ExitCode;
        stdout = builder.ToString();
        return true;
    }

    private static string PickWineTemp() =>
        Directory.Exists(@"Z:\tmp") ? @"Z:\tmp" : Path.GetTempPath();

    private static string? FirstTool(params string[] unixPaths)
    {
        if (IsWine())
        {
            return unixPaths[0];
        }

        for (var i = 0; i < unixPaths.Length; i++)
        {
            var unix = unixPaths[i];
            if (File.Exists(unix) || File.Exists(ToWine(unix)))
            {
                return unix;
            }
        }

        return null;
    }

    private static string PreferredUnixStart(string wineStart)
    {
        var mapped = ToUnix(wineStart);
        if (mapped.Length > 0 && HostDirExists(mapped))
        {
            return mapped;
        }

        var home = UnixHome();
        if (home.Length > 0)
        {
            var pictures = home.TrimEnd('/') + "/Pictures";
            if (HostDirExists(pictures))
            {
                return pictures;
            }

            if (HostDirExists(home))
            {
                return home;
            }
        }

        return "/";
    }

    private static bool HostDirExists(string unix) =>
        Directory.Exists(unix) || Directory.Exists(ToWine(unix));

    private static string UnixHome()
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(home) && home.StartsWith('/'))
        {
            return home.TrimEnd('/');
        }

        if (Directory.Exists(@"Z:\home"))
        {
            try
            {
                var homes = Directory.GetDirectories(@"Z:\home");
                if (homes.Length > 0)
                {
                    return ToUnix(homes[0]);
                }
            }
            catch (IOException)
            {
            }
        }

        var fromZ = ToUnix(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        return fromZ.StartsWith('/') ? fromZ.TrimEnd('/') : string.Empty;
    }

    private static string ToUnix(string path)
    {
        var trimmed = path.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        if (trimmed.StartsWith('/'))
        {
            return trimmed;
        }

        if (!LooksWindows(trimmed))
        {
            return trimmed.Replace('\\', '/');
        }

        var letter = char.ToUpperInvariant(trimmed[0]);
        var rest = trimmed[2..].Replace('\\', '/').Trim('/');
        if (letter == 'Z')
        {
            return rest.Length == 0 ? "/" : "/" + rest;
        }

        var prefix = UnixWinePrefix();
        if (prefix.Length == 0)
        {
            return string.Empty;
        }

        return prefix.TrimEnd('/') + "/drive_" + char.ToLowerInvariant(letter) + "/" + rest;
    }

    private static string UnixWinePrefix()
    {
        var prefix = Environment.GetEnvironmentVariable("WINEPREFIX");
        if (!string.IsNullOrEmpty(prefix))
        {
            if (prefix.StartsWith('/'))
            {
                return prefix;
            }

            if (LooksWindows(prefix) && char.ToUpperInvariant(prefix[0]) == 'Z')
            {
                return ToUnix(prefix);
            }
        }

        if (Directory.Exists(@"Z:\home"))
        {
            try
            {
                var homes = Directory.GetDirectories(@"Z:\home");
                for (var i = 0; i < homes.Length; i++)
                {
                    var xl = Path.Combine(homes[i], ".xlcore", "wineprefix");
                    if (Directory.Exists(xl))
                    {
                        return ToUnix(xl);
                    }
                }
            }
            catch (IOException)
            {
            }
        }

        return string.Empty;
    }

    private static string ToWine(string unix)
    {
        if (unix.Length == 0 || LooksWindows(unix))
        {
            return unix;
        }

        if (!unix.StartsWith('/'))
        {
            return unix;
        }

        return @"Z:" + unix.Replace('/', '\\');
    }

    private static bool LooksWindows(string path) => path.Length >= 2 && path[1] == ':';

    private static string TrailingSlash(string unix) => unix.EndsWith('/') ? unix : unix + "/";

    private static string ShQuote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static string QuoteCmd(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        if (value.IndexOfAny([' ', '"']) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

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

    private static bool IsWine()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINEPREFIX")))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINELOADER")))
        {
            return true;
        }

        if (Directory.Exists(@"Z:\usr\bin") || Directory.Exists(@"Z:\home"))
        {
            return true;
        }

        try
        {
            if (NativeLibrary.TryLoad("ntdll.dll", out var ntdll)
                && NativeLibrary.TryGetExport(ntdll, "wine_get_version", out _))
            {
                return true;
            }
        }
        catch (DllNotFoundException)
        {
        }

        return false;
    }
}
