using Linkpearl.Geometry;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Camera;

public sealed partial class CameraApplet : IApplet, IDisposable
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
    private bool uploadSheet;
    private string uploadNote = string.Empty;
    private bool gposeWait;
    private bool storeWait;

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
        uploadSheet = false;
        uploadNote = string.Empty;
        display.Landscape = false;
        library.Save();
    }

    public void Dispose() => library.Save();

    public bool CanGoBack => picking || confirmBulkRemove || uploadSheet || mode != Mode.Page || pane != Pane.Camera ||
        place.Length > 0;

    public bool Back()
    {
        if (menuShotId.Length > 0)
        {
            menuShotId = string.Empty;
            return true;
        }

        if (uploadSheet)
        {
            uploadSheet = false;
            uploadNote = string.Empty;
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
        FinishStorage();
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
        var body = frame.Content.Inset(frame.Units(18f));
        if (body.IsEmpty)
        {
            return;
        }

        var live = game.IsInGpose;
        var gold = frame.Theme.Palette.WarmAccent;
        var span = MathF.Min(body.Width, body.Height);
        var diameter = Math.Clamp(span * 0.58f, frame.Units(132f), frame.Units(188f));
        var pulse = live
            ? 0.5f + 0.5f * MathF.Sin(clock.UtcNow.ToUnixTimeMilliseconds() * 0.0024f)
            : 0f;
        var grow = live ? 1f + pulse * 0.03f : 1f;
        var center = new Vector2(body.Center.X, body.Min.Y + body.Height * 0.42f);
        var radius = diameter * 0.5f * grow;

        var halo = live ? gold with { W = 0.18f + pulse * 0.16f } : PhotosChrome.Tile with { W = 0.92f };
        frame.Paint.FillCircle(center, radius + frame.Units(14f), halo);
        frame.Paint.FillCircle(center, radius, new Vector4(0.10f, 0.10f, 0.11f, 1f));
        frame.Paint.StrokeCircle(center, radius, gold with { W = live ? 0.95f : 0.78f }, frame.Units(3.2f));
        frame.Paint.StrokeCircle(center, radius * 0.78f, gold with { W = 0.35f }, frame.Units(1.4f));
        var well = live ? gold with { W = 0.22f + pulse * 0.12f } : new Vector4(0.07f, 0.07f, 0.08f, 1f);
        frame.Paint.FillCircle(center, radius * 0.58f, well);
        frame.Paint.StrokeCircle(center, radius * 0.58f, PhotosChrome.Ink with { W = 0.22f }, frame.Units(1.2f));
        frame.Paint.FillCircle(center, radius * 0.16f, live ? gold : PhotosChrome.Accent);
        frame.Paint.StrokeCircle(center, radius * 0.16f, PhotosChrome.AccentInk with { W = 0.55f },
            frame.Units(1.1f));

        var label = Rect.FromSize(
            new Vector2(body.Min.X, center.Y + radius + frame.Units(18f)),
            new Vector2(body.Width, frame.Units(28f)));
        frame.Text.DrawIn(label, live ? "In GPose" : "Open GPose",
            new TextStyle(FontRole.Title, live ? gold : PhotosChrome.Ink, TextAlign.Center));

        var hit = new Rect(
            new Vector2(center.X - radius, center.Y - radius),
            new Vector2(center.X + radius, label.Max.Y));
        if (frame.Input.IsHovering(hit) && !live)
        {
            frame.Paint.StrokeCircle(center, radius + frame.Units(4f), gold with { W = 0.45f }, frame.Units(1.6f));
        }

        if (frame.Input.ConsumeClick(hit) && !live)
        {
            game.OpenGroupPose();
            game.CuePocket();
        }
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
