using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Device.Windows;

// Power-on cover: field rings and motes, then the Linkpearl mark.
internal sealed class HandsetBoot
{
    private const float FadeIn = 0.06f;
    private const float Hold = 2.05f;
    private const float FadeOut = 0.5f;
    private const float Total = FadeIn + Hold + FadeOut;
    private const string MarkFile = "linkpearl-mark-core.png";
    private const string Word = "LINKPEARL";

    private float time;
    private bool pending;
    private bool playing;
    private bool reduce;

    public bool Active => pending || playing;

    public bool Covering => pending || (playing && time < CoverUntil);

    private float CoverUntil => reduce ? 0.24f : FadeIn + Hold;

    public void Play(bool reduceMotion)
    {
        reduce = reduceMotion;
        pending = false;
        playing = true;
        time = 0f;
    }

    public static void PaintVeil(IPaintSurface paint, Rect screen) =>
        paint.Fill(screen, new Vector4(0.06f, 0.05f, 0.04f, 1f));

    public void Cancel()
    {
        pending = false;
        playing = false;
        time = 0f;
    }

    public void Draw(in AppletFrame frame, Rect screen)
    {
        if (pending)
        {
            pending = false;
            playing = true;
            time = 0f;
        }

        if (!playing)
        {
            return;
        }

        time += frame.DeltaSeconds;
        var span = reduce ? 0.42f : Total;
        if (time >= span)
        {
            playing = false;
            return;
        }

        var fadeIn = reduce ? 0.04f : FadeIn;
        var fadeOut = reduce ? 0.16f : FadeOut;
        var hold = MathF.Max(0f, span - fadeIn - fadeOut);
        float veil;
        float mark;
        if (time < fadeIn)
        {
            veil = 1f;
            mark = 0.72f + 0.28f * Ease(time / MathF.Max(fadeIn, 0.001f));
        }
        else if (time < fadeIn + hold)
        {
            veil = 1f;
            mark = 1f;
        }
        else
        {
            var leave = (time - fadeIn - hold) / MathF.Max(fadeOut, 0.001f);
            veil = 1f - Ease(leave);
            mark = veil;
        }

        if (veil <= 0.004f)
        {
            return;
        }

        var gold = frame.Theme.Palette.WarmAccent;
        var lift = 1f - (1f - mark) * (1f - mark);
        var center = screen.Center;
        DrawField(frame, screen, center, gold, veil, time);

        var spanPx = MathF.Min(screen.Width, screen.Height);
        var side = Math.Clamp(spanPx * 0.42f, frame.Units(118f), frame.Units(168f)) *
            (0.96f + 0.04f * lift);
        var plate = Rect.FromSize(center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));

        var texture = frame.Textures.FromFile(frame.Paths.Asset(Path.Combine("Icons", "brand", MarkFile)));
        if (texture is { IsReady: true })
        {
            frame.Paint.Image(texture, plate, new Vector4(1f, 1f, 1f, mark));
        }
        else
        {
            DrawFallback(frame, center, side * 0.28f, gold, mark);
        }

        var wordTop = plate.Max.Y + frame.Units(6f);
        DrawWord(frame, new Vector2(center.X, wordTop), mark, lift);
        var track = Rect.FromSize(
            new Vector2(center.X - frame.Units(36f), wordTop + frame.Units(26f)),
            new Vector2(frame.Units(72f), frame.Units(2.2f)));
        frame.Paint.Fill(track, gold with { W = 0.16f * mark }, track.Height * 0.5f);
        var filled = Math.Clamp((time - fadeIn * 0.45f) / MathF.Max(hold + fadeIn * 0.45f, 0.001f), 0f, 1f);
        if (filled > 0.01f)
        {
            frame.Paint.Fill(track.LeftSlice(track.Width * Ease(filled)), gold with { W = 0.92f * mark },
                track.Height * 0.5f);
        }
    }

    private static void DrawField(in AppletFrame frame, Rect screen, Vector2 origin, Vector4 gold, float veil,
        float clock)
    {
        frame.Paint.FillGradient(screen, new Vector4(0.08f, 0.07f, 0.05f, veil),
            new Vector4(0.03f, 0.03f, 0.035f, veil), GradientAxis.Vertical);
        var reach = MathF.Max(screen.Width, screen.Height);
        for (var ring = 0; ring < 3; ring++)
        {
            var travel = (clock * 0.16f + ring * 0.33f) % 1f;
            var radius = reach * (0.08f + travel * 0.62f);
            var fade = (1f - travel) * (1f - travel) * 0.14f * veil;
            frame.Paint.StrokeCircle(origin, radius, gold with { W = fade }, frame.Units(1.2f));
        }

        for (var mote = 0; mote < 14; mote++)
        {
            var drift = mote * 0.618f;
            var x = screen.Min.X + Fract(drift + clock * 0.035f) * screen.Width;
            var y = screen.Min.Y + Fract(drift * 1.37f + clock * 0.022f) * screen.Height;
            var size = frame.Units(1.1f + (mote % 3) * 0.55f);
            frame.Paint.FillCircle(new Vector2(x, y), size, gold with { W = 0.18f * veil });
        }
    }

    private static float Fract(float value) => value - MathF.Floor(value);

    private static void DrawWord(in AppletFrame frame, Vector2 origin, float alpha, float lift)
    {
        var letter = frame.Units(12.5f);
        var gap = frame.Units(2.6f) + frame.Units(2.4f) * (1f - lift);
        var width = Word.Length * letter + (Word.Length - 1) * gap;
        var left = origin.X - width * 0.5f;
        var ink = new Vector4(0.93f, 0.86f, 0.68f, alpha);
        for (var index = 0; index < Word.Length; index++)
        {
            var cell = Rect.FromSize(new Vector2(left + index * (letter + gap), origin.Y),
                new Vector2(letter, frame.Units(20f)));
            frame.Text.DrawIn(cell, Word[index].ToString(),
                new TextStyle(FontRole.CaptionStrong, ink, TextAlign.Center));
        }
    }

    private static void DrawFallback(in AppletFrame frame, Vector2 center, float radius, Vector4 gold, float alpha)
    {
        frame.Paint.StrokeCircle(center, radius, gold with { W = 0.92f * alpha }, frame.Units(2.8f));
        frame.Paint.StrokeCircle(center, radius * 1.22f, gold with { W = 0.38f * alpha }, frame.Units(1.3f));
        frame.Paint.StrokeCircle(center + new Vector2(0f, radius * 1.22f), radius * 0.16f, gold with { W = alpha },
            frame.Units(2.2f));
    }

    private static float Ease(float value)
    {
        var t = Math.Clamp(value, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
