using System.Globalization;
using Dalamud.Interface.Windowing;
using Linkpearl.Talk;
using Linkpearl.Theming;

namespace Linkpearl.Host.Windows;

public sealed class TalkPopoutBoard : ITalkPopouts, IDisposable
{
    internal const string DockPlaceId = "__telldock__";

    private readonly WindowSystem windows;
    private readonly ITalk talk;
    private readonly ITheme theme;
    private readonly Action persist;
    private readonly HashSet<string> armed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TalkPopoutWindow> panes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Vector4> places = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();
    private TellDockWindow? dock;

    public TalkPopoutBoard(WindowSystem windows, ITalk talk, ITheme theme, Action persist)
    {
        this.windows = windows;
        this.talk = talk;
        this.theme = theme;
        this.persist = persist;
        talk.LinePosted += HandleLinePosted;
    }

    public IReadOnlyList<string> ArmedIds
    {
        get
        {
            var ids = new List<string>(armed.Count);
            foreach (var id in armed)
            {
                ids.Add(id);
            }

            return ids;
        }
    }

    public IReadOnlyList<string> PlaceBlobs
    {
        get
        {
            RememberDockPlace();
            var list = new List<string>(places.Count);
            foreach (var pair in places)
            {
                list.Add(FormatPlace(pair.Key, pair.Value));
            }

            return list;
        }
    }

    public bool IsArmed(string threadId) =>
        threadId.Length > 0 && armed.Contains(threadId);

    public void Toggle(string threadId) => SetArmed(threadId, !IsArmed(threadId));

    public void SetArmed(string threadId, bool armedNow)
    {
        if (threadId.Length == 0)
        {
            return;
        }

        if (armedNow)
        {
            var added = armed.Add(threadId);
            if (IsTell(threadId))
            {
                ShowTell(threadId, select: true);
            }
            else
            {
                Show(threadId);
            }

            if (added)
            {
                persist();
            }

            return;
        }

        var changed = armed.Remove(threadId);
        if (IsTell(threadId))
        {
            dock?.CloseTab(threadId);
        }
        else
        {
            Hide(threadId, forgetPlace: false);
        }

        lock (gate)
        {
            pending.Remove(threadId);
        }

        if (changed)
        {
            persist();
        }
    }

    public void Restore(IEnumerable<string> ids, IEnumerable<string>? placeBlobs = null)
    {
        if (placeBlobs != null)
        {
            foreach (var blob in placeBlobs)
            {
                if (TryParsePlace(blob, out var id, out var place))
                {
                    places[id] = place;
                }
            }
        }

        var tells = new List<string>();
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var trimmed = id.Trim();
            armed.Add(trimmed);
            if (IsTell(trimmed))
            {
                tells.Add(trimmed);
            }
        }

        if (tells.Count == 0)
        {
            return;
        }

        var pane = EnsureDock();
        for (var index = 0; index < tells.Count; index++)
        {
            pane.OpenTab(tells[index], select: index == 0);
        }

        pane.IsOpen = false;
    }

    public void Pulse()
    {
        List<string> reopen;
        lock (gate)
        {
            if (pending.Count == 0)
            {
                return;
            }

            reopen = pending.ToList();
            pending.Clear();
        }

        for (var index = 0; index < reopen.Count; index++)
        {
            var id = reopen[index];
            if (!armed.Contains(id))
            {
                continue;
            }

            if (IsTell(id))
            {
                var hidden = dock is not { IsOpen: true } || dock.Has(id) != true;
                ShowTell(id, select: hidden);
                continue;
            }

            Show(id);
        }
    }

    public void Dispose()
    {
        talk.LinePosted -= HandleLinePosted;
        foreach (var pane in panes.Values)
        {
            pane.IsOpen = false;
            windows.RemoveWindow(pane);
        }

        panes.Clear();
        armed.Clear();
        if (dock != null)
        {
            RememberDockPlace();
            dock.IsOpen = false;
            windows.RemoveWindow(dock);
            dock = null;
        }

        lock (gate)
        {
            pending.Clear();
        }
    }

    private void HandleLinePosted(string threadId, TalkLine line)
    {
        if (line.Mine || threadId.Length == 0)
        {
            return;
        }

        if (!armed.Contains(threadId))
        {
            return;
        }

        lock (gate)
        {
            pending.Add(threadId);
        }
    }

    private void ShowTell(string threadId, bool select)
    {
        var pane = EnsureDock();
        pane.OpenTab(threadId, select);
        if (select)
        {
            pane.Snap(threadId);
        }
    }

    private TellDockWindow EnsureDock()
    {
        if (dock != null)
        {
            return dock;
        }

        places.TryGetValue(DockPlaceId, out var place);
        var hasPlace = places.ContainsKey(DockPlaceId);
        dock = new TellDockWindow(talk, theme, persist, HandleDockClosed,
            hasPlace ? new Vector2(place.X, place.Y) : null,
            hasPlace ? new Vector2(place.Z, place.W) : null);
        windows.AddWindow(dock);
        return dock;
    }

    private void HandleDockClosed()
    {
        RememberDockPlace();
        persist();
    }

    private void RememberDockPlace()
    {
        if (dock == null)
        {
            return;
        }

        var pos = dock.LastPos;
        var size = dock.LastSize;
        if (size.X < 40f || size.Y < 40f)
        {
            return;
        }

        places[DockPlaceId] = new Vector4(pos.X, pos.Y, size.X, size.Y);
    }

    private void Show(string threadId)
    {
        if (panes.ContainsKey(threadId))
        {
            var existing = panes[threadId];
            existing.IsOpen = true;
            existing.SnapToNewest();
            return;
        }

        places.TryGetValue(threadId, out var place);
        var hasPlace = places.ContainsKey(threadId);
        var pane = new TalkPopoutWindow(threadId, talk, theme, HandleClosed,
            hasPlace ? new Vector2(place.X, place.Y) : null,
            hasPlace ? new Vector2(place.Z, place.W) : null);
        panes[threadId] = pane;
        windows.AddWindow(pane);
    }

    private void Hide(string threadId, bool forgetPlace)
    {
        if (!panes.Remove(threadId, out var existing))
        {
            return;
        }

        RememberPlace(existing);
        existing.IsOpen = false;
        windows.RemoveWindow(existing);
        if (forgetPlace)
        {
            places.Remove(threadId);
        }
    }

    private void HandleClosed(string threadId)
    {
        if (!panes.Remove(threadId, out var pane))
        {
            return;
        }

        RememberPlace(pane);
        windows.RemoveWindow(pane);
        persist();
    }

    private void RememberPlace(TalkPopoutWindow pane)
    {
        var pos = pane.LastPos;
        var size = pane.LastSize;
        if (size.X < 40f || size.Y < 40f)
        {
            return;
        }

        places[pane.ThreadId] = new Vector4(pos.X, pos.Y, size.X, size.Y);
    }

    private bool IsTell(string threadId) =>
        TalkIds.TryParseTell(threadId, out _, out _) || talk.Find(threadId)?.Kind == TalkKind.Tell;

    private static string FormatPlace(string id, Vector4 place) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{id}|{place.X:0.##}|{place.Y:0.##}|{place.Z:0.##}|{place.W:0.##}");

    private static bool TryParsePlace(string blob, out string id, out Vector4 place)
    {
        id = string.Empty;
        place = default;
        var parts = (blob ?? string.Empty).Split('|');
        if (parts.Length != 5 || parts[0].Length == 0)
        {
            return false;
        }

        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ||
            !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
        {
            return false;
        }

        id = parts[0];
        place = new Vector4(x, y, w, h);
        return true;
    }
}
