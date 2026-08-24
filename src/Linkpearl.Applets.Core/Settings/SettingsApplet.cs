using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Preferences;

namespace Linkpearl.Applets.Core.Settings;

public sealed class SettingsApplet : IApplet
{
    public static readonly AppletManifest Manifest = new()
    {
        Id = "settings",
        DisplayNameKey = "Settings",
        Family = AppletFamily.System,
        Glyph = "⚙",
        RemovableFromHome = false,
        HomeOrder = 999,
    };

    private readonly DisplayPreferences preferences;
    private readonly HostEnvironment environment;

    public SettingsApplet(DisplayPreferences preferences, HostEnvironment environment)
    {
        this.preferences = preferences;
        this.environment = environment;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public void Enter(AppletEntry entry)
    {
    }

    public void Leave()
    {
    }

    public void Compose(in AppletFrame frame)
    {
        var content = frame.Content.Inset(frame.Units(16f));
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(2f));

        DrawSectionHeader(frame, stack.Take(frame.Units(28f)), "Display");
        DrawToggleRow(frame, stack.Take(frame.Units(48f)), "24-hour clock", preferences.Use24HourClock,
            value => preferences.Use24HourClock = value);

        stack.Take(frame.Units(20f));
        DrawSectionHeader(frame, stack.Take(frame.Units(28f)), "About");
        DrawValueRow(frame, stack.Take(frame.Units(40f)), "Version", environment.Version);
        DrawValueRow(frame, stack.Take(frame.Units(40f)), "Build", environment.IsDevelopment ? "Dev" : "Release");
    }

    private static void DrawSectionHeader(in AppletFrame frame, Rect row, string label) =>
        frame.Text.DrawIn(row, label, new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));

    private static void DrawValueRow(in AppletFrame frame, Rect row, string label, string value)
    {
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceRaised, frame.Units(10f));
        var inset = row.Inset(new Edges(frame.Units(12f), 0f));
        frame.Text.DrawIn(inset, label, new TextStyle(FontRole.Body, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(inset, value, new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Right));
    }

    private static void DrawToggleRow(in AppletFrame frame, Rect row, string label, bool value,
        Action<bool> onChanged)
    {
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceRaised, frame.Units(10f));
        var inset = row.Inset(new Edges(frame.Units(12f), 0f));
        frame.Text.DrawIn(inset.LeftSlice(inset.Width - frame.Units(56f)), label,
            new TextStyle(FontRole.Body, frame.Theme.Palette.Ink));

        var switchArea = inset.RightSlice(frame.Units(44f));
        var switchRect = new Rect(new Vector2(switchArea.Min.X, switchArea.Center.Y - frame.Units(12f)),
            new Vector2(switchArea.Max.X, switchArea.Center.Y + frame.Units(12f)));
        var trackColor = value ? frame.Theme.Palette.Accent : frame.Theme.Palette.SurfaceOverlay;
        frame.Paint.Fill(switchRect, trackColor, frame.Units(999f));

        var knobRadius = frame.Units(10f);
        var knobX = value ? switchRect.Max.X - knobRadius - frame.Units(2f) : switchRect.Min.X + knobRadius + frame.Units(2f);
        frame.Paint.FillCircle(new Vector2(knobX, switchRect.Center.Y), knobRadius, frame.Theme.Palette.AccentInk);

        if (frame.Input.ConsumeClick(row))
        {
            onChanged(!value);
        }
    }
}
