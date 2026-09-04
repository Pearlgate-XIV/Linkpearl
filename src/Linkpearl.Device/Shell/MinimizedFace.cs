using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

// Miniature phone: same chassis aspect as the open handset, clock on the glass, slide-to-wake
// on the track. Drag anywhere except the slider to move it, locked or unlocked.
public static class MinimizedFace
{

    public static bool Draw(IPaintSurface paint, ITextPainter text, ITheme theme, IInputProbe input, Rect screen,
        float scale, string clockText, PocketUnlock unlock, float deltaSeconds, bool allowSlide)
    {
        var inset = screen.Inset(scale * 8f);
        if (inset.IsEmpty)
        {
            return false;
        }

        text.DrawIn(inset.TopSlice(scale * 22f), clockText,
            new TextStyle(FontRole.CaptionStrong, theme.Palette.Ink, TextAlign.Center));
        return unlock.Draw(paint, input, theme, screen, scale, deltaSeconds, allowSlide);
    }
}
