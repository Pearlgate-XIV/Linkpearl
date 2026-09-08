using System.IO;
using Linkpearl.Applets;
using Linkpearl.Emoji;
using Linkpearl.Geometry;
using Linkpearl.Media;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Chat;

public enum ChatTrayPane : byte
{
    Closed = 0,
    Attach = 1,
    Faces = 2,
}

public sealed class ChatTray
{
    public ChatTrayPane Pane { get; private set; }

    private readonly EmojiPick faces = new();
    private bool albumOpen;
    private bool albumLock;

    public float SheetHeight(in AppletFrame frame)
    {
        if (Pane == ChatTrayPane.Closed)
        {
            return 0f;
        }

        if (Pane == ChatTrayPane.Attach)
        {
            return albumOpen ? frame.Units(196f) : frame.Units(96f);
        }

        return frame.Units(248f);
    }

    public void Close()
    {
        Pane = ChatTrayPane.Closed;
        albumOpen = false;
        albumLock = false;
    }

    public void Toggle(ChatTrayPane pane)
    {
        if (Pane == pane)
        {
            Close();
            return;
        }

        Pane = pane;
        albumOpen = false;
        albumLock = false;
        if (pane == ChatTrayPane.Faces)
        {
            faces.Open();
        }
    }

    public void TickFiles(IFilePicker? files, Action<string> send)
    {
        if (files is null || !files.TryTakeImages(out var picked) || picked.Count == 0)
        {
            return;
        }

        var path = picked[0];
        if (path.Length > 0)
        {
            send(ChatBits.Pic(path));
            Close();
        }
    }

    public bool DrawPlus(in AppletFrame frame, Rect area, Vector4 idle)
    {
        var on = Pane == ChatTrayPane.Attach;
        var mark = Mark(frame, area, idle, on);
        var c = area.Center;
        var s = frame.Units(7f);
        var stroke = frame.Units(1.6f);
        frame.Paint.Line(c + new Vector2(-s, 0f), c + new Vector2(s, 0f), mark, stroke);
        frame.Paint.Line(c + new Vector2(0f, -s), c + new Vector2(0f, s), mark, stroke);
        if (on)
        {
            frame.Paint.Fill(area.BottomSlice(frame.Units(2f)).Inset(new Edges(frame.Units(8f), 0f)), mark,
                frame.Units(1f));
        }

        if (!frame.Input.ConsumeClick(area))
        {
            return false;
        }

        Toggle(ChatTrayPane.Attach);
        return true;
    }

    public bool DrawPlace(in AppletFrame frame, Rect area, Vector4 idle)
    {
        var ink = Mark(frame, area, idle, false);
        var stroke = frame.Units(1.6f);
        var mid = area.Center;
        var head = mid + new Vector2(0f, -frame.Units(2f));
        var tip = mid + new Vector2(0f, frame.Units(7f));
        frame.Paint.StrokeCircle(head, frame.Units(5.5f), ink, stroke);
        frame.Paint.Line(head + new Vector2(-frame.Units(3.2f), frame.Units(4.2f)), tip, ink, stroke);
        frame.Paint.Line(head + new Vector2(frame.Units(3.2f), frame.Units(4.2f)), tip, ink, stroke);
        return frame.Input.ConsumeClick(area);
    }

    public bool DrawFaces(in AppletFrame frame, Rect area, Vector4 idle)
    {
        var on = Pane == ChatTrayPane.Faces;
        var ink = Mark(frame, area, idle, on);
        EmojiArt.DrawSmile(frame, area.Inset(frame.Units(1f)), ink);
        if (on)
        {
            frame.Paint.Fill(area.BottomSlice(frame.Units(2f)).Inset(new Edges(frame.Units(8f), 0f)), ink,
                frame.Units(1f));
        }

        if (!frame.Input.ConsumeClick(area))
        {
            return false;
        }

        Toggle(ChatTrayPane.Faces);
        return true;
    }

    public void DrawSheet(in AppletFrame frame, Rect area, Vector4 ink, Vector4 mute, Vector4 accent, Vector4 card,
        IFilePicker? files, string galleryFolder, Action<string> insertEmote, Action<string> send,
        Action? openGallery = null)
    {
        _ = galleryFolder;
        if (Pane == ChatTrayPane.Closed || area.Height <= 0f)
        {
            return;
        }

        frame.Paint.Fill(area, card, frame.Units(16f));
        if (Pane == ChatTrayPane.Attach)
        {
            DrawAttach(frame, area.Inset(frame.Units(10f)), ink, mute, files, send, openGallery);
        }
        else
        {
            faces.Draw(frame, area.Inset(frame.Units(8f)), ink, mute, accent, card, insertEmote);
        }

        frame.Input.Claim(area);
    }

    private void DrawAttach(in AppletFrame frame, Rect area, Vector4 ink, Vector4 mute, IFilePicker? files,
        Action<string> send, Action? openGallery)
    {
        if (albumOpen)
        {
            DrawAlbum(frame, area, ink, mute, send);
            return;
        }

        var rowH = frame.Units(36f);
        var gap = frame.Units(6f);
        var gallery = area.TopSlice(rowH);
        var pc = Rect.FromSize(new Vector2(area.Min.X, gallery.Max.Y + gap), new Vector2(area.Width, rowH));
        DrawAttachRow(frame, gallery, mute, ink, "Gallery", DrawGalleryMark);
        DrawAttachRow(frame, pc, mute, ink, "This PC", DrawPcMark);
        if (frame.Input.ConsumeClick(gallery))
        {
            if (openGallery is not null)
            {
                openGallery();
            }
            else
            {
                albumOpen = true;
                albumLock = true;
            }
        }

        if (files is not null && frame.Input.ConsumeClick(pc))
        {
            files.BeginImagePick();
        }
    }

    private void DrawAlbum(in AppletFrame frame, Rect area, Vector4 ink, Vector4 mute, Action<string> send)
    {
        var head = area.TopSlice(frame.Units(22f));
        var back = head.LeftSlice(frame.Units(22f));
        DrawBack(frame.Paint, back.Center, frame.Units(5.5f), mute);
        frame.Text.DrawIn(head.Inset(new Edges(frame.Units(26f), 0f, 0f, 0f)), "Camera gallery",
            new TextStyle(FontRole.CaptionStrong, ink, TextAlign.Left));
        if (frame.Input.ConsumeClick(head))
        {
            albumOpen = false;
            return;
        }

        var pick = !albumLock;
        if (albumLock && !frame.Input.IsHeld())
        {
            albumLock = false;
        }

        var shots = GalleryFiles.List(frame.Paths);
        var grid = area.Inset(new Edges(0f, frame.Units(28f), 0f, 0f));
        if (shots.Count == 0)
        {
            frame.Text.DrawIn(grid, "No photos in Camera yet.",
                new TextStyle(FontRole.Caption, mute, TextAlign.Center));
            return;
        }

        var gap = frame.Units(5f);
        var cols = 3;
        var cell = (grid.Width - gap * (cols - 1)) / cols;
        var rows = Math.Max(1, (int)(grid.Height / (cell + gap)));
        var shown = Math.Min(shots.Count, cols * rows);
        for (var index = 0; index < shown; index++)
        {
            var col = index % cols;
            var row = index / cols;
            var dest = Rect.FromSize(
                new Vector2(grid.Min.X + (cell + gap) * col, grid.Min.Y + (cell + gap) * row),
                new Vector2(cell, cell));
            var path = shots[index].Path;
            var texture = frame.Textures.FromFile(path);
            if (texture is { IsReady: true })
            {
                var uv = CoverFit.Uv(texture.Size, dest.Size);
                frame.Paint.ImageRounded(texture, dest, uv.Min, uv.Max, Vector4.One, frame.Units(8f));
            }
            else
            {
                frame.Paint.Fill(dest, mute with { W = 0.14f }, frame.Units(8f));
            }

            if (pick && frame.Input.ConsumeClick(dest))
            {
                send(ChatBits.Pic(path));
                Close();
            }
        }
    }

    private static void DrawAttachRow(in AppletFrame frame, Rect area, Vector4 mute, Vector4 ink, string title,
        Action<IPaintSurface, Rect, Vector4> mark)
    {
        var hot = frame.Input.IsHovering(area);
        var wash = hot ? Vector4.One : mute;
        frame.Paint.Fill(area, mute with { W = hot ? 0.18f : 0.10f }, area.Height * 0.5f);
        var icon = area.LeftSlice(area.Height).Inset(frame.Units(8f));
        mark(frame.Paint, icon, wash);
        frame.Text.DrawIn(area.Inset(new Edges(area.Height, 0f, frame.Units(12f), 0f)), title,
            new TextStyle(FontRole.CaptionStrong, wash, TextAlign.Left));
    }

    private static void DrawGalleryMark(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var stroke = MathF.Max(1.4f, area.Width * 0.10f);
        paint.Stroke(area.Inset(area.Width * 0.06f), ink, stroke, area.Width * 0.18f);
        var mid = area.Center + new Vector2(0f, area.Height * 0.10f);
        paint.Line(area.Min + new Vector2(area.Width * 0.18f, area.Height * 0.72f),
            mid + new Vector2(-area.Width * 0.08f, -area.Height * 0.10f), ink, stroke);
        paint.Line(mid + new Vector2(-area.Width * 0.08f, -area.Height * 0.10f),
            mid + new Vector2(area.Width * 0.22f, area.Height * 0.16f), ink, stroke);
        paint.StrokeCircle(area.Min + new Vector2(area.Width * 0.68f, area.Height * 0.32f), area.Width * 0.08f, ink,
            stroke);
    }

    private static void DrawPcMark(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var stroke = MathF.Max(1.4f, area.Width * 0.10f);
        var screen = area.Inset(new Edges(area.Width * 0.08f, area.Height * 0.12f, area.Width * 0.08f,
            area.Height * 0.36f));
        paint.Stroke(screen, ink, stroke, screen.Height * 0.16f);
        var stand = area.Center.X;
        paint.Line(new Vector2(stand, screen.Max.Y + stroke),
            new Vector2(stand, area.Max.Y - area.Height * 0.16f), ink, stroke);
        paint.Line(new Vector2(area.Min.X + area.Width * 0.28f, area.Max.Y - area.Height * 0.16f),
            new Vector2(area.Max.X - area.Width * 0.28f, area.Max.Y - area.Height * 0.16f), ink, stroke);
    }

    private static void DrawBack(IPaintSurface paint, Vector2 center, float size, Vector4 ink)
    {
        var stroke = size * 0.28f;
        paint.Line(center + new Vector2(size * 0.35f, -size), center + new Vector2(-size * 0.55f, 0f), ink, stroke);
        paint.Line(center + new Vector2(-size * 0.55f, 0f), center + new Vector2(size * 0.35f, size), ink, stroke);
    }

    private static Vector4 Mark(in AppletFrame frame, Rect area, Vector4 idle, bool on) =>
        on || frame.Input.IsHovering(area) ? Vector4.One : idle;
}
