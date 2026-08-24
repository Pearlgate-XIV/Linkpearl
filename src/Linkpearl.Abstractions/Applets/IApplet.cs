using Linkpearl.Geometry;

namespace Linkpearl.Applets;

public interface IApplet
{
    AppletManifest Manifest { get; }

    AppletBadge Badge { get; }

    void Enter(AppletEntry entry);

    void Leave();

    void Compose(in AppletFrame frame);
}

public interface IAppletBackground
{
    string AppletId { get; }

    void Resume();

    void Suspend();
}

public readonly struct AppletBadge
{
    public readonly int Count;
    public readonly bool AsDot;

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
    public readonly string? RouteHint;
    public readonly Rect? OriginTile;

    public AppletEntry(string? routeHint, Rect? originTile)
    {
        RouteHint = routeHint;
        OriginTile = originTile;
    }

    public static AppletEntry Plain => new(null, null);
}
