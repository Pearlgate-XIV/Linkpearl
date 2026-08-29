using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Shell;

namespace Linkpearl.Device.Shell;

public enum ShellMotion : byte
{
    None = 0,
    Presenting = 1,
    Dismissing = 2,
}

// Owns the applet back-stack and the single in-flight transition. Applets never see this type:
// they see IRouter. Presentation animation lives on the shell layer that draws Current/Motion*.
public sealed class RouteStack : IRouter
{
    private readonly IReadOnlyDictionary<string, IApplet> applets;
    private readonly Stack<IApplet> history = new();
    private readonly List<string> recents = new();
    private IApplet? current;
    private IApplet? motionEntering;
    private IApplet? motionLeaving;
    private ShellMotion motion = ShellMotion.None;
    private Rect? motionOrigin;
    private float motionProgress;
    private bool recentsWanted;

    public RouteStack(IReadOnlyDictionary<string, IApplet> applets)
    {
        this.applets = applets;
    }

    public event Action<string>? Opened;

    public event Action? ReturnedHome;

    public IApplet? Current => current;

    public string? CurrentAppletId => current?.Manifest.Id;

    public IReadOnlyList<string> RecentIds => recents;

    public bool AtHome => current is null && motion == ShellMotion.None;

    public bool IsTransitioning => motion != ShellMotion.None;

    public ShellMotion Motion => motion;

    public float MotionProgress => motionProgress;

    public IApplet? MotionEntering => motionEntering;

    public IApplet? MotionLeaving => motionLeaving;

    public Rect? MotionOrigin => motionOrigin;

    public bool CanOpen(string appletId) => applets.ContainsKey(appletId);

    public void Open(string appletId) => Open(appletId, null, null);

    public void Open(string appletId, string routeHint) => Open(appletId, routeHint, null);

    public void OpenFrom(string appletId, Rect originTile) => Open(appletId, null, originTile);

    public void Back()
    {
        CancelMotion();
        if (history.Count == 0)
        {
            return;
        }

        var leaving = history.Pop();
        current = history.Count > 0 ? history.Peek() : null;
        leaving.Leave();
        current?.Enter(AppletEntry.Plain);
        if (current is null)
        {
            ReturnedHome?.Invoke();
        }
    }

    public void Home()
    {
        CancelMotion();
        while (history.Count > 0)
        {
            history.Pop().Leave();
        }

        current = null;
        ReturnedHome?.Invoke();
    }

    public void Recents() => recentsWanted = true;

    public bool TakeRecents()
    {
        if (!recentsWanted)
        {
            return false;
        }

        recentsWanted = false;
        return true;
    }

    public void Advance(float deltaSeconds, float durationSeconds)
    {
        if (motion == ShellMotion.None || durationSeconds <= 0f)
        {
            return;
        }

        motionProgress = Math.Clamp(motionProgress + deltaSeconds / durationSeconds, 0f, 1f);
        if (motionProgress < 1f)
        {
            return;
        }

        motion = ShellMotion.None;
        motionEntering = null;
        motionLeaving = null;
        motionOrigin = null;
        motionProgress = 0f;
    }

    private void CancelMotion()
    {
        motion = ShellMotion.None;
        motionEntering = null;
        motionLeaving = null;
        motionOrigin = null;
        motionProgress = 0f;
    }

    private void Open(string appletId, string? routeHint, Rect? originTile)
    {
        if (!applets.TryGetValue(appletId, out var applet))
        {
            return;
        }

        if (ReferenceEquals(applet, current))
        {
            return;
        }

        CancelMotion();
        history.Push(applet);
        current = applet;
        RememberRecent(appletId);
        applet.Enter(new AppletEntry(routeHint, originTile));
        Opened?.Invoke(appletId);
    }

    private void RememberRecent(string appletId)
    {
        recents.Remove(appletId);
        recents.Insert(0, appletId);
        if (recents.Count > 8)
        {
            recents.RemoveAt(recents.Count - 1);
        }
    }
}
