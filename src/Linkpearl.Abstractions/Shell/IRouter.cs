using Linkpearl.Geometry;

namespace Linkpearl.Shell;

public interface IRouter
{
    string? CurrentAppletId { get; }

    bool AtHome { get; }

    bool IsTransitioning { get; }

    bool CanOpen(string appletId);

    void Open(string appletId);

    void Open(string appletId, string routeHint);

    void OpenFrom(string appletId, Rect originTile);

    void OpenFrom(string appletId, Rect originTile, string routeHint);

    void Back();

    void Home();

    void Recents();
}

public interface IShellIntents
{
    void Request(AppletIntent intent);

    bool TryConsume(string appletId, out AppletIntent intent);
}

public readonly struct AppletIntent
{
    public readonly string AppletId;
    public readonly string Action;
    public readonly string Payload;

    public AppletIntent(string appletId, string action, string payload)
    {
        AppletId = appletId;
        Action = action;
        Payload = payload;
    }
}
