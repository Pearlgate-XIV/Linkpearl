using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace Linkpearl.Host.Windows;

internal enum FilePickMode : byte
{
    Images = 0,
    Attach = 1,
    Folder = 2,
}

internal sealed class FilePickWindow : Window
{
    private readonly Action<IReadOnlyList<string>, string> finished;
    private FilePickMode mode;
    private string current = string.Empty;
    private string search = string.Empty;
    private string typed = string.Empty;
    private string[] names = [];
    private bool[] isDir = [];
    private long[] sizes = [];
    private DateTime[] stamps = [];
    private readonly HashSet<string> selected = new(StringComparer.OrdinalIgnoreCase);
    private bool appear;
    private bool live;

    public FilePickWindow(Action<IReadOnlyList<string>, string> finished)
        : base("Files###LinkpearlFilePick",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking)
    {
        this.finished = finished;
        Size = new Vector2(920f, 560f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(640f, 400f),
            MaximumSize = new Vector2(4096f, 4096f),
        };
        RespectCloseHotkey = false;
        IsOpen = false;
    }

    public void Open(FilePickMode next, string directory)
    {
        mode = next;
        WindowName = Title() + "###LinkpearlFilePick";
        current = Directory.Exists(directory) ? directory : FallbackHome();
        search = string.Empty;
        typed = string.Empty;
        selected.Clear();
        appear = true;
        live = true;
        Reload();
        IsOpen = true;
    }

    public override void OnClose()
    {
        if (!live)
        {
            return;
        }

        live = false;
        finished([], string.Empty);
    }

    public override void PreDraw()
    {
        if (appear)
        {
            var view = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(view.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(920f, 560f) * ImGuiHelpers.GlobalScale, ImGuiCond.Appearing);
            appear = false;
        }

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.12f, 0.12f, 0.14f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.10f, 0.10f, 0.12f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.92f, 0.92f, 0.94f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.22f, 0.38f, 0.55f, 0.55f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.28f, 0.46f, 0.66f, 0.70f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.32f, 0.52f, 0.74f, 0.85f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.20f, 0.22f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.28f, 0.28f, 0.32f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, new Vector4(0.16f, 0.16f, 0.18f, 1f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(9);
    }

    public override void Draw()
    {
        DrawPath();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##lp-file-search", "Search", ref search, 200);
        var footer = 52f * ImGuiHelpers.GlobalScale;
        var body = ImGui.GetContentRegionAvail();
        body.Y = MathF.Max(120f, body.Y - footer);
        DrawPlaces(body.Y);
        ImGui.SameLine();
        DrawTable(body);
        DrawFooter();
    }

    private void DrawPath()
    {
        if (ImGui.Button("Up"))
        {
            GoUp();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(current);
    }

    private void DrawPlaces(float height)
    {
        ImGui.BeginChild("lp-file-places", new Vector2(176f * ImGuiHelpers.GlobalScale, height), true);
        foreach (var drive in DriveList())
        {
            Place(drive, drive);
        }

        Place("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        Place("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Place("Downloads", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
        Place("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        if (Directory.Exists(@"Z:\home"))
        {
            Place("Linux home", HostHome());
        }

        ImGui.EndChild();
    }

    private void Place(string label, string path)
    {
        if (path.Length == 0 || !Directory.Exists(path))
        {
            return;
        }

        if (ImGui.Selectable(label, string.Equals(current, path, StringComparison.OrdinalIgnoreCase)))
        {
            Go(path);
        }
    }

    private void DrawTable(Vector2 body)
    {
        ImGui.BeginChild("lp-file-list", new Vector2(body.X - 176f * ImGuiHelpers.GlobalScale - ImGui.GetStyle().ItemSpacing.X, body.Y), true);
        var flags = ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV |
            ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Hideable;
        var listedNames = names;
        var listedDirs = isDir;
        var listedSizes = sizes;
        var listedStamps = stamps;
        var listedDir = current;
        string? nextDir = null;
        if (ImGui.BeginTable("lp-file-rows", 4, flags))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("File Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 90f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Size", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Date", ImGuiTableColumnFlags.WidthFixed, 130f * ImGuiHelpers.GlobalScale);
            ImGui.TableHeadersRow();
            var query = search.Trim();
            if (Directory.GetParent(listedDir) is not null)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Selectable("../##parent", false, ImGuiSelectableFlags.SpanAllColumns))
                {
                    nextDir = Directory.GetParent(listedDir)?.FullName;
                }

                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Folder");
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
            }

            var count = Math.Min(listedNames.Length, Math.Min(listedDirs.Length, Math.Min(listedSizes.Length, listedStamps.Length)));
            for (var i = 0; i < count; i++)
            {
                var name = listedNames[i];
                var folder = listedDirs[i];
                if (query.Length > 0 && name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                var path = Path.Combine(listedDir, name);
                var on = selected.Contains(path);
                var label = folder ? name + "/" : name;
                if (ImGui.Selectable(label + "##" + i, on, ImGuiSelectableFlags.SpanAllColumns))
                {
                    if (folder)
                    {
                        nextDir = path;
                    }
                    else if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        Confirm([path], string.Empty);
                    }
                    else
                    {
                        Toggle(path);
                        typed = name;
                    }
                }

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(folder ? "Folder" : TypeName(name));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(folder ? string.Empty : SizeText(listedSizes[i]));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(listedStamps[i].ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture));
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();
        if (nextDir is { Length: > 0 })
        {
            Go(nextDir);
        }
    }

    private void DrawFooter()
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.55f);
        ImGui.InputTextWithHint("##lp-file-name", "File Name", ref typed, 260);
        ImGui.SameLine();
        ImGui.TextUnformatted(mode == FilePickMode.Folder ? "Folders" : mode == FilePickMode.Attach ? "Pictures, documents, and packs" : "Pictures");
        ImGui.SameLine();
        if (ImGui.Button("Ok", new Vector2(76f * ImGuiHelpers.GlobalScale, 0f)))
        {
            Accept();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(76f * ImGuiHelpers.GlobalScale, 0f)))
        {
            Confirm([], string.Empty);
        }
    }

    private void Accept()
    {
        if (mode == FilePickMode.Folder)
        {
            Confirm([], current);
            return;
        }

        if (selected.Count > 0)
        {
            Confirm([.. selected], string.Empty);
            return;
        }

        if (typed.Length > 0)
        {
            var path = Path.IsPathRooted(typed) ? typed : Path.Combine(current, typed);
            if (File.Exists(path))
            {
                Confirm([path], string.Empty);
                return;
            }
        }

        Confirm([], string.Empty);
    }

    private void Confirm(IReadOnlyList<string> files, string folder)
    {
        if (!live)
        {
            return;
        }

        live = false;
        IsOpen = false;
        finished(files, folder);
    }

    private void Toggle(string path)
    {
        if (!selected.Add(path))
        {
            selected.Remove(path);
        }
    }

    private void GoUp()
    {
        if (Directory.GetParent(current) is { } parent)
        {
            Go(parent.FullName);
        }
    }

    private void Go(string path)
    {
        current = path;
        selected.Clear();
        typed = string.Empty;
        Reload();
    }

    private void Reload()
    {
        var listed = new List<(string Name, bool Dir, long Size, DateTime Stamp)>();
        try
        {
            if (!Directory.Exists(current))
            {
                names = [];
                isDir = [];
                sizes = [];
                stamps = [];
                return;
            }

            foreach (var path in Directory.GetDirectories(current))
            {
                var name = Path.GetFileName(path);
                if (name.Length == 0 || name.StartsWith('.'))
                {
                    continue;
                }

                listed.Add((name, true, 0, SafeStamp(path)));
            }

            if (mode != FilePickMode.Folder)
            {
                foreach (var path in Directory.GetFiles(current))
                {
                    var name = Path.GetFileName(path);
                    if (name.Length == 0 || name.StartsWith('.') || !Matches(name))
                    {
                        continue;
                    }

                    listed.Add((name, false, SafeSize(path), SafeStamp(path)));
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        listed.Sort((left, right) =>
        {
            var dirs = right.Dir.CompareTo(left.Dir);
            return dirs != 0 ? dirs : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });
        names = listed.ConvertAll(row => row.Name).ToArray();
        isDir = listed.ConvertAll(row => row.Dir).ToArray();
        sizes = listed.ConvertAll(row => row.Size).ToArray();
        stamps = listed.ConvertAll(row => row.Stamp).ToArray();
    }

    private bool Matches(string name)
    {
        var ext = Path.GetExtension(name);
        if (mode == FilePickMode.Attach)
        {
            return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".log", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".dmp", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".doc", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".docx", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".tspack", StringComparison.OrdinalIgnoreCase);
        }

        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase);
    }

    private string Title() => mode switch
    {
        FilePickMode.Folder => "Choose folder",
        FilePickMode.Attach => "Attach files",
        _ => "Upload pictures",
    };

    private static string[] DriveList()
    {
        try
        {
            return Directory.GetLogicalDrives();
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static string FallbackHome()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (pictures.Length > 0 && Directory.Exists(pictures))
        {
            return pictures;
        }

        return HostHome();
    }

    private static string HostHome()
    {
        if (Directory.Exists(@"Z:\home"))
        {
            try
            {
                var homes = Directory.GetDirectories(@"Z:\home");
                if (homes.Length > 0)
                {
                    var pictures = Path.Combine(homes[0], "Pictures");
                    return Directory.Exists(pictures) ? pictures : homes[0];
                }
            }
            catch (IOException)
            {
            }
        }

        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return user.Length > 0 ? user : @"Z:\";
    }

    private static DateTime SafeStamp(string path)
    {
        try
        {
            return File.GetLastWriteTime(path);
        }
        catch (IOException)
        {
            return DateTime.MinValue;
        }
    }

    private static long SafeSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static string SizeText(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        }

        if (bytes < 1024 * 1024)
        {
            return (bytes / 1024f).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
        }

        return (bytes / (1024f * 1024f)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
    }

    private static string TypeName(string name)
    {
        var ext = Path.GetExtension(name);
        return ext.Length == 0 ? "File" : ext.TrimStart('.').ToUpperInvariant();
    }
}
