using Linkpearl.Geometry;

namespace Linkpearl.Painting;

public static class CaseWash
{
    public static void StampSkin(IPaintSurface paint, ITextureHandle skin, Rect window, float radius = 0f,
        bool sealTopRim = false, Vector4? tint = null)
    {
        if (!skin.IsReady || window.IsEmpty)
        {
            return;
        }

        var wash = tint ?? new Vector4(1.18f, 1.18f, 1.20f, 1f);
        if (radius > 0.5f)
        {
            paint.ImageRounded(skin, window, Vector2.Zero, Vector2.One, wash, radius);
        }
        else
        {
            paint.Image(skin, window, wash);
        }

        _ = sealTopRim;
    }

    public static void Android(IPaintSurface paint, Rect body, Rect screen, Rect volume, Rect power, float bodyRadius,
        float screenRadius)
    {
        if (body.IsEmpty)
        {
            return;
        }

        var shine = new Vector4(0.93f, 0.94f, 0.97f, 1f);
        var mid = new Vector4(0.62f, 0.64f, 0.68f, 1f);
        var shade = new Vector4(0.28f, 0.29f, 0.32f, 1f);
        var radius = MathF.Min(bodyRadius, MathF.Min(body.Width, body.Height) * 0.5f);
        paint.Fill(body, mid, radius, Corner.All);
        var core = body.Inset(new Edges(radius * 0.62f, 0f));
        if (!core.IsEmpty)
        {
            paint.FillGradient(core, shine with { W = 0.42f }, shade with { W = 0.38f }, GradientAxis.Vertical);
        }

        paint.Stroke(body, shine, MathF.Max(1.4f, body.Width * 0.012f), radius);
        paint.Stroke(body.Inset(MathF.Max(1.2f, body.Width * 0.008f)), shade with { W = 0.55f },
            MathF.Max(1.0f, body.Width * 0.006f), MathF.Max(0f, radius - 1.2f));
        if (!screen.IsEmpty)
        {
            var hole = MathF.Min(screenRadius, MathF.Min(screen.Width, screen.Height) * 0.5f);
            paint.Fill(screen, new Vector4(0f, 0f, 0f, 1f), hole, Corner.All);
            paint.Stroke(screen, shade, MathF.Max(1.1f, body.Width * 0.006f), hole);
        }

        Nub(paint, volume);
        Nub(paint, power);
    }

    private static void Nub(IPaintSurface paint, Rect area)
    {
        if (area.IsEmpty || area.Height < 4f)
        {
            return;
        }

        var shine = new Vector4(0.90f, 0.91f, 0.95f, 1f);
        var mid = new Vector4(0.58f, 0.60f, 0.64f, 1f);
        var cap = MathF.Min(area.Width, area.Height) * 0.5f;
        paint.Fill(area, mid, cap, Corner.All);
        paint.FillGradient(area.Inset(new Edges(0f, cap * 0.35f)), shine with { W = 0.35f },
            new Vector4(0.22f, 0.22f, 0.24f, 0.35f), GradientAxis.Horizontal);
        paint.Stroke(area, shine, 1.1f, cap);
    }

    public static void Body(IPaintSurface paint, Rect body, float radius, Vector4 metal, Vector4 accent)
    {
        if (body.IsEmpty)
        {
            return;
        }

        var light = Mix(Lift(metal, 0.42f), new Vector4(0.82f, 0.84f, 0.88f, 1f), 0.28f);
        var mid = Mix(Lift(metal, 0.18f), new Vector4(0.62f, 0.64f, 0.68f, 1f), 0.22f);
        var dark = Mix(metal, new Vector4(0.52f, 0.53f, 0.56f, 1f), 0.28f);
        paint.FillSquircleGradient(body, light, mid, dark, dark, radius);
        _ = accent;
    }

    public static void Sheen(IPaintSurface paint, Rect body, Rect screen, float radius = 0f)
    {
        if (body.IsEmpty || screen.IsEmpty)
        {
            return;
        }

        var pad = MathF.Max(0f, radius);
        var gleam = new Vector4(1f, 1f, 1f, 0.22f);
        var hush = new Vector4(0.08f, 0.08f, 0.10f, 0.10f);

        var top = new Rect(new Vector2(body.Min.X + pad, body.Min.Y),
            new Vector2(body.Max.X - pad, screen.Min.Y));
        if (!top.IsEmpty)
        {
            paint.FillCorners(top, gleam, gleam with { W = 0.08f }, gleam with { W = 0.03f }, gleam with { W = 0.10f });
        }

        var bottom = new Rect(new Vector2(body.Min.X + pad, screen.Max.Y),
            new Vector2(body.Max.X - pad, body.Max.Y));
        if (!bottom.IsEmpty)
        {
            paint.FillCorners(bottom, hush with { W = 0.06f }, hush with { W = 0.04f }, hush with { W = 0.08f },
                hush with { W = 0.11f });
        }

        var left = new Rect(new Vector2(body.Min.X, MathF.Max(screen.Min.Y, body.Min.Y + pad)),
            new Vector2(screen.Min.X, MathF.Min(screen.Max.Y, body.Max.Y - pad)));
        var right = new Rect(new Vector2(screen.Max.X, MathF.Max(screen.Min.Y, body.Min.Y + pad)),
            new Vector2(body.Max.X, MathF.Min(screen.Max.Y, body.Max.Y - pad)));
        if (!left.IsEmpty)
        {
            paint.FillCorners(left, gleam with { W = 0.18f }, gleam with { W = 0.08f }, hush with { W = 0.07f },
                hush);
        }

        if (!right.IsEmpty)
        {
            paint.FillCorners(right, gleam with { W = 0.06f }, gleam with { W = 0.04f }, hush with { W = 0.08f },
                hush with { W = 0.06f });
        }
    }

    private static Vector4 Mix(Vector4 from, Vector4 to, float amount)
    {
        var t = Math.Clamp(amount, 0f, 1f);
        return from + (to - from) * t;
    }

    private static Vector4 Lift(Vector4 color, float amount) =>
        new(Math.Clamp(color.X + amount, 0f, 1f), Math.Clamp(color.Y + amount, 0f, 1f),
            Math.Clamp(color.Z + amount, 0f, 1f), color.W);

    private static Vector4 Drop(Vector4 color, float amount) =>
        new(Math.Clamp(color.X - amount, 0f, 1f), Math.Clamp(color.Y - amount, 0f, 1f),
            Math.Clamp(color.Z - amount, 0f, 1f), color.W);
}
