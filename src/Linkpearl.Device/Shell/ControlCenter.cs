using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Preferences;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

public readonly record struct ControlCenterResult(bool Recents, bool Closed);

public sealed class ControlCenter
{
    private bool open;

    public bool IsOpen => open;

    public void Open() => open = true;

    public void Close() => open = false;

    public ControlCenterResult Draw(IPaintSurface paint, ITextPainter text, IInputProbe input, ITheme theme,
        Rect screen, float scale, DisplayPreferences display)
    {
        if (!open)
        {
            return default;
        }

        paint.Fill(screen, theme.Palette.SurfaceSunken with { W = 0.55f });
        var sheet = screen.TopSlice(screen.Height * 0.58f).Inset(new Edges(scale * 12f, scale * 36f, scale * 12f, 0f));
        paint.Fill(sheet, theme.Palette.SurfaceRaised, scale * 16f);
        paint.Stroke(sheet, theme.Palette.WarmAccent with { W = 0.35f }, MathF.Max(1f, scale), scale * 16f);

        if (input.WasClicked(screen) && !sheet.Contains(input.Pointer))
        {
            open = false;
            return new ControlCenterResult(false, true);
        }

        var inner = sheet.Inset(scale * 14f);
        text.DrawIn(inner.TopSlice(scale * 22f), "Control",
            new TextStyle(FontRole.Title, theme.Palette.Ink));

        var grid = inner.Inset(new Edges(0f, scale * 28f, 0f, 0f));
        var gap = scale * 8f;
        var cellWidth = (grid.Width - gap) * 0.5f;
        var cellHeight = scale * 44f;
        var recents = false;

        DrawTile(paint, text, input, theme, Cell(grid, cellWidth, cellHeight, gap, 0, 0), "Quiet",
            display.Quiet, () => display.Quiet = !display.Quiet);
        DrawTile(paint, text, input, theme, Cell(grid, cellWidth, cellHeight, gap, 1, 0), "Hush in duty",
            display.QuietWhenBusy, () => display.QuietWhenBusy = !display.QuietWhenBusy);
        DrawTile(paint, text, input, theme, Cell(grid, cellWidth, cellHeight, gap, 0, 1), "Bells 24",
            display.Use24HourClock, () => display.Use24HourClock = !display.Use24HourClock);
        DrawTile(paint, text, input, theme, Cell(grid, cellWidth, cellHeight, gap, 1, 1), "World",
            display.ShowWorld, () => display.ShowWorld = !display.ShowWorld);
        DrawTile(paint, text, input, theme, Cell(grid, cellWidth, cellHeight, gap, 0, 2), "Marks",
            display.ShowMarks, () => display.ShowMarks = !display.ShowMarks);
        DrawTile(paint, text, input, theme, Cell(grid, cellWidth, cellHeight, gap, 1, 2), "Still",
            display.ReduceMotion, () => display.ReduceMotion = !display.ReduceMotion);

        var recentsCell = Cell(grid, cellWidth, cellHeight, gap, 0, 3);
        DrawTile(paint, text, input, theme, recentsCell, "Recents", false, () => recents = true);
        var closeCell = Cell(grid, cellWidth, cellHeight, gap, 1, 3);
        DrawTile(paint, text, input, theme, closeCell, "Close", false, () => open = false);

        if (recents)
        {
            open = false;
            return new ControlCenterResult(true, true);
        }

        return default;
    }

    private static Rect Cell(Rect grid, float width, float height, float gap, int column, int row)
    {
        var origin = new Vector2(grid.Min.X + column * (width + gap), grid.Min.Y + row * (height + gap));
        return Rect.FromSize(origin, new Vector2(width, height));
    }

    private static void DrawTile(IPaintSurface paint, ITextPainter text, IInputProbe input, ITheme theme, Rect area,
        string label, bool on, Action tap)
    {
        var gold = theme.Palette.WarmAccent;
        paint.Fill(area, on ? gold with { W = 0.28f } : theme.Palette.SurfaceOverlay, area.Height * 0.28f);
        paint.Stroke(area, gold with { W = on ? 0.7f : 0.22f }, MathF.Max(1f, area.Height * 0.03f),
            area.Height * 0.28f);
        text.DrawIn(area, label, new TextStyle(FontRole.CaptionStrong, on ? gold : theme.Palette.Ink, TextAlign.Center));
        if (input.ConsumeClick(area))
        {
            tap();
        }
    }
}
