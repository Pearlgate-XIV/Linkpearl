using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Preferences;

namespace Linkpearl.Device.Shell;

// Phone-style home grid: 5x6, packed from the top. Icons stay a fixed diameter. Columns
// use the full width so labels read in full; rows stay close rather than stretching to
// fill leftover glass.
public sealed class HomeSurface
{
    private const int Columns = 5;
    private const int Rows = 6;
    private const float IconUnits = 52f;
    private const float LabelGapUnits = 4f;
    private const float GapUnits = 10f;
    private const float TopPadUnits = 2f;
    private const float UnderLabelUnits = 4f;

    private readonly IReadOnlyList<IApplet> apps;

    public HomeSurface(IReadOnlyList<IApplet> apps)
    {
        this.apps = apps;
    }

    public void Draw(in AppletFrame frame, Rect area, bool hush = false)
    {
        var usable = area.Inset(frame.Units(8f));
        if (usable.IsEmpty)
        {
            return;
        }

        var icon = frame.Units(IconUnits);
        var labelGap = frame.Units(LabelGapUnits);
        var labelHeight = frame.Text.LineHeight(FontRole.Caption);
        var gap = frame.Units(GapUnits);
        var topPad = frame.Units(TopPadUnits);
        var underLabel = frame.Units(UnderLabelUnits);
        var cellHeight = topPad + icon + labelGap + labelHeight + underLabel;
        var packedHeight = cellHeight * Rows + gap * (Rows - 1);
        if (packedHeight > usable.Height && packedHeight > 0.001f)
        {
            var fit = usable.Height / packedHeight;
            gap *= fit;
            topPad *= fit;
            underLabel *= fit;
            cellHeight = topPad + icon + labelGap + labelHeight + underLabel;
            packedHeight = cellHeight * Rows + gap * (Rows - 1);
        }

        var grid = new TileGrid(Rect.FromSize(usable.Min, new Vector2(usable.Width, packedHeight)), Columns, Rows,
            gap);
        var slot = 0;
        var capacity = Columns * Rows;
        for (var index = 0; index < apps.Count && slot < capacity; index++)
        {
            if (IsDocked(apps[index].Manifest.Id))
            {
                continue;
            }

            DrawTile(frame, grid.CellAt(slot), apps[index], icon, labelGap, labelHeight, hush);
            slot++;
        }
    }

    internal static float DockRowHeight(in AppletFrame frame)
    {
        var icon = frame.Units(IconUnits);
        return frame.Units(TopPadUnits) + icon + frame.Units(LabelGapUnits) +
            frame.Text.LineHeight(FontRole.Caption) + frame.Units(UnderLabelUnits);
    }

    internal static void DrawShortcut(in AppletFrame frame, Rect cell, string id, string name, float icon,
        float labelGap, float labelHeight, bool hush, AppletBadge badge, Action pressed)
    {
        if (cell.IsEmpty)
        {
            return;
        }

        icon = MathF.Min(icon, MathF.Max(MathF.Min(cell.Width, cell.Height) * 0.72f, 1f));
        var topPad = frame.Units(TopPadUnits);
        var hover = frame.Input.IsHovering(cell);
        var drawn = hover ? icon * 1.08f : icon;
        var iconArea = Rect.FromSize(new Vector2(cell.Center.X - icon * 0.5f, cell.Min.Y + topPad),
            new Vector2(icon, icon));
        var drawArea = Rect.FromSize(iconArea.Center - new Vector2(drawn * 0.5f, drawn * 0.5f),
            new Vector2(drawn, drawn));
        AppMarks.DrawFace(frame, drawArea, id, hover);

        if (badge.IsVisible && !hush)
        {
            DrawBadge(frame, drawArea, badge);
        }

        var labelArea = new Rect(
            new Vector2(cell.Min.X + frame.Units(2f), iconArea.Max.Y + labelGap),
            new Vector2(cell.Max.X - frame.Units(2f), iconArea.Max.Y + labelGap + labelHeight));
        DrawLabel(frame, labelArea, name);

        if (frame.Input.ConsumeClick(cell))
        {
            pressed();
        }
    }

    internal static bool IsDocked(string appletId) =>
        string.Equals(appletId, "camera", StringComparison.Ordinal);

    internal static void DrawTile(in AppletFrame frame, Rect cell, IApplet applet, float icon, float labelGap,
        float labelHeight, bool hush)
    {
        if (cell.IsEmpty)
        {
            return;
        }

        icon = MathF.Min(icon, MathF.Max(MathF.Min(cell.Width, cell.Height) * 0.72f, 1f));
        var topPad = frame.Units(TopPadUnits);
        if (icon + labelGap + labelHeight + topPad > cell.Height && cell.Height > icon + labelGap + topPad)
        {
            labelHeight = MathF.Max(cell.Height - icon - labelGap - topPad, frame.Text.LineHeight(FontRole.Caption));
        }

        var hover = frame.Input.IsHovering(cell);
        var drawn = hover ? icon * 1.08f : icon;
        var iconArea = Rect.FromSize(new Vector2(cell.Center.X - icon * 0.5f, cell.Min.Y + topPad),
            new Vector2(icon, icon));
        var drawArea = Rect.FromSize(iconArea.Center - new Vector2(drawn * 0.5f, drawn * 0.5f),
            new Vector2(drawn, drawn));
        AppMarks.DrawFace(frame, drawArea, applet.Manifest.Id, hover);

        if (applet.Badge.IsVisible && !hush)
        {
            DrawBadge(frame, drawArea, applet.Badge);
        }

        var labelArea = new Rect(
            new Vector2(cell.Min.X + frame.Units(2f), iconArea.Max.Y + labelGap),
            new Vector2(cell.Max.X - frame.Units(2f), iconArea.Max.Y + labelGap + labelHeight));
        DrawLabel(frame, labelArea, PhoneLanguages.App(applet.Manifest.Id, applet.Manifest.DisplayNameKey));

        if (frame.Input.ConsumeClick(cell))
        {
            frame.Router.OpenFrom(applet.Manifest.Id, iconArea);
        }
    }

    private static void DrawLabel(in AppletFrame frame, Rect area, string name)
    {
        if (area.IsEmpty)
        {
            return;
        }

        var style = new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink, TextAlign.Center);
        var size = frame.Text.Measure(name, FontRole.Caption);
        if (size.X <= area.Width)
        {
            frame.Text.DrawIn(area, name, style);
            return;
        }

        frame.Text.DrawWrapped(area, name, style);
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
