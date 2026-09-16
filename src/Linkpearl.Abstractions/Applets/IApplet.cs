using Linkpearl.Geometry;

namespace Linkpearl.Applets;

public interface IApplet
{
    AppletManifest Manifest { get; }

    AppletBadge Badge { get; }

    void Enter(AppletEntry entry);

    void Leave();

    void Compose(in AppletFrame frame);

    // Soft-key Back. Return true when an inner page was closed so the shell stays here.
    bool CanGoBack => false;

    bool Back() => false;

    // Last inner page or tab, restored through AppletEntry.RouteHint after a cold open.
    string Place => string.Empty;

    // When false the shell hides the icon and will not open the applet.
    bool Allowed => true;
}

public interface IAppletBackground
{
    string AppletId { get; }

    void Resume();

    void Suspend();
}

public readonly struct AppletBadge
{
    public int Count { get; }
    public bool AsDot { get; }

    public AppletBadge(int count, bool asDot = false)
    {
        Count = count;
        AsDot = asDot;
    }

    public static AppletBadge None => new(0);

    public bool IsVisible => Count > 0 || AsDot;
}

public readonly struct AppletEntry
{
    public string? RouteHint { get; }
    public Rect? OriginTile { get; }

    public AppletEntry(string? routeHint, Rect? originTile)
    {
        RouteHint = routeHint;
        OriginTile = originTile;
    }

    public static AppletEntry Plain => new(null, null);
}
