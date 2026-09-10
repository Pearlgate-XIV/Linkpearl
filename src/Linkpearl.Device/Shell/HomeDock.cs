using Linkpearl.Applets;
using Linkpearl.Destinations;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Preferences;

namespace Linkpearl.Device.Shell;

// Phone on the left and Camera on the right, pinned just above the soft keys on Home only.
internal static class HomeDock
{
    private const int Columns = 5;

    public static float Height(in AppletFrame frame) =>
        frame.Units(8f) + frame.Units(68f) + frame.Units(4f) +
        frame.Text.LineHeight(FontRole.Caption) + frame.Units(10f);

    public static void Draw(in AppletFrame frame, Rect strip, IApplet? camera, bool hush, Action openPhone,
        Action openCamera)
    {
        if (strip.IsEmpty)
        {
            return;
        }

        var icon = frame.Units(68f);
        var labelGap = frame.Units(4f);
        var labelHeight = frame.Text.LineHeight(FontRole.Caption);
        var grid = new TileGrid(strip.Inset(new Edges(frame.Units(8f), frame.Units(8f), frame.Units(8f),
            frame.Units(2f))), Columns, 1, frame.Units(10f));
        HomeSurface.DrawShortcut(frame, grid.Cell(0, 0), "phone", PhoneLanguages.App("phone", "Phone"), icon,
            labelGap, labelHeight, hush, AppletBadge.None, openPhone);
        if (camera is not null)
        {
            HomeSurface.DrawShortcut(frame, grid.Cell(Columns - 1, 0), camera.Manifest.Id,
                PhoneLanguages.App(camera.Manifest.Id, camera.Manifest.DisplayNameKey), icon, labelGap, labelHeight,
                hush, camera.Badge, openCamera);
        }
    }
}
