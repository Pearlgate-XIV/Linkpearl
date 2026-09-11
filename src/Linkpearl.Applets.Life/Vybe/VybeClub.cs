using Linkpearl.Applets.Life.Calendar;
using Linkpearl.Net;

namespace Linkpearl.Applets.Life.Vybe;

internal sealed class VybeClub
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string About { get; set; } = string.Empty;

    public string FacePath { get; set; } = string.Empty;

    public bool PlusOnly { get; set; }

    public int Members { get; set; } = 1;

    public long BornUnix { get; set; }

    public List<PearlPost> Posts { get; set; } = new();
}

internal static class VybeClubs
{
    public const string CalendarSource = "vybe";

    public static string EventPrefix(string clubId) => "vg:" + clubId + ":";

    public static string Handle(string id) => "group:" + id;

    public static bool TryFind(VybeState state, string id, out VybeClub club)
    {
        club = null!;
        if (id.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < state.Clubs.Count; index++)
        {
            if (!string.Equals(state.Clubs[index].Id, id, StringComparison.Ordinal))
            {
                continue;
            }

            club = state.Clubs[index];
            return true;
        }

        return false;
    }

    public static bool Owns(VybeState state, string id) => TryFind(state, id, out _);

    public static SceneGroup Scene(VybeClub club) =>
        new(club.Id, club.Name, club.PlusOnly ? "Plus" : "Yours", Math.Max(1, club.Members),
            club.FacePath, true, false, club.PlusOnly);

    public static SceneGroup? SceneOf(VybeState state, string id)
    {
        if (TryFind(state, id, out var club))
        {
            return Scene(club);
        }

        for (var index = 0; index < VybeGroups.Catalog.Length; index++)
        {
            if (string.Equals(VybeGroups.Catalog[index].Id, id, StringComparison.Ordinal))
            {
                return VybeGroups.Catalog[index];
            }
        }

        return null;
    }

    public static PearlPost[] Wall(VybeState state, PearlPost[] demo)
    {
        var wall = new List<PearlPost>();
        for (var index = 0; index < state.Clubs.Count; index++)
        {
            wall.AddRange(state.Clubs[index].Posts);
        }

        wall.AddRange(demo);
        return wall.ToArray();
    }

    public static PearlPost[] PostsFor(VybeState state, string id, PearlPost[] demo)
    {
        var hits = new List<PearlPost>();
        var handle = Handle(id);
        if (TryFind(state, id, out var club))
        {
            hits.AddRange(club.Posts);
        }

        for (var index = 0; index < demo.Length; index++)
        {
            if (string.Equals(demo[index].AuthorHandle, handle, StringComparison.Ordinal))
            {
                hits.Add(demo[index]);
            }
        }

        return hits.ToArray();
    }

    public static IReadOnlyList<CalendarItem> Events(CalendarBook calendar, string clubId)
    {
        var prefix = EventPrefix(clubId);
        var hits = new List<CalendarItem>();
        foreach (var item in calendar.Items)
        {
            if (string.Equals(item.Source, CalendarSource, StringComparison.Ordinal) &&
                item.Id.StartsWith(prefix, StringComparison.Ordinal))
            {
                hits.Add(item);
            }
        }

        hits.Sort(static (a, b) => a.StartsAt.CompareTo(b.StartsAt));
        return hits;
    }

    public static void PutEvent(CalendarBook calendar, string clubId, string title, DateTimeOffset starts)
    {
        calendar.Put(new CalendarItem
        {
            Id = EventPrefix(clubId) + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            Kind = CalendarKind.Event,
            Title = title,
            StartsAt = starts,
            RemindMinutes = 30,
            Source = CalendarSource,
        });
    }
}
