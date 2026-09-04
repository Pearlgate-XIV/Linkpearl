using Linkpearl.Geometry;

namespace Linkpearl.Device.Shell;

internal static class EdgeHandles
{
    public const float WidthUnits = 18f;
    public const float HeightUnits = 72f;

    public static float CenterY(Rect screen, float scale)
    {
        var top = screen.Min.Y + scale * 10f;
        var bottom = screen.Max.Y - SoftKeyBar.Height(scale);
        return (top + bottom) * 0.5f;
    }

    public static Rect Left(Rect screen, float scale)
    {
        var width = WidthUnits * scale;
        var height = HeightUnits * scale;
        return Rect.FromSize(new Vector2(screen.Min.X, CenterY(screen, scale) - height * 0.5f),
            new Vector2(width, height));
    }

    public static Rect Right(Rect screen, float scale)
    {
        var width = WidthUnits * scale;
        var height = HeightUnits * scale;
        return Rect.FromSize(new Vector2(screen.Max.X - width, CenterY(screen, scale) - height * 0.5f),
            new Vector2(width, height));
    }
}
