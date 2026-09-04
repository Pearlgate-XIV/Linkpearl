using Linkpearl.Geometry;

namespace Linkpearl.Media;

// Cover-fit crop in UV space: the image fills the target, overflow is clipped equally on the
// long axis. Pure math, no textures, so the same crop can be unit-tested without Dalamud.
public readonly struct CoverUv
{
    public readonly Vector2 Min;
    public readonly Vector2 Max;

    public CoverUv(Vector2 min, Vector2 max)
    {
        Min = min;
        Max = max;
    }

    public static CoverUv Full => new(Vector2.Zero, Vector2.One);
}

public static class CoverFit
{
    public static CoverUv Uv(Vector2 sourceSize, Vector2 targetSize)
    {
        if (sourceSize.X <= 0f || sourceSize.Y <= 0f || targetSize.X <= 0f || targetSize.Y <= 0f)
        {
            return CoverUv.Full;
        }

        var sourceAspect = sourceSize.X / sourceSize.Y;
        var targetAspect = targetSize.X / targetSize.Y;
        if (MathF.Abs(sourceAspect - targetAspect) < 0.0001f)
        {
            return CoverUv.Full;
        }

        if (sourceAspect > targetAspect)
        {
            var visible = targetAspect / sourceAspect;
            var pad = (1f - visible) * 0.5f;
            return new CoverUv(new Vector2(pad, 0f), new Vector2(1f - pad, 1f));
        }

        var visibleHeight = sourceAspect / targetAspect;
        var padY = (1f - visibleHeight) * 0.5f;
        return new CoverUv(new Vector2(0f, padY), new Vector2(1f, 1f - padY));
    }

    // zoom 1 is cover-fit. focus is the image-space center of the window, 0–1.
    public static CoverUv Framed(Vector2 sourceSize, Vector2 targetSize, float zoom, Vector2 focus)
    {
        var cover = Uv(sourceSize, targetSize);
        var scale = MathF.Max(zoom, 1f);
        var width = (cover.Max.X - cover.Min.X) / scale;
        var height = (cover.Max.Y - cover.Min.Y) / scale;
        width = Math.Clamp(width, 0.02f, 1f);
        height = Math.Clamp(height, 0.02f, 1f);
        var cx = Math.Clamp(focus.X, width * 0.5f, 1f - width * 0.5f);
        var cy = Math.Clamp(focus.Y, height * 0.5f, 1f - height * 0.5f);
        return new CoverUv(new Vector2(cx - width * 0.5f, cy - height * 0.5f),
            new Vector2(cx + width * 0.5f, cy + height * 0.5f));
    }

    public static Rect InscribedSquare(Rect area)
    {
        var side = MathF.Min(area.Width, area.Height);
        if (side <= 0f)
        {
            return area;
        }

        return Rect.FromSize(new Vector2(area.Center.X - side * 0.5f, area.Center.Y - side * 0.5f),
            new Vector2(side, side));
    }

    public static Rect Contained(Vector2 sourceSize, Rect target)
    {
        if (sourceSize.X <= 0f || sourceSize.Y <= 0f || target.IsEmpty)
        {
            return target;
        }

        var sourceAspect = sourceSize.X / sourceSize.Y;
        var targetAspect = target.Width / target.Height;
        if (sourceAspect > targetAspect)
        {
            var height = target.Width / sourceAspect;
            var y = target.Min.Y + (target.Height - height) * 0.5f;
            return Rect.FromSize(new Vector2(target.Min.X, y), new Vector2(target.Width, height));
        }

        var width = target.Height * sourceAspect;
        var x = target.Min.X + (target.Width - width) * 0.5f;
        return Rect.FromSize(new Vector2(x, target.Min.Y), new Vector2(width, target.Height));
    }

    public static Rect Snapped(Rect area)
    {
        var minX = MathF.Round(area.Min.X);
        var minY = MathF.Round(area.Min.Y);
        var maxX = MathF.Round(area.Max.X);
        var maxY = MathF.Round(area.Max.Y);
        if (maxX <= minX)
        {
            maxX = minX + 1f;
        }

        if (maxY <= minY)
        {
            maxY = minY + 1f;
        }

        return new Rect(new Vector2(minX, minY), new Vector2(maxX, maxY));
    }
}
