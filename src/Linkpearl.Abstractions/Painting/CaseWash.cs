using Linkpearl.Geometry;

namespace Linkpearl.Painting;

public static class CaseWash
{
    public static void StampSkin(IPaintSurface paint, ITextureHandle skin, Rect window, float radius = 0f,
        bool sealTopRim = false)
    {
        if (!skin.IsReady || window.IsEmpty)
        {
            return;
        }

        var tint = new Vector4(1.18f, 1.18f, 1.20f, 1f);
        if (radius > 0.5f)
        {
            paint.ImageRounded(skin, window, Vector2.Zero, Vector2.One, tint, radius);
        }
        else
        {
            paint.Image(skin, window, tint);
        }

        if (sealTopRim)
        {
            FillTopRim(paint, skin, window, tint);
        }
    }

    // The Android skin used to have a camera cutout in the top rim. Cover that span with a
    // 1:1 slice of the same rim from beside the hole so the metal reads as one piece.
    private static void FillTopRim(IPaintSurface paint, ITextureHandle skin, Rect window, Vector4 tint)
    {
        const float rim = 10f / 1000f;
        const float donorLeft = 110f / 472f;
        const float donorRight = 190f / 472f;
        var height = window.Height * rim;
        if (height < 1f)
        {
            return;
        }

        var width = window.Width * (donorRight - donorLeft);
        var dest = new Rect(
            new Vector2(window.Center.X - width * 0.5f, window.Min.Y),
            new Vector2(window.Center.X + width * 0.5f, window.Min.Y + height));
        paint.Image(skin, dest, new Vector2(donorLeft, 0f), new Vector2(donorRight, rim), tint);
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
