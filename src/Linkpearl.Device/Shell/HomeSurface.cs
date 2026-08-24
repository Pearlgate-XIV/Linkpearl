using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Canvas.Layout;
using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Device.Shell;

// Linkpearl's home grid: circular tiles, not squircles. Renders one page at a time; paging and
// folders are follow-up work, not part of this foundation pass.
public sealed class HomeSurface
{
    private const int Columns = 4;
    private const int Rows = 5;

    private readonly IReadOnlyList<IApplet> apps;

    public HomeSurface(IReadOnlyList<IApplet> apps)
    {
        this.apps = apps;
    }

    public void Draw(in AppletFrame frame, Rect area)
    {
        var grid = new TileGrid(area.Inset(frame.Units(12f)), Columns, Rows, frame.Units(14f));
        for (var index = 0; index < apps.Count && index < grid.CapacityPerPage; index++)
        {
            DrawTile(frame, grid.CellAt(index), apps[index]);
        }
    }

    private static void DrawTile(in AppletFrame frame, Rect cell, IApplet applet)
    {
        var diameter = MathF.Min(cell.Width, cell.Height * 0.72f);
        var iconArea = Rect.FromSize(new Vector2(cell.Center.X - diameter * 0.5f, cell.Min.Y), new Vector2(diameter, diameter));
        var accent = frame.Theme.AccentFor(applet.Manifest.Id);

        frame.Paint.FillCircle(iconArea.Center, diameter * 0.5f, accent);
        frame.Text.DrawIn(iconArea, applet.Manifest.Glyph,
            new TextStyle(FontRole.Title, frame.Theme.Palette.AccentInk, TextAlign.Center));

        if (applet.Badge.IsVisible)
        {
            DrawBadge(frame, iconArea, applet.Badge);
        }

        var labelArea = new Rect(new Vector2(cell.Min.X, iconArea.Max.Y + frame.Units(4f)), cell.Max);
        frame.Text.DrawEllipsized(labelArea, applet.Manifest.DisplayNameKey,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink, TextAlign.Center));

        if (frame.Input.ConsumeClick(iconArea))
        {
            frame.Router.OpenFrom(applet.Manifest.Id, iconArea);
        }
    }

    private static void DrawBadge(in AppletFrame frame, Rect iconArea, AppletBadge badge)
    {
        var badgeRadius = frame.Units(badge.AsDot ? 4f : 8f);
        var badgeCenter = new Vector2(iconArea.Max.X - badgeRadius * 0.4f, iconArea.Min.Y + badgeRadius * 0.4f);
        frame.Paint.FillCircle(badgeCenter, badgeRadius, frame.Theme.Palette.Negative);
        if (badge.AsDot)
        {
            return;
        }

        var label = badge.Count > 99 ? "99+" : badge.Count.ToString(CultureInfo.CurrentCulture);
        frame.Text.Draw(badgeCenter, label,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.AccentInk, TextAlign.Center));
    }
}
