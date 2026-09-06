using System.Windows.Forms;
using Linkpearl.Platform;

namespace Linkpearl.Host.Platform;

public sealed class WindowsImagePicker : IFilePicker
{
    private readonly object gate = new();
    private bool picking;
    private IReadOnlyList<string>? taken;

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

    public void BeginImagePick()
    {
        lock (gate)
        {
            if (picking)
            {
                return;
            }

            picking = true;
            taken = null;
        }

        var worker = new Thread(Run)
        {
            IsBackground = true,
            Name = "Linkpearl-pictures",
        };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

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

    private void Run()
    {
        IReadOnlyList<string> result = [];
        try
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Upload pictures",
                Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.gif|All files|*.*",
                FilterIndex = 1,
                Multiselect = true,
                CheckFileExists = true,
                CheckPathExists = true,
                RestoreDirectory = true,
                AutoUpgradeEnabled = true,
                InitialDirectory = PicturesRoot(),
            };
            result = dialog.ShowDialog() == DialogResult.OK ? dialog.FileNames : [];
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            lock (gate)
            {
                taken = result;
                picking = false;
            }
        }
    }

    private static string PicturesRoot()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return pictures.Length > 0 && Directory.Exists(pictures)
            ? pictures
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
