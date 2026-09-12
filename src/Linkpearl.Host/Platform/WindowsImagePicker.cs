using System.Windows.Forms;
using Linkpearl.Host.Windows;
using Linkpearl.Platform;

namespace Linkpearl.Host.Platform;

public sealed class WindowsImagePicker : IFilePicker
{
    private readonly object gate = new();
    private bool picking;
    private IReadOnlyList<string>? taken;
    private string? folder;
    private string startDirectory = string.Empty;
    private PickKind kind;
    private FilePickWindow? glass;

    public bool Picking
    {
        get
        {
            lock (gate)
            {
                return picking;
            }
        }
    }

    internal void Bind(FilePickWindow window) => glass = window;

    public void BeginImagePick() => Begin(PickKind.Images, string.Empty);

    public void BeginImagePickFrom(string directory) => Begin(PickKind.Images, directory);

    public void BeginAttachPick() => Begin(PickKind.Attach, string.Empty);

    public void BeginAudioPick() => Begin(PickKind.Audio, string.Empty);

    public void BeginFolderPick() => Begin(PickKind.Folder, string.Empty);

    public void BeginFolderPickFrom(string directory) => Begin(PickKind.Folder, directory);

    public bool TryTakeImages(out IReadOnlyList<string> paths)
    {
        lock (gate)
        {
            if (taken is null)
            {
                paths = [];
                return false;
            }

            paths = taken;
            taken = null;
            return true;
        }
    }

    public bool TryTakeFolder(out string path)
    {
        lock (gate)
        {
            if (folder is null)
            {
                path = string.Empty;
                return false;
            }

            path = folder;
            folder = null;
            return true;
        }
    }

    public bool TryTakeClipboardImages(out IReadOnlyList<string> paths)
    {
        paths = ClipboardPictures.ExportOnSta();
        return paths.Count > 0;
    }

    internal void AcceptGlass(IReadOnlyList<string> files, string pickedFolder)
    {
        lock (gate)
        {
            if (kind == PickKind.Folder)
            {
                folder = pickedFolder;
            }
            else
            {
                taken = files;
            }

            picking = false;
        }
    }

    private void Begin(PickKind next, string directory)
    {
        lock (gate)
        {
            if (picking)
            {
                return;
            }

            picking = true;
            taken = null;
            folder = null;
            kind = next;
            startDirectory = directory.Trim();
        }

        if (HostUnixFilePick.OnUnixHost && glass is not null)
        {
            var mode = next == PickKind.Folder
                ? FilePickMode.Folder
                : next == PickKind.Attach
                    ? FilePickMode.Attach
                    : next == PickKind.Audio
                        ? FilePickMode.Audio
                        : FilePickMode.Images;
            glass.Open(mode, ExistingStart());
            return;
        }

        var worker = new Thread(Run)
        {
            IsBackground = true,
            Name = "Linkpearl-pictures",
        };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

    private void Run()
    {
        IReadOnlyList<string> images = [];
        var pickedFolder = string.Empty;
        var pick = kind;
        var start = ExistingStart();
        try
        {
            if (pick == PickKind.Folder)
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = "Link GPose folder",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = false,
                    SelectedPath = start,
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    pickedFolder = dialog.SelectedPath ?? string.Empty;
                }
            }
            else
            {
                var attach = pick == PickKind.Attach;
                var audio = pick == PickKind.Audio;
                using var dialog = new OpenFileDialog
                {
                    Title = audio ? "Load a track" : attach ? "Attach files" : "Upload pictures",
                    Filter = audio
                        ? "Audio|*.mp3;*.wav;*.flac;*.ogg;*.m4a;*.aac;*.wma;*.aiff;*.aif|All files|*.*"
                        : attach
                        ? "Crash, pictures, documents, and packs|*.png;*.jpg;*.jpeg;*.webp;*.gif;*.txt;*.log;*.dmp;*.pdf;*.doc;*.docx;*.tspack|All files|*.*"
                        : "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.gif|All files|*.*",
                    FilterIndex = 1,
                    Multiselect = !audio,
                    CheckFileExists = true,
                    CheckPathExists = true,
                    RestoreDirectory = true,
                    AutoUpgradeEnabled = true,
                    InitialDirectory = start,
                };
                images = dialog.ShowDialog() == DialogResult.OK ? dialog.FileNames : [];
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            lock (gate)
            {
                if (pick == PickKind.Folder)
                {
                    folder = pickedFolder;
                }
                else
                {
                    taken = images;
                }

                picking = false;
            }
        }
    }

    private string ExistingStart()
    {
        if (startDirectory.Length > 0 && Directory.Exists(startDirectory))
        {
            return startDirectory;
        }

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

        var picturesWin = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return picturesWin.Length > 0 && Directory.Exists(picturesWin)
            ? picturesWin
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private enum PickKind : byte
    {
        Images = 0,
        Folder = 1,
        Attach = 2,
        Audio = 3,
    }
}
