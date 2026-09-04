using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Music;

internal static class MusicChrome
{
    public static readonly Vector4 Bg = new(0.039f, 0.039f, 0.059f, 1f);
    public static readonly Vector4 Card = new(0.071f, 0.071f, 0.094f, 1f);
    public static readonly Vector4 CardHi = new(0.102f, 0.102f, 0.133f, 1f);
    public static readonly Vector4 Purple = new(0.659f, 0.333f, 0.969f, 1f);
    public static readonly Vector4 PurpleDim = new(0.659f, 0.333f, 0.969f, 0.18f);
    public static readonly Vector4 Live = new(0.133f, 0.773f, 0.369f, 1f);
    public static readonly Vector4 Ink = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 Mute = new(0.63f, 0.64f, 0.70f, 1f);
    public static readonly Vector4 Faint = new(1f, 1f, 1f, 0.10f);

    public static void Plate(in AppletFrame frame, Rect area, float radius)
    {
        frame.Paint.Fill(area, Card, radius);
        frame.Paint.Stroke(area, Faint, frame.Units(1f), radius);
    }

    public static void GlowPlate(in AppletFrame frame, Rect area, float radius, bool on)
    {
        frame.Paint.Fill(area, on ? PurpleDim : Card, radius);
        frame.Paint.Stroke(area, on ? Purple : Faint, frame.Units(1.2f), radius);
    }

    public static void Primary(in AppletFrame frame, Rect area, string label)
    {
        frame.Paint.Fill(area, Purple, frame.Units(12f));
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.BodyStrong, Ink, TextAlign.Center));
    }

    public static void LiveMark(in AppletFrame frame, Rect area)
    {
        frame.Paint.Fill(area, Live with { W = 0.22f }, frame.Units(8f));
        frame.Text.DrawIn(area, "LIVE", new TextStyle(FontRole.CaptionStrong, Live, TextAlign.Center));
    }

    public static void Meter(in AppletFrame frame, Rect area, float level)
    {
        Plate(frame, area, frame.Units(8f));
        var fill = Math.Clamp(level, 0f, 1f);
        if (fill <= 0.01f)
        {
            return;
        }

        var bar = area.Inset(frame.Units(4f));
        var hot = Rect.FromSize(bar.Min, new Vector2(bar.Width * fill, bar.Height));
        frame.Paint.Fill(hot, fill > 0.08f ? Live : Mute, frame.Units(6f));
    }

    public static void OffMark(in AppletFrame frame, Rect area)
    {
        frame.Paint.Fill(area, Faint, frame.Units(8f));
        frame.Text.DrawIn(area, "OFFLINE", new TextStyle(FontRole.CaptionStrong, Mute, TextAlign.Center));
    }

    public static bool Chip(in AppletFrame frame, Rect area, string label, bool on)
    {
        frame.Paint.Fill(area, on ? Purple : CardHi, frame.Units(12f));
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, on ? Ink : Mute, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static bool Row(in AppletFrame frame, Rect area, string title, string detail, bool live)
    {
        Plate(frame, area, frame.Units(12f));
        var inset = area.Inset(frame.Units(10f));
        var thumb = inset.LeftSlice(frame.Units(36f));
        frame.Paint.Fill(thumb, PurpleDim, frame.Units(8f));
        var body = inset.Inset(new Edges(frame.Units(44f), 0f, live ? frame.Units(44f) : 0f, 0f));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), title, new TextStyle(FontRole.BodyStrong, Ink));
        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(16f)), detail, new TextStyle(FontRole.Caption, Mute));
        if (live)
        {
            LiveMark(frame, inset.RightSlice(frame.Units(40f)).TopSlice(frame.Units(18f)));
        }

        return frame.Input.ConsumeClick(area);
    }

    public static void Kicker(in AppletFrame frame, Rect area, string label)
    {
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.CaptionStrong, Purple));
    }

    public static void Title(in AppletFrame frame, Rect area, string label)
    {
        frame.Text.DrawEllipsized(area, label, new TextStyle(FontRole.Title, Ink));
    }

    public static bool Back(in AppletFrame frame, Rect row, string label)
    {
        var hit = row.LeftSlice(frame.Units(28f));
        frame.Text.DrawIn(hit, "‹", new TextStyle(FontRole.Title, Purple, TextAlign.Center));
        frame.Text.DrawEllipsized(row.Inset(new Edges(frame.Units(28f), 0f, 0f, 0f)), label,
            new TextStyle(FontRole.BodyStrong, Ink));
        return frame.Input.ConsumeClick(row.LeftSlice(frame.Units(90f)));
    }

    public static Stack BeginSheet(in AppletFrame frame, Rect view, MusicState state, float gap)
    {
        var content = MathF.Max(view.Height + frame.Units(8f), state.SheetHeight);
        Wheel(frame, view, state, content);
        var sheet = Rect.FromSize(new Vector2(view.Min.X, view.Min.Y - state.Scroll),
            new Vector2(view.Width, content));
        frame.Paint.PushClip(view);
        return new Stack(sheet, StackAxis.Vertical, gap);
    }

    public static void EndSheet(in AppletFrame frame, Rect view, MusicState state, ref Stack stack)
    {
        var used = stack.Remaining.Min.Y - (view.Min.Y - state.Scroll);
        state.SheetHeight = MathF.Max(used + frame.Units(16f), view.Height);
        frame.Paint.PopClip();
    }

    public static void Wheel(in AppletFrame frame, Rect area, MusicState state, float content)
    {
        var max = MathF.Max(0f, content - area.Height);
        if (!frame.Input.IsHovering(area) && !(frame.Input.IsHeld() && area.Contains(frame.Input.Pointer)))
        {
            state.Scroll = Math.Clamp(state.Scroll, 0f, max);
            return;
        }

        if (frame.Input.ScrollDelta != 0f)
        {
            state.Scroll -= frame.Input.ScrollDelta * frame.Units(28f);
        }

        if (frame.Input.IsHeld() && MathF.Abs(frame.Input.PointerDelta.Y) > frame.Units(22f))
        {
            state.Scroll -= frame.Input.PointerDelta.Y;
        }

        state.Scroll = Math.Clamp(state.Scroll, 0f, max);
    }

    public static void Rail(in AppletFrame frame, Rect track, float offset, float content, float view)
    {
        if (content <= view + 1f || track.Height < 8f)
        {
            return;
        }

        frame.Paint.Fill(track, Faint, frame.Units(3f));
        var thumbH = MathF.Max(frame.Units(18f), track.Height * (view / content));
        var travel = MathF.Max(0f, track.Height - thumbH);
        var y = track.Min.Y + travel * (offset / MathF.Max(content - view, 1f));
        frame.Paint.Fill(Rect.FromSize(new Vector2(track.Min.X, y), new Vector2(track.Width, thumbH)), Purple,
            frame.Units(3f));
    }
}
