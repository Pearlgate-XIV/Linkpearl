using System.Windows.Forms;
using Linkpearl.Platform;

namespace Linkpearl.Host.Platform;

// Vista+ Open dialog: nav pane on the left, address bar, list/details in the body.
public sealed class WindowsImagePicker : IFilePicker
{
    public IReadOnlyList<string> PickImageFiles()
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

        try
        {
            return dialog.ShowDialog() == DialogResult.OK ? dialog.FileNames : Array.Empty<string>();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<string>();
        }
    }

    private static string PicturesRoot()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return pictures.Length > 0 && Directory.Exists(pictures) ? pictures : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
