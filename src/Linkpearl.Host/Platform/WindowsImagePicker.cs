using System.Windows.Forms;
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

    public void BeginImagePick() => Begin(PickKind.Images, string.Empty);

    public void BeginImagePickFrom(string directory) => Begin(PickKind.Images, directory);

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
                using var dialog = new OpenFileDialog
                {
                    Title = "Upload pictures",
                    Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.gif|All files|*.*",
                    FilterIndex = 1,
                    Multiselect = true,
                    CheckFileExists = true,
                    CheckPathExists = true,
                    RestoreDirectory = false,
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

        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return pictures.Length > 0 && Directory.Exists(pictures)
            ? pictures
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private enum PickKind : byte
    {
        Images = 0,
        Folder = 1,
    }
}
