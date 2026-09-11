using Linkpearl.Audio;
using Linkpearl.Net;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Music;

internal readonly record struct MusicPerson(
    string Id,
    string Name,
    string Handle,
    string Bio,
    string Role,
    string Station,
    string Genre,
    bool Live,
    string StreamUrl,
    int Listeners,
    bool Mine,
    string TimeZoneId = "",
    string TwitchLogin = "",
    int Viewers = 0,
    string WatchUrl = "");

internal static class MusicRoster
{
    public static string SelfId(PearlSnapshot pearl) =>
        pearl.MeId.Length > 0 ? pearl.MeId : "me";

    public static MusicPerson Self(MusicState state, PearlSnapshot pearl, bool broadcasting, string shownName = "",
        string timeZoneId = "")
    {
        var role = state.Dj ? "DJ" : state.Venue ? "Venue" : "Listener";
        var name = shownName.Length > 0 ? shownName : (state.DisplayName.Length > 0 ? state.DisplayName : "Listener");
        return new MusicPerson(
            SelfId(pearl),
            name,
            state.Handle,
            state.Bio.Length > 0 ? state.Bio : pearl.MeBio,
            role,
            state.StationName,
            state.Dj ? state.StationGenreLine : state.Genre,
            broadcasting,
            string.Empty,
            0,
            true,
            timeZoneId);
    }

    public static MusicPerson FromLive(CommunityStation station) =>
        new(
            "live:" + station.Id,
            station.Host.Length > 0 ? station.Host : station.Name,
            HandleOf(station.Host.Length > 0 ? station.Host : station.Name),
            station.Bio.Length > 0 ? station.Bio : station.Live ? "On air now." : "Offline. Station is listed until they go live.",
            "DJ",
            station.Name,
            station.Genre,
            station.Live,
            station.ListenUrl,
            station.Listeners,
            false,
            WorldZones.PickFor("live:" + station.Id),
            station.TwitchLogin,
            station.Viewers,
            station.WatchUrl);

    public static MusicPerson FromPearl(PearlPerson person) =>
        new(
            person.Id,
            person.DisplayName.Length > 0 ? person.DisplayName : person.Handle,
            person.Handle.Length > 0 ? person.Handle : HandleOf(person.DisplayName),
            person.IsMutual ? "Mutual on Pearlgate." : "Listener on Pearlgate.",
            "Listener",
            string.Empty,
            string.Empty,
            false,
            string.Empty,
            0,
            false,
            person.TimeZoneId);

    public static IReadOnlyList<MusicPerson> Directory(MusicState state, PearlSnapshot pearl,
        IReadOnlyList<CommunityStation> live, IReadOnlyList<CommunityStation> mine,
        IReadOnlyList<CommunityStation> listed, bool broadcasting, string shownName = "",
        string timeZoneId = "")
    {
        var byId = new Dictionary<string, MusicPerson>(StringComparer.OrdinalIgnoreCase);
        var self = Self(state, pearl, broadcasting, shownName, timeZoneId);
        byId[self.Id] = self;
        AddLive(byId, live);
        AddLive(byId, mine);
        AddLive(byId, listed);
        for (var index = 0; index < pearl.People.Length; index++)
        {
            var person = FromPearl(pearl.People[index]);
            if (person.Id.Length == 0 || string.Equals(person.Id, self.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!byId.ContainsKey(person.Id))
            {
                byId[person.Id] = person;
            }
        }

        return byId.Values
            .OrderByDescending(static row => row.Mine)
            .ThenByDescending(static row => row.Live)
            .ThenBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static MusicPerson? Find(IReadOnlyList<MusicPerson> people, string id)
    {
        for (var index = 0; index < people.Count; index++)
        {
            if (string.Equals(people[index].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return people[index];
            }
        }

        return null;
    }

    public static IReadOnlyList<MusicPerson> Filter(IReadOnlyList<MusicPerson> people, string query)
    {
        var text = query.Trim();
        if (text.Length == 0)
        {
            return people;
        }

        return people.Where(row =>
                row.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                row.Handle.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                row.Station.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                row.Role.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                row.TwitchLogin.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static void AddLive(Dictionary<string, MusicPerson> byId, IReadOnlyList<CommunityStation> rows)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            var person = FromLive(rows[index]);
            if (person.Id.Length == 0)
            {
                continue;
            }

            if (byId.TryGetValue(person.Id, out var prior) && prior.Live)
            {
                continue;
            }

            byId[person.Id] = person;
        }
    }

    private static string HandleOf(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return "@dj";
        }

        return "@" + trimmed.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }
}
