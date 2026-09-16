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

public readonly struct RecentTask
{
    public string Id { get; }
    public string Place { get; }

    public RecentTask(string id, string place)
    {
        Id = id;
        Place = place;
    }
}

// Owns the applet back-stack and the single in-flight transition. Applets never see this type:
// they see IRouter. Presentation animation lives on the shell layer that draws Current/Motion*.
public sealed class RouteTrail : IRouter
{
    private const int RecentCap = 12;

    private readonly IReadOnlyDictionary<string, IApplet> applets;
    private readonly List<RecentTask> recents = new();
    private readonly HashSet<string> living = new(StringComparer.Ordinal);
    private IApplet? current;
    private IApplet? motionEntering;
    private IApplet? motionLeaving;
    private ShellMotion motion = ShellMotion.None;
    private Rect? motionOrigin;
    private float motionProgress;
    private bool recentsWanted;

    public RouteTrail(IReadOnlyDictionary<string, IApplet> applets)
    {
        this.applets = applets;
    }

    public event Action<string>? Opened;

    public event Action? ReturnedHome;

    public event Action? RecentsChanged;

    public IApplet? Current => current;

    public string? CurrentAppletId => current?.Manifest.Id;

    public IReadOnlyList<string> RecentIds
    {
        get
        {
            var ids = new string[recents.Count];
            for (var index = 0; index < recents.Count; index++)
            {
                ids[index] = recents[index].Id;
            }

            return ids;
        }
    }

    public IReadOnlyList<string> RecentPlaces
    {
        get
        {
            var places = new string[recents.Count];
            for (var index = 0; index < recents.Count; index++)
            {
                places[index] = recents[index].Place;
            }

            return places;
        }
    }

    public IReadOnlyList<RecentTask> Tasks => recents;

    public bool AtHome => current is null && motion == ShellMotion.None;

    public bool IsTransitioning => motion != ShellMotion.None;

    public ShellMotion Motion => motion;

    public float MotionProgress => motionProgress;

    public IApplet? MotionEntering => motionEntering;

    public IApplet? MotionLeaving => motionLeaving;

    public Rect? MotionOrigin => motionOrigin;

    public bool CanOpen(string appletId) =>
        applets.TryGetValue(appletId, out var applet) && applet.Allowed;

    public void RevokeDisallowed()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (current is { Allowed: false })
        {
            ids.Add(current.Manifest.Id);
        }

        foreach (var id in living)
        {
            if (applets.TryGetValue(id, out var applet) && !applet.Allowed)
            {
                ids.Add(id);
            }
        }

        for (var index = 0; index < recents.Count; index++)
        {
            var id = recents[index].Id;
            if (applets.TryGetValue(id, out var applet) && !applet.Allowed)
            {
                ids.Add(id);
            }
        }

        foreach (var id in ids)
        {
            Dismiss(id);
        }
    }

    public void Open(string appletId) => Open(appletId, null, null);

    public void Open(string appletId, string routeHint) => Open(appletId, routeHint, null);

    public void OpenFrom(string appletId, Rect originTile) => Open(appletId, null, originTile);

    public void OpenFrom(string appletId, Rect originTile, string routeHint) =>
        Open(appletId, routeHint, originTile);

    public void Back()
    {
        CancelMotion();
        if (current is null)
        {
            return;
        }

        CaptureCurrent();
        current = null;
        ReturnedHome?.Invoke();
    }

    public void Home()
    {
        CancelMotion();
        CaptureCurrent();
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

    public void CapturePlaces() => CaptureCurrent();

    public void RememberVisit(string id, string place)
    {
        if (string.IsNullOrWhiteSpace(id) || id.StartsWith("folder:", StringComparison.Ordinal))
        {
            return;
        }

        RememberRecent(id, place ?? string.Empty);
        RecentsChanged?.Invoke();
    }

    public string PlaceOf(string appletId)
    {
        if (applets.TryGetValue(appletId, out var applet) && living.Contains(appletId))
        {
            var live = applet.Place;
            if (live.Length > 0)
            {
                return live;
            }
        }

        for (var index = 0; index < recents.Count; index++)
        {
            if (string.Equals(recents[index].Id, appletId, StringComparison.Ordinal))
            {
                return recents[index].Place;
            }
        }

        return string.Empty;
    }

    public void Dismiss(string appletId)
    {
        CancelMotion();
        if (applets.TryGetValue(appletId, out var applet) && living.Remove(appletId))
        {
            applet.Leave();
        }

        if (ReferenceEquals(current, applet) ||
            (current is not null && string.Equals(current.Manifest.Id, appletId, StringComparison.Ordinal)))
        {
            current = null;
            ReturnedHome?.Invoke();
        }

        DropRecent(appletId);
        RecentsChanged?.Invoke();
    }

    public void DismissAll()
    {
        CancelMotion();
        var ids = living.ToArray();
        living.Clear();
        for (var index = 0; index < ids.Length; index++)
        {
            if (applets.TryGetValue(ids[index], out var applet))
            {
                applet.Leave();
            }
        }
        recents.Clear();
        current = null;
        ReturnedHome?.Invoke();
        RecentsChanged?.Invoke();
    }

    public void Restore(IReadOnlyList<string> ids, IReadOnlyList<string> places)
    {
        recents.Clear();
        var count = Math.Min(ids.Count, RecentCap);
        for (var index = 0; index < count; index++)
        {
            var id = ids[index];
            if (string.IsNullOrWhiteSpace(id) || id.StartsWith("folder:", StringComparison.Ordinal))
            {
                continue;
            }

            var place = index < places.Count ? places[index] ?? string.Empty : string.Empty;
            recents.Add(new RecentTask(id, place));
        }
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
        if (!applets.TryGetValue(appletId, out var applet) || !applet.Allowed)
        {
            return;
        }

        CancelMotion();
        CaptureCurrent();
        var saved = routeHint is { Length: > 0 } ? routeHint : PlaceOf(appletId);
        var entry = new AppletEntry(saved.Length > 0 ? saved : routeHint, originTile);
        var alwaysEnter = string.Equals(appletId, "pearlchat", StringComparison.Ordinal);
        if (ReferenceEquals(applet, current))
        {
            RememberRecent(appletId, saved);
            if (alwaysEnter || routeHint is { Length: > 0 })
            {
                applet.Enter(entry);
            }

            RecentsChanged?.Invoke();
            return;
        }

        current = applet;
        RememberRecent(appletId, saved);
        if (alwaysEnter || living.Add(appletId) || routeHint is { Length: > 0 })
        {
            applet.Enter(entry);
        }

        Opened?.Invoke(appletId);
        RecentsChanged?.Invoke();
    }

    private void CaptureCurrent()
    {
        if (current is null)
        {
            return;
        }

        RememberRecent(current.Manifest.Id, current.Place);
        RecentsChanged?.Invoke();
    }

    private void RememberRecent(string appletId, string place)
    {
        recents.RemoveAll(task => string.Equals(task.Id, appletId, StringComparison.Ordinal));
        recents.Insert(0, new RecentTask(appletId, place ?? string.Empty));
        if (recents.Count > RecentCap)
        {
            recents.RemoveAt(recents.Count - 1);
        }
    }

    private void DropRecent(string appletId) =>
        recents.RemoveAll(task => string.Equals(task.Id, appletId, StringComparison.Ordinal));
}
