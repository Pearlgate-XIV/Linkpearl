using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

internal enum ShadeMark : byte
{
    Sun = 0,
    Moon = 1,
    Note = 2,
    Speaker = 3,
}

internal static class ShadeSlider
{
    private static readonly Vector4 Track = new(0.16f, 0.16f, 0.18f, 0.90f);
    private static readonly Vector4 Fill = new(0.97f, 0.97f, 0.99f, 0.98f);
    private static readonly Vector4 Knob = new(0.86f, 0.86f, 0.89f, 1f);
    private static readonly Vector4 MarkOn = new(0.14f, 0.14f, 0.16f, 1f);
    private static readonly Vector4 MarkOff = new(0.94f, 0.94f, 0.96f, 1f);

    public static bool Draw(IPaintSurface paint, IInputProbe input, ITheme theme, Rect row, float value,
        ref bool dragging, Action<float> set, bool sun, ITextureHandle? leftIcon = null,
        ITextureHandle? rightIcon = null) =>
        DrawPanel(paint, input, row, value, ref dragging, set, sun ? ShadeMark.Sun : ShadeMark.Note,
            sun ? ShadeMark.Moon : ShadeMark.Speaker, leftIcon, rightIcon);

    public static bool DrawPanel(IPaintSurface paint, IInputProbe input, Rect row, float value,
        ref bool dragging, Action<float> set, ShadeMark left, ShadeMark right, ITextureHandle? leftIcon = null,
        ITextureHandle? rightIcon = null)
    {
        var amount = Math.Clamp(value, 0f, 1f);
        var radius = row.Height * 0.5f;
        paint.Fill(row, Track, radius);
        var filled = MathF.Max(row.Height, row.Width * amount);
        var lit = row.LeftSlice(filled);
        paint.Fill(lit, Fill, radius);
        var knobX = Math.Clamp(row.Min.X + row.Width * amount, row.Min.X + radius, row.Max.X - radius);
        var knob = new Vector2(knobX, row.Center.Y);
        paint.FillCircle(knob, row.Height * 0.20f, Knob);
        paint.FillCircle(knob + new Vector2(-row.Height * 0.04f, -row.Height * 0.05f), row.Height * 0.07f,
            new Vector4(1f, 1f, 1f, 0.72f));
        var inset = row.Height * 0.18f;
        var mark = row.Height * 0.558f;
        DrawMark(paint, Rect.FromSize(new Vector2(row.Min.X + inset, row.Center.Y - mark * 0.5f),
            new Vector2(mark, mark)), left, amount > 0.12f ? MarkOn : MarkOff, leftIcon);
        DrawMark(paint, Rect.FromSize(new Vector2(row.Max.X - inset - mark, row.Center.Y - mark * 0.5f),
            new Vector2(mark, mark)), right, amount > 0.88f ? MarkOn : MarkOff, rightIcon);
        return Slide(input, row, row, value, ref dragging, set, horizontal: true);
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
        if (input.WasPressed(row) || (input.IsHeld() && row.Contains(input.Cursor) && !dragging))
        {
            dragging = true;
        }

        if (dragging && input.IsHeld())
        {
            var next = horizontal
                ? (input.Cursor.X - track.Min.X) / MathF.Max(track.Width, 1f)
                : (track.Max.Y - input.Cursor.Y) / MathF.Max(track.Height, 1f);
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

    private static void DrawMark(IPaintSurface paint, Rect area, ShadeMark mark, Vector4 ink,
        ITextureHandle? icon = null)
    {
        if (icon is { IsReady: true })
        {
            paint.Image(icon, area, ink);
            return;
        }

        switch (mark)
        {
            case ShadeMark.Moon:
                DrawMoon(paint, area, ink);
                break;
            case ShadeMark.Note:
                DrawNote(paint, area, ink);
                break;
            case ShadeMark.Speaker:
                DrawSpeaker(paint, area, ink, true);
                break;
            default:
                DrawSun(paint, area, ink);
                break;
        }
    }

    private static void DrawMoon(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.34f;
        paint.FillCircle(c, s, ink);
        paint.FillCircle(c + new Vector2(s * 0.38f, -s * 0.12f), s * 0.72f, ink with { W = 0.08f });
    }

    private static void DrawNote(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.32f;
        var stroke = MathF.Max(1.3f, s * 0.22f);
        paint.FillCircle(c + new Vector2(-s * 0.28f, s * 0.42f), s * 0.22f, ink);
        paint.Line(c + new Vector2(-s * 0.08f, s * 0.42f), c + new Vector2(-s * 0.08f, -s * 0.62f), ink, stroke);
        paint.Line(c + new Vector2(-s * 0.08f, -s * 0.62f), c + new Vector2(s * 0.55f, -s * 0.38f), ink, stroke);
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
