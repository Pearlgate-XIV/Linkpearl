using System.Globalization;
using Dalamud.Interface.Windowing;
using Linkpearl.Talk;
using Linkpearl.Theming;

namespace Linkpearl.Host.Windows;

public sealed class TalkPopoutBoard : ITalkPopouts, IDisposable
{
    private readonly WindowSystem windows;
    private readonly ITalk talk;
    private readonly ITheme theme;
    private readonly Action persist;
    private readonly HashSet<string> armed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TalkPopoutWindow> panes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Vector4> places = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();

    public TalkPopoutBoard(WindowSystem windows, ITalk talk, ITheme theme, Action persist)
    {
        this.windows = windows;
        this.talk = talk;
        this.theme = theme;
        this.persist = persist;
        talk.LinePosted += HandleLinePosted;
    }

    public IReadOnlyList<string> ArmedIds => armed.ToArray();

    public IReadOnlyList<string> PlaceBlobs
    {
        get
        {
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
            if (!armed.Add(threadId))
            {
                Show(threadId);
                return;
            }

            Show(threadId);
            persist();
            return;
        }

        var changed = armed.Remove(threadId);
        Hide(threadId, forgetPlace: false);
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

        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            armed.Add(id.Trim());
        }
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
            if (armed.Contains(id))
            {
                Show(id);
            }
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
        lock (gate)
        {
            pending.Clear();
        }
    }

    private void HandleLinePosted(string threadId, TalkLine line)
    {
        if (line.Mine || threadId.Length == 0 || !armed.Contains(threadId))
        {
            return;
        }

        lock (gate)
        {
            pending.Add(threadId);
        }
    }

    private void Show(string threadId)
    {
        if (panes.ContainsKey(threadId))
        {
            var existing = panes[threadId];
            existing.IsOpen = true;
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
