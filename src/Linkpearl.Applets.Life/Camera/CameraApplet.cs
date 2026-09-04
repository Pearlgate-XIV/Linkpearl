using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Camera;

public sealed class CameraApplet : IApplet
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

    public CameraApplet(IClock clock, IGameSession game, HostPaths paths, ITextureSource textures, IFilePicker files,
        DisplayPreferences display)
    {
        this.clock = clock;
        this.game = game;
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
        display.Landscape = false;
        library.Save();
    }

    public bool CanGoBack => mode == Mode.Viewer || pane != Pane.Camera;

    public bool Back()
    {
        if (mode == Mode.Viewer)
        {
            mode = Mode.Page;
            viewingId = string.Empty;
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
        PhotosChrome.Fill(frame);
        if (mode == Mode.Viewer)
        {
            DrawViewer(frame);
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
        var camera = row.LeftSlice(cell);
        var gallery = row.RightSlice(cell);
        Chip(frame, camera, "Camera", pane == Pane.Camera, () => pane = Pane.Camera);
        Chip(frame, gallery, "Gallery", pane == Pane.Gallery, () =>
        {
            pane = Pane.Gallery;
            scroll = 0f;
        });
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

    private void DrawGallery(in AppletFrame frame)
    {
        var upload = frame.Content.TopSlice(frame.Units(36f)).RightSlice(frame.Units(78f)).Inset(
            new Edges(0f, frame.Units(4f), frame.Units(8f), frame.Units(4f)));
        var head = frame.Content.TopSlice(frame.Units(36f)).Inset(new Edges(frame.Units(12f), frame.Units(6f),
            frame.Units(90f), 0f));
        frame.Text.DrawIn(head, "Gallery", new TextStyle(FontRole.Title, PhotosChrome.Ink));
        frame.Paint.Fill(upload, PhotosChrome.Accent, frame.Units(12f));
        frame.Text.DrawIn(upload, "Upload",
            new TextStyle(FontRole.CaptionStrong, PhotosChrome.AccentInk, TextAlign.Center));
        if (frame.Input.ConsumeClick(upload))
        {
            var picked = files.PickImageFiles();
            if (picked.Count > 0)
            {
                library.Import(picked, clock.Now);
                RebuildAlbums();
                scroll = 0f;
            }

            return;
        }

        var body = frame.Content.Inset(new Edges(frame.Units(8f), frame.Units(40f), frame.Units(8f), frame.Units(8f)));
        var content = DrawAlbums(frame, body);
        PhotosChrome.Wheel(frame, body, ref scroll, content);
    }

    private float DrawAlbums(in AppletFrame frame, Rect viewport)
    {
        frame.Paint.PushClip(viewport);
        if (albums.Count == 0)
        {
            frame.Text.DrawIn(viewport.TopSlice(frame.Units(28f)), "No photos yet",
                new TextStyle(FontRole.BodyStrong, PhotosChrome.Ink, TextAlign.Center));
            frame.Text.DrawWrapped(viewport.Inset(new Edges(frame.Units(16f), frame.Units(40f), frame.Units(16f), 0f)),
                "Tap Upload to open a Windows file window. Pictures go into an album named for today’s date.",
                new TextStyle(FontRole.Caption, PhotosChrome.Mute, TextAlign.Center));
            frame.Paint.PopClip();
            return viewport.Height;
        }

        var columns = 3;
        var gap = frame.Units(3f);
        var cell = (viewport.Width - gap * (columns - 1)) / columns;
        var cursor = viewport.Min.Y - scroll;
        var total = 0f;

        for (var album = 0; album < albums.Count; album++)
        {
            var group = albums[album];
            var header = Rect.FromSize(new Vector2(viewport.Min.X, cursor),
                new Vector2(viewport.Width, frame.Units(28f)));
            if (header.Overlaps(viewport))
            {
                frame.Text.DrawIn(header, group.Title,
                    new TextStyle(FontRole.BodyStrong, PhotosChrome.Ink));
                frame.Text.DrawIn(header.RightSlice(frame.Units(48f)),
                    group.Items.Count.ToString(CultureInfo.CurrentCulture),
                    new TextStyle(FontRole.Caption, PhotosChrome.Mute, TextAlign.Right));
            }

            cursor += frame.Units(30f);
            total += frame.Units(30f);
            var rowsNeeded = (group.Items.Count + columns - 1) / columns;
            var gridArea = Rect.FromSize(new Vector2(viewport.Min.X, cursor),
                new Vector2(viewport.Width, rowsNeeded * (cell + gap) - gap));
            var grid = new TileGrid(gridArea, columns, Math.Max(rowsNeeded, 1), gap);
            for (var item = 0; item < group.Items.Count; item++)
            {
                var tile = grid.CellAt(item);
                if (tile.Overlaps(viewport))
                {
                    DrawThumb(frame, tile, group.Items[item]);
                }
            }

            var block = rowsNeeded * (cell + gap) - gap + frame.Units(12f);
            cursor += block;
            total += block;
        }

        frame.Paint.PopClip();
        return MathF.Max(total, viewport.Height);
    }

    private void DrawThumb(in AppletFrame frame, Rect tile, PhotoShot shot)
    {
        var path = library.Absolute(shot);
        var texture = textures.FromFile(path);
        if (texture is not null && texture.IsReady)
        {
            frame.Paint.PushClip(tile);
            PhotosChrome.Cover(frame, tile, texture);
            frame.Paint.PopClip();
        }
        else
        {
            PhotosChrome.Placeholder(frame, tile);
        }

        if (frame.Input.ConsumeClick(tile))
        {
            viewingId = shot.Id;
            mode = Mode.Viewer;
        }
    }

    private void DrawViewer(in AppletFrame frame)
    {
        PhotoShot? shot = null;
        var shots = library.Shots;
        for (var index = 0; index < shots.Count; index++)
        {
            if (string.Equals(shots[index].Id, viewingId, StringComparison.Ordinal))
            {
                shot = shots[index];
                break;
            }
        }

        if (shot is null)
        {
            mode = Mode.Page;
            viewingId = string.Empty;
            return;
        }

        var bar = frame.Content.TopSlice(frame.Units(40f));
        frame.Text.DrawIn(bar.LeftSlice(frame.Units(28f)), "‹",
            new TextStyle(FontRole.Title, PhotosChrome.Accent, TextAlign.Center));
        frame.Text.DrawEllipsized(bar.Inset(new Edges(frame.Units(32f), frame.Units(8f), frame.Units(8f), 0f)),
            AlbumTitle(shot.Album) + "  ·  " + shot.Title,
            new TextStyle(FontRole.CaptionStrong, PhotosChrome.Ink));
        if (frame.Input.ConsumeClick(bar))
        {
            mode = Mode.Page;
            pane = Pane.Gallery;
            viewingId = string.Empty;
            return;
        }

        var stage = frame.Content.Inset(new Edges(frame.Units(8f), frame.Units(44f), frame.Units(8f), frame.Units(8f)));
        var texture = textures.FromFile(library.Absolute(shot));
        if (texture is not null && texture.IsReady)
        {
            PhotosChrome.Contain(frame, stage, texture);
        }
        else
        {
            PhotosChrome.Placeholder(frame, stage);
        }

        LandscapeHold.Draw(frame, display);
        if (frame.Input.ConsumeClick(stage))
        {
            mode = Mode.Page;
            pane = Pane.Gallery;
            viewingId = string.Empty;
        }
    }

    private void RebuildAlbums()
    {
        albums.Clear();
        Dictionary<string, List<PhotoShot>> map = new(StringComparer.Ordinal);
        var shots = library.Shots;
        for (var index = 0; index < shots.Count; index++)
        {
            var shot = shots[index];
            if (!map.TryGetValue(shot.Album, out var list))
            {
                list = new List<PhotoShot>();
                map[shot.Album] = list;
            }

            list.Add(shot);
        }

        var keys = new List<string>(map.Keys);
        keys.Sort(static (left, right) => string.CompareOrdinal(right, left));
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            albums.Add((key, AlbumTitle(key), map[key]));
        }
    }

    private string AlbumTitle(string key)
    {
        if (!DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var day))
        {
            return key;
        }

        var today = DateOnly.FromDateTime(clock.Now.LocalDateTime);
        var album = DateOnly.FromDateTime(day);
        if (album == today)
        {
            return "Today";
        }

        if (album == today.AddDays(-1))
        {
            return "Yesterday";
        }

        return day.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture);
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
    }
}
