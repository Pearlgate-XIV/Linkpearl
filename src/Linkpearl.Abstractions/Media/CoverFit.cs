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
}
