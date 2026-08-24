using Linkpearl.Applets;
using Linkpearl.Device.Time;
using Linkpearl.Geometry;
using Linkpearl.Time;

namespace Linkpearl.Device.Shell;

// Composes the pieces that make a Linkpearl handset recognisable: status strip, home grid or
// the current applet, and soft-key nav. Chassis geometry is drawn by the window host; this type
// only fills the screen rect it is handed.
public sealed class HandsetShell
{
    private readonly RouteStack router;
    private readonly HomeSurface home;
    private readonly IClock clock;

    public HandsetShell(RouteStack router, HomeSurface home, IClock clock)
    {
        this.router = router;
        this.home = home;
        this.clock = clock;
    }

    public bool Use24HourClock { get; set; }

    public void Draw(in AppletFrame outerFrame, Rect screen)
    {
        router.Advance(outerFrame.DeltaSeconds, 0.28f);

        StatusStrip.Draw(outerFrame, screen, HandsetClockText.Format(clock, Use24HourClock));
        var content = screen.Inset(new Edges(0f, StatusStrip.Height(outerFrame.Scale), 0f,
            SoftKeyBar.Height(outerFrame.Scale)));

        var frame = outerFrame.WithContent(content);
        if (router.Current is { } current)
        {
            current.Compose(frame);
        }
        else
        {
            home.Draw(frame, content);
        }

        var key = SoftKeyBar.Draw(outerFrame, screen, router.CurrentAppletId is not null);
        switch (key)
        {
            case SoftKey.Home:
                router.Home();
                break;
            case SoftKey.Back:
                router.Back();
                break;
            case SoftKey.Recents:
                router.Recents();
                break;
        }
    }
}
