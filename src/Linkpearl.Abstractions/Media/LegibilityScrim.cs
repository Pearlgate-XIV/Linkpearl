namespace Linkpearl.Media;

// Maps wallpaper luminance to a dim overlay. Bright plates need more ink contrast, dark plates
// barely any: a single hardcoded opacity looks right on one image and wrong on the next.
public static class LegibilityScrim
{
    private const float Floor = 0.16f;
    private const float Ceiling = 0.56f;

    public static float Alpha(float luminance)
    {
        var amount = Math.Clamp(luminance, 0f, 1f);
        return Floor + amount * (Ceiling - Floor);
    }
}
