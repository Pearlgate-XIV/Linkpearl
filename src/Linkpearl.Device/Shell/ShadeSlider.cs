using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

internal static class ShadeSlider
{
    public static bool Draw(IPaintSurface paint, IInputProbe input, ITheme theme, Rect row, float value,
        ref bool dragging, Action<float> set, bool sun)
    {
        var gold = theme.Palette.WarmAccent;
        var mark = row.Height * 0.72f;
        var left = row.LeftSlice(mark);
        var right = row.RightSlice(mark);
        if (sun)
        {
            DrawSun(paint, left, gold with { W = 0.55f });
            DrawSun(paint, right, gold);
        }
        else
        {
            DrawSpeaker(paint, left, gold with { W = 0.55f }, false);
            DrawSpeaker(paint, right, gold, true);
        }

        var track = row.Inset(new Edges(mark + row.Height * 0.2f, row.Height * 0.38f, mark + row.Height * 0.2f,
            row.Height * 0.38f));
        DrawTrack(paint, theme, track, value, horizontal: true);
        return Slide(input, row, track, value, ref dragging, set, horizontal: true);
    }

    public static bool DrawVertical(IPaintSurface paint, IInputProbe input, ITheme theme, Rect row, float value,
        ref bool dragging, Action<float> set)
    {
        var gold = theme.Palette.WarmAccent;
        paint.Fill(row, theme.Palette.SurfaceRaised with { W = 0.94f }, row.Width * 0.28f);
        paint.Stroke(row, gold with { W = 0.28f }, MathF.Max(1f, row.Width * 0.04f), row.Width * 0.28f);

        var mark = MathF.Min(row.Width * 0.72f, row.Height * 0.18f);
        var top = row.TopSlice(mark);
        var bottom = row.BottomSlice(mark);
        DrawSpeaker(paint, top, gold, true);
        DrawSpeaker(paint, bottom, gold with { W = 0.55f }, false);

        var track = row.Inset(new Edges(row.Width * 0.38f, mark + row.Width * 0.18f, row.Width * 0.38f,
            mark + row.Width * 0.18f));
        DrawTrack(paint, theme, track, value, horizontal: false);
        return Slide(input, row, track, value, ref dragging, set, horizontal: false);
    }

    private static void DrawTrack(IPaintSurface paint, ITheme theme, Rect track, float value, bool horizontal)
    {
        var gold = theme.Palette.WarmAccent;
        var fillAmount = Math.Clamp(value, 0f, 1f);
        paint.Fill(track, theme.Palette.SurfaceOverlay, MathF.Min(track.Width, track.Height) * 0.5f);
        if (horizontal)
        {
            paint.Fill(track.LeftSlice(MathF.Max(track.Height, track.Width * fillAmount)), gold with { W = 0.88f },
                track.Height * 0.5f);
            var thumb = new Vector2(track.Min.X + track.Width * fillAmount, track.Center.Y);
            paint.FillCircle(thumb, track.Height * 0.95f, gold);
            paint.StrokeCircle(thumb, track.Height * 0.95f, gold with { W = 0.95f },
                MathF.Max(1f, track.Height * 0.12f));
            return;
        }

        var filled = track.Height * fillAmount;
        paint.Fill(new Rect(new Vector2(track.Min.X, track.Max.Y - filled), track.Max), gold with { W = 0.88f },
            track.Width * 0.5f);
        var knob = new Vector2(track.Center.X, track.Max.Y - track.Height * fillAmount);
        paint.FillCircle(knob, track.Width * 0.95f, gold);
        paint.StrokeCircle(knob, track.Width * 0.95f, gold with { W = 0.95f }, MathF.Max(1f, track.Width * 0.12f));
    }

    private static bool Slide(IInputProbe input, Rect row, Rect track, float value, ref bool dragging,
        Action<float> set, bool horizontal)
    {
        if (input.WasPressed(row) || (input.IsHeld() && row.Contains(input.Pointer) && !dragging))
        {
            dragging = true;
        }

        if (dragging && input.IsHeld())
        {
            var next = horizontal
                ? (input.Pointer.X - track.Min.X) / MathF.Max(track.Width, 1f)
                : (track.Max.Y - input.Pointer.Y) / MathF.Max(track.Height, 1f);
            next = Math.Clamp(next, 0f, 1f);
            if (MathF.Abs(next - value) > 0.0001f)
            {
                set(next);
            }

            input.Claim(row);
            input.ConsumeClick(row);
            return true;
        }

        dragging = false;
        return input.ConsumeClick(row);
    }

    private static void DrawSun(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.22f;
        paint.FillCircle(c, s, ink);
        var ray = s * 1.55f;
        var stroke = MathF.Max(1.1f, s * 0.28f);
        paint.Line(c + new Vector2(0f, -ray), c + new Vector2(0f, -s * 1.15f), ink, stroke);
        paint.Line(c + new Vector2(0f, ray), c + new Vector2(0f, s * 1.15f), ink, stroke);
        paint.Line(c + new Vector2(-ray, 0f), c + new Vector2(-s * 1.15f, 0f), ink, stroke);
        paint.Line(c + new Vector2(ray, 0f), c + new Vector2(s * 1.15f, 0f), ink, stroke);
    }

    private static void DrawSpeaker(IPaintSurface paint, Rect area, Vector4 ink, bool waves)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.28f;
        var stroke = MathF.Max(1.1f, s * 0.22f);
        paint.Fill(Rect.FromSize(c + new Vector2(-s * 0.72f, -s * 0.28f), new Vector2(s * 0.42f, s * 0.56f)), ink,
            s * 0.08f);
        paint.Line(c + new Vector2(-s * 0.30f, -s * 0.28f), c + new Vector2(s * 0.18f, -s * 0.72f), ink, stroke);
        paint.Line(c + new Vector2(-s * 0.30f, s * 0.28f), c + new Vector2(s * 0.18f, s * 0.72f), ink, stroke);
        if (waves)
        {
            paint.StrokeCircle(c + new Vector2(s * 0.12f, 0f), s * 0.42f, ink, stroke);
            paint.StrokeCircle(c + new Vector2(s * 0.12f, 0f), s * 0.68f, ink with { W = ink.W * 0.7f }, stroke);
        }
    }
}
