using System.Globalization;
using Linkpearl.Diagnostics;
using Linkpearl.Modules;
using Linkpearl.Persistence;

namespace Linkpearl.Applets.Life.Calendar;

public enum CalendarKind : byte
{
    Event = 0,
    Reminder = 1,
}

public sealed class CalendarItem
{
    public string Id { get; set; } = "";
    public CalendarKind Kind { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public int RemindMinutes { get; set; } = -1;
    public string LastFired { get; set; } = "";

    public string Source { get; set; } = "";
}

public readonly record struct CalendarAgendaLine(string Id, string Title, string When, string Kind);

public sealed class CalendarBook : IDisposable
{
    private readonly HostPaths paths;
    private readonly ILinkpearlLog log;
    private string file = string.Empty;
    private ulong bound;
    private readonly List<CalendarItem> items = [];
    private JsonCorruptHold corrupt;
    private bool dirty;

    public CalendarBook(HostPaths paths, ILinkpearlLog log)
    {
        this.paths = paths;
        this.log = log;
    }

    public ulong BoundId => bound;

    public IReadOnlyList<CalendarItem> Items => items;

    public byte[] DayDots(DateTime month)
    {
        var dots = new byte[32];
        for (var index = 0; index < items.Count; index++)
        {
            var stamp = items[index].StartsAt.LocalDateTime;
            if (stamp.Year != month.Year || stamp.Month != month.Month)
            {
                continue;
            }

            if (dots[stamp.Day] < 4)
            {
                dots[stamp.Day]++;
            }
        }

        return dots;
    }

    public int DotsOn(DateTime day)
    {
        var date = day.Date;
        var count = 0;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].StartsAt.LocalDateTime.Date != date)
            {
                continue;
            }

            count++;
            if (count >= 4)
            {
                return 4;
            }
        }

        return count;
    }

    public IReadOnlyList<CalendarAgendaLine> ForDay(DateTime day)
    {
        var date = day.Date;
        var lines = new List<CalendarAgendaLine>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.StartsAt.LocalDateTime.Date != date)
            {
                continue;
            }

            lines.Add(new CalendarAgendaLine(item.Id, ShownTitle(item),
                item.StartsAt.ToLocalTime().ToString("h:mm tt", CultureInfo.CurrentCulture),
                VenueKind(item)));
        }

        lines.Sort(static (a, b) => string.CompareOrdinal(a.When, b.When));
        return lines;
    }

    public CalendarItem? Find(string id)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (string.Equals(items[index].Id, id, StringComparison.Ordinal))
            {
                return items[index];
            }
        }

        return null;
    }

    public int TodayCount(DateTimeOffset now)
    {
        var date = now.Date;
        var count = 0;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].StartsAt.LocalDateTime.Date == date)
            {
                count++;
            }
        }

        return count;
    }

    public void Put(CalendarItem item)
    {
        if (item.Id.Length == 0)
        {
            item.Id = Guid.NewGuid().ToString("N");
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (!string.Equals(items[index].Id, item.Id, StringComparison.Ordinal))
            {
                continue;
            }

            items[index] = item;
            dirty = true;
            Save();
            return;
        }

        items.Add(item);
        dirty = true;
        Save();
    }

    public void Drop(string id)
    {
        for (var index = items.Count - 1; index >= 0; index--)
        {
            if (string.Equals(items[index].Id, id, StringComparison.Ordinal))
            {
                items.RemoveAt(index);
                dirty = true;
            }
        }

        Save();
    }

    public bool HoldsPrefixed(string prefix)
    {
        if (prefix.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Id.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public void DropPrefixed(string prefix)
    {
        if (prefix.Length == 0)
        {
            return;
        }

        var dirty = false;
        for (var index = items.Count - 1; index >= 0; index--)
        {
            if (!items[index].Id.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            items.RemoveAt(index);
            dirty = true;
        }

        if (dirty)
        {
            this.dirty = true;
            Save();
        }
    }

    public void DropFuturePrefixed(string prefix, DateTimeOffset now)
    {
        KeepFuturePrefixed(prefix, now, null);
    }

    public void KeepFuturePrefixed(string prefix, DateTimeOffset now, HashSet<string>? keep)
    {
        if (prefix.Length == 0)
        {
            return;
        }

        var changed = false;
        for (var index = items.Count - 1; index >= 0; index--)
        {
            var item = items[index];
            if (!item.Id.StartsWith(prefix, StringComparison.Ordinal) || item.StartsAt <= now)
            {
                continue;
            }

            if (keep is not null && keep.Contains(item.Id))
            {
                continue;
            }

            items.RemoveAt(index);
            changed = true;
        }

        if (changed)
        {
            dirty = true;
            Save();
        }
    }

    public bool FireDue(DateTimeOffset now, Action<CalendarItem> due)
    {
        var changed = false;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.RemindMinutes < 0)
            {
                continue;
            }

            var fireAt = item.StartsAt.ToLocalTime() - TimeSpan.FromMinutes(item.RemindMinutes);
            if (now < fireAt || now - fireAt > TimeSpan.FromHours(18))
            {
                continue;
            }

            var stamp = fireAt.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
            if (string.Equals(item.LastFired, stamp, StringComparison.Ordinal))
            {
                continue;
            }

            item.LastFired = stamp;
            changed = true;
            due(item);
        }

        if (changed)
        {
            dirty = true;
            Save();
        }

        return changed;
    }

    public bool Flush()
    {
        if (file.Length == 0)
        {
            dirty = false;
            return true;
        }

        if (!dirty)
        {
            return true;
        }

        Save();
        return !dirty;
    }

    public bool Bind(ulong contentId)
    {
        if (contentId == bound)
        {
            return true;
        }

        if (!Flush())
        {
            return false;
        }

        items.Clear();
        corrupt = default;
        dirty = false;
        bound = contentId;
        file = CharacterStatePaths.Calendar(paths, contentId);
        if (file.Length > 0)
        {
            Load();
        }

        return true;
    }

    public void Dispose()
    {
        Flush();
    }

    public static string ShownTitle(CalendarItem item)
    {
        if (item.Title.Trim().Length > 0)
        {
            return item.Title.Trim();
        }

        return item.Kind == CalendarKind.Reminder ? "Reminder" : "Event";
    }

    private static string VenueKind(CalendarItem item)
    {
        if (item.Id.StartsWith("vn:", StringComparison.Ordinal) ||
            item.Id.StartsWith("vs:", StringComparison.Ordinal))
        {
            return "Venue";
        }

        return item.Kind == CalendarKind.Reminder ? "Reminder" : "Event";
    }

    private void Load()
    {
        if (file.Length == 0)
        {
            return;
        }

        if (!AtomicJson.TryRead(file, null, out List<CalendarItem>? loaded, ref corrupt, log) ||
            loaded is null)
        {
            return;
        }

        items.Clear();
        items.AddRange(loaded);
    }

    private void Save()
    {
        if (file.Length == 0)
        {
            return;
        }

        if (AtomicJson.TrySave(file, items, null, ref corrupt, log))
        {
            dirty = false;
        }
    }
}
