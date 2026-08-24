using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Painting;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Clock;

public sealed class ClockApplet : IApplet
{
    public static readonly AppletManifest Manifest = new()
    {
        Id = "clock",
        DisplayNameKey = "Clock",
        Family = AppletFamily.Life,
        Glyph = "⏰",
        HomeOrder = 0,
    };

    private readonly IClock clock;

    public ClockApplet(IClock clock)
    {
        this.clock = clock;
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
        var content = frame.Content.Inset(frame.Units(20f));
        var timeArea = content.TopSlice(content.Height * 0.4f);
        frame.Text.DrawIn(timeArea, clock.Now.ToString("HH:mm"),
            new TextStyle(FontRole.Display, frame.Theme.Palette.Ink, TextAlign.Center));

        var dateArea = timeArea.Translate(new Vector2(0f, timeArea.Height));
        frame.Text.DrawIn(dateArea.WithHeight(frame.Units(28f)), clock.Now.ToString("dddd, MMMM d"),
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Center));
    }
}
