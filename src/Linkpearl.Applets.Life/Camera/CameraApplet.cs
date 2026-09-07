using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Camera;

public sealed partial class CameraApplet : IApplet
{
    public static readonly AppletManifest Manifest = new()
    {
        Id = "camera",
        DisplayNameKey = "Camera",
        Family = AppletFamily.Life,
        Glyph = "◎",
        HomeOrder = 28,
        Capabilities = AppletCapabilities.UsesCamera,
    };

    private readonly IClock clock;
    private readonly IGameSession game;
    private readonly HostPaths paths;
    private readonly ITextureSource textures;
    private readonly IFilePicker files;
    private readonly DisplayPreferences display;
    private readonly PhotoLibrary library;
    private readonly List<(string Key, string Title, List<PhotoShot> Items)> albums = new();
    private Pane pane = Pane.Camera;
    private Mode mode = Mode.Page;
    private float scroll;
    private string viewingId = string.Empty;
    private string lastStill = string.Empty;
    private string place = string.Empty;
    private string folderDraft = string.Empty;
    private string cropSource = string.Empty;
    private string cropExisting = string.Empty;
    private string cropKey = string.Empty;
    private byte[]? cropPreview;
    private float cropX;
    private float cropY;
    private float cropW = 1f;
    private float cropH = 1f;
    private int cropTurns;
    private CropDrag cropDrag;
    private Vector2 cropGrab;
    private float grabX;
    private float grabY;
    private float grabW;
    private float grabH;
    private bool confirmRemove;
    private bool confirmDropFolder;
    private bool confirmBulkRemove;
    private bool picking;
    private readonly HashSet<string> picked = new(StringComparer.Ordinal);
    private string menuShotId = string.Empty;
    private Vector2 menuAt;
    private bool uploadWait;
    private bool gposeWait;

    public CameraApplet(IClock clock, IGameSession game, HostPaths paths, ITextureSource textures, IFilePicker files,
        DisplayPreferences display)
    {
        this.clock = clock;
        this.game = game;
        this.paths = paths;
        this.textures = textures;
        this.files = files;
        this.display = display;
        library = PhotoLibrary.Load(paths);
        RebuildAlbums();
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public string Place => mode == Mode.Viewer ? "photo" : pane == Pane.Gallery ? "gallery" : "camera";

    public void Enter(AppletEntry entry)
    {
        if (string.Equals(entry.RouteHint, "gallery", StringComparison.OrdinalIgnoreCase))
        {
            pane = Pane.Gallery;
        }
        else if (string.Equals(entry.RouteHint, "camera", StringComparison.OrdinalIgnoreCase))
        {
            pane = Pane.Camera;
        }
    }

    public void Leave()
    {
        mode = Mode.Page;
        viewingId = string.Empty;
        menuShotId = string.Empty;
        EndPick();
        display.Landscape = false;
        library.Save();
    }

    public bool CanGoBack => picking || confirmBulkRemove || mode != Mode.Page || pane != Pane.Camera ||
        place.Length > 0;

    public bool Back()
    {
        if (menuShotId.Length > 0)
        {
            menuShotId = string.Empty;
            return true;
        }

        if (confirmRemove || confirmDropFolder || confirmBulkRemove)
        {
            confirmRemove = false;
            confirmDropFolder = false;
            confirmBulkRemove = false;
            return true;
        }

        if (picking)
        {
            EndPick();
            return true;
        }

        if (mode == Mode.Crop)
        {
            CancelCrop();
            return true;
        }

        if (mode is Mode.Move or Mode.MakeFolder)
        {
            mode = viewingId.Length > 0 ? Mode.Viewer : Mode.Page;
            folderDraft = string.Empty;
            return true;
        }

        if (mode == Mode.Viewer)
        {
            mode = Mode.Page;
            viewingId = string.Empty;
            return true;
        }

        if (place.Length > 0)
        {
            place = string.Empty;
            scroll = 0f;
            return true;
        }

        if (pane == Pane.Camera)
        {
            return false;
        }

        pane = Pane.Camera;
        scroll = 0f;
        return true;
    }

    public void Compose(in AppletFrame frame)
    {
        FinishUpload();
        FinishGposeLink();
        if (mode == Mode.Crop)
        {
            DrawCrop(frame);
            return;
        }

        if (mode == Mode.Viewer)
        {
            DrawViewer(frame);
            return;
        }

        if (mode == Mode.Move)
        {
            DrawMove(frame);
            return;
        }

        DrawTabs(frame);
        var body = frame.Content.Inset(new Edges(0f, frame.Units(44f), 0f, 0f));
        if (pane == Pane.Gallery)
        {
            DrawGallery(frame.WithContent(body));
            return;
        }

        DrawCamera(frame.WithContent(body));
    }

    private void DrawTabs(in AppletFrame frame)
    {
        var row = frame.Content.TopSlice(frame.Units(40f)).Inset(new Edges(frame.Units(10f), frame.Units(6f),
            frame.Units(10f), frame.Units(2f)));
        var gap = frame.Units(6f);
        var cell = (row.Width - gap) * 0.5f;
        Chip(frame, row.LeftSlice(cell), "Camera", pane == Pane.Camera, () =>
        {
            EndPick();
            pane = Pane.Camera;
        });
        Chip(frame, row.RightSlice(cell), "Gallery", pane == Pane.Gallery, () =>
        {
            pane = Pane.Gallery;
            scroll = 0f;
        });
    }

    private void EndPick()
    {
        picking = false;
        confirmBulkRemove = false;
        picked.Clear();
    }

    private void BeginPick(string firstId)
    {
        picking = true;
        confirmBulkRemove = false;
        menuShotId = string.Empty;
        mode = Mode.Page;
        if (firstId.Length > 0)
        {
            picked.Add(firstId);
        }
    }

    private static void Chip(in AppletFrame frame, Rect area, string label, bool on, Action pick)
    {
        frame.Paint.Fill(area, on ? PhotosChrome.Accent : PhotosChrome.Tile, frame.Units(12f));
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, on ? PhotosChrome.AccentInk : PhotosChrome.Mute, TextAlign.Center));
        if (frame.Input.ConsumeClick(area))
        {
            pick();
        }
    }

    private void DrawCamera(in AppletFrame frame)
    {
        var stack = new Stack(frame.Content.Inset(frame.Units(14f)), StackAxis.Vertical, frame.Units(10f));
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Camera",
            new TextStyle(FontRole.Title, PhotosChrome.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Hold the pearl up. The still is a note, not a screenshot.",
            new TextStyle(FontRole.Caption, PhotosChrome.Mute));

        var finder = stack.Take(frame.Units(160f));
        CardChrome.DrawGold(frame, finder);
        var inset = finder.Inset(frame.Units(14f));
        var zone = game.IsLoggedIn && game.ZoneName.Length > 0 ? game.ZoneName : "No view";
        var job = game.JobName.Length > 0 ? game.JobName : "—";
        frame.Text.DrawIn(inset.TopSlice(frame.Units(22f)), zone,
            new TextStyle(FontRole.Title, PhotosChrome.Ink, TextAlign.Center));
        frame.Text.DrawIn(inset.Inset(new Edges(0f, frame.Units(28f), 0f, frame.Units(36f))), job,
            new TextStyle(FontRole.Body, PhotosChrome.Mute, TextAlign.Center));
        frame.Paint.StrokeCircle(finder.Center + new Vector2(0f, frame.Units(18f)), frame.Units(28f),
            frame.Theme.Palette.WarmAccent with { W = 0.55f }, frame.Units(2f));

        var shutter = stack.Take(frame.Units(48f));
        frame.Paint.Fill(shutter, frame.Theme.Palette.Accent, shutter.Height * 0.5f);
        frame.Text.DrawIn(shutter, "Shutter",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
        if (frame.Input.ConsumeClick(shutter))
        {
            lastStill = zone + " · " + job + " · " + clock.Now.ToString("HH:mm", CultureInfo.CurrentCulture);
        }

        var still = stack.Take(frame.Units(56f));
        CardChrome.Draw(frame, still);
        frame.Text.DrawWrapped(still.Inset(frame.Units(12f)),
            lastStill.Length > 0 ? lastStill : "No still yet.",
            new TextStyle(FontRole.Caption, PhotosChrome.Mute));
    }

    private enum Pane : byte
    {
        Camera = 0,
        Gallery = 1,
    }

    private enum Mode : byte
    {
        Page = 0,
        Viewer = 1,
        Crop = 2,
        Move = 3,
        MakeFolder = 4,
    }

    private enum CropDrag : byte
    {
        None = 0,
        Move = 1,
        NorthWest = 2,
        NorthEast = 3,
        SouthWest = 4,
        SouthEast = 5,
    }
}
