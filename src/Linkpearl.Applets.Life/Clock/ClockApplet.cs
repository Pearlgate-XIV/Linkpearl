using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Preferences;
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
    private readonly DisplayPreferences preferences;

    public ClockApplet(IClock clock, DisplayPreferences preferences)
    {
        this.clock = clock;
        this.preferences = preferences;
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
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(12f));
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Clock",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));

        var local = stack.Take(frame.Units(120f));
        CardChrome.Draw(frame, local);
        var localInset = local.Inset(frame.Units(14f));
        CardChrome.DrawKicker(frame, localInset.TopSlice(frame.Units(16f)), "Local", frame.Theme.Palette.WarmAccent);
        var localFormat = preferences.Use24HourClock ? "HH:mm" : "h:mm tt";
        frame.Text.DrawIn(localInset.Inset(new Edges(0f, frame.Units(20f), 0f, frame.Units(24f))),
            clock.Now.ToString(localFormat),
            new TextStyle(FontRole.Display, frame.Theme.Palette.Ink, TextAlign.Center));
        frame.Text.DrawIn(localInset.BottomSlice(frame.Units(22f)), clock.Now.ToString("dddd, MMMM d"),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));

        var eorzea = stack.Take(frame.Units(88f));
        CardChrome.Draw(frame, eorzea);
        var eorzeaInset = eorzea.Inset(frame.Units(14f));
        CardChrome.DrawKicker(frame, eorzeaInset.TopSlice(frame.Units(16f)), "Eorzea", frame.Theme.Palette.WarmAccent);
        var bells = EorzeaTime.FromUnix(clock.UtcNow.ToUnixTimeSeconds());
        frame.Text.DrawIn(eorzeaInset.Inset(new Edges(0f, frame.Units(18f), 0f, 0f)), bells.Format(),
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, TextAlign.Center));
    }
}