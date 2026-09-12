using System.Collections.Generic;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Music;

public sealed partial class MusicApplet
{
    private IReadOnlyList<MusicPerson> Roster() =>
        MusicRoster.Directory(state, pearl.Current, MergedLive(), community.Mine, community.Directory,
            community.Broadcasting, MarkedName(), display.OwnTimeZoneId);

    private void DrawPersonSection(in AppletFrame frame, ref Stack stack, string title, IReadOnlyList<MusicPerson> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), title);
        for (var index = 0; index < rows.Count; index++)
        {
            DrawPersonRow(frame, stack.Take(frame.Units(56f)), rows[index]);
        }
    }

    private void DrawPersonRow(in AppletFrame frame, Rect row, MusicPerson person)
    {
        MusicChrome.GlowPlate(frame, row, frame.Units(12f), person.Live || person.Mine);
        var inset = row.Inset(frame.Units(8f));
        var face = inset.LeftSlice(frame.Units(36f));
        frame.Paint.FillCircle(face.Center, frame.Units(14f), MusicChrome.Purple);
        frame.Text.DrawIn(face, person.Name.Length > 0 ? char.ToUpperInvariant(person.Name[0]).ToString() : "♪",
            new TextStyle(FontRole.CaptionStrong, MusicChrome.GroundHi, TextAlign.Center));
        var follow = !person.Mine ? inset.RightSlice(frame.Units(108f)).LeftSlice(frame.Units(78f)) : Rect.Empty;
        var flag = !person.Mine ? inset.RightSlice(frame.Units(28f)) : Rect.Empty;
        var body = inset.Inset(new Edges(frame.Units(42f), 0f, person.Mine ? 0f : frame.Units(112f), 0f));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), person.Name,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        var zone = WorldZones.ForPerson(person.TimeZoneId, person.Id);
        var time = zone.Length > 0 || person.Mine
            ? ZoneClock.Stamp(person.Mine ? display.OwnTimeZoneId : zone, display.Use24HourClock)
            : string.Empty;
        var detail = person.Handle + " · " + person.Role + (person.Live ? " · Live" : string.Empty);
        if (time.Length > 0)
        {
            detail += " · " + time;
        }

        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(16f)), detail,
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        if (!person.Mine)
        {
            MusicChrome.ReportFlag(frame, flag);
            if (Tap(frame, flag.Expand(frame.Units(4f))))
            {
                OpenPersonReport(person);
                return;
            }

            if (MusicChrome.FollowChip(frame, follow, FollowsProfile(person)))
            {
                FollowPerson(person);
                return;
            }
        }

        if (frame.Input.ConsumeClick(row))
        {
            ShowProfile(person.Id);
        }
    }

    private bool FollowsProfile(MusicPerson person) =>
        state.FollowsProfile(person.Id) ||
        (person.Handle.Length > 0 && state.Following.Contains(person.Handle));

    private int ShownFollowing(MusicPerson person, bool mine) =>
        mine ? state.FollowingCount() : MusicState.PublicFollowing(person.Id);

    private int ShownFollowers(MusicPerson person, bool mine) =>
        mine
            ? Math.Max(pearl.Current.Followers, 0)
            : MusicState.PublicFollowers(person.Id, FollowsProfile(person));

    private void OpenFollowList(bool followers, string ownerId = "")
    {
        state.FollowListFollowers = followers;
        state.FollowListOwnerId = ownerId;
        state.Open(MusicPage.FollowList);
    }

    private bool FollowListIsMine()
    {
        var owner = state.FollowListOwnerId;
        return owner.Length == 0 ||
               string.Equals(owner, MusicRoster.SelfId(pearl.Current), StringComparison.OrdinalIgnoreCase);
    }

    private void DrawFollowList(in AppletFrame frame, Rect area)
    {
        var followers = state.FollowListFollowers;
        var mine = FollowListIsMine();
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), followers ? "Followers" : "Following"))
            {
                state.Back();
                return;
            }

            var rows = followers
                ? mine ? FollowerProfiles() : OtherFollowerProfiles(state.FollowListOwnerId)
                : mine ? FollowingProfiles() : OtherFollowingProfiles(state.FollowListOwnerId);
            var owner = mine ? default(MusicPerson?) : MusicRoster.Find(Roster(), state.FollowListOwnerId);
            var shown = followers && owner is { } person
                ? ShownFollowers(person, false)
                : rows.Count;
            frame.Text.DrawIn(stack.Take(frame.Units(16f)),
                shown == 1 ? "1 profile" : shown + " profiles",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            if (rows.Count == 0)
            {
                DrawHint(frame, ref stack, followers
                    ? mine
                        ? "People who follow you on Pearlgate will land here."
                        : "No one from your circle shows on this list yet."
                    : mine
                        ? "Follow a profile and they show up here."
                        : "Their following is not on your roster yet.");
                return;
            }

            for (var index = 0; index < rows.Count; index++)
            {
                DrawPersonRow(frame, stack.Take(frame.Units(56f)), rows[index]);
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private IReadOnlyList<MusicPerson> FollowingProfiles()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<MusicPerson>();
        var people = Roster();
        for (var index = 0; index < people.Count; index++)
        {
            var person = people[index];
            if (person.Mine || !FollowsProfile(person) || !seen.Add(MusicState.BareStationId(person.Id)))
            {
                continue;
            }

            rows.Add(person);
        }

        foreach (var snap in state.FollowedStations)
        {
            var id = "live:" + snap.Id;
            if (snap.Id.Length == 0 || seen.Contains(snap.Id))
            {
                continue;
            }

            seen.Add(snap.Id);
            rows.Add(new MusicPerson(id, snap.Host.Length > 0 ? snap.Host : snap.Name,
                "@" + (snap.Host.Length > 0 ? snap.Host : snap.Name).Replace(" ", string.Empty,
                    StringComparison.Ordinal).ToLowerInvariant(),
                snap.Bio, "DJ", snap.Name, snap.Genre, false, snap.ListenUrl, 0, false,
                WorldZones.PickFor(id), snap.TwitchLogin, 0, snap.WatchUrl));
        }

        return rows;
    }

    private IReadOnlyList<MusicPerson> FollowerProfiles()
    {
        var mine = MusicRoster.SelfId(pearl.Current);
        var rows = new List<MusicPerson>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mine };
        for (var index = 0; index < pearl.Current.People.Length; index++)
        {
            var person = pearl.Current.People[index];
            if (!person.IsMutual || person.Id.Length == 0 || !seen.Add(person.Id))
            {
                continue;
            }

            rows.Add(MusicRoster.FromPearl(person));
        }

        return rows;
    }

    private IReadOnlyList<MusicPerson> OtherFollowerProfiles(string ownerId)
    {
        var people = Roster();
        var owner = MusicRoster.Find(people, ownerId);
        var rows = new List<MusicPerson>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ownerId, MusicRoster.SelfId(pearl.Current) };
        if (owner is { } person && FollowsProfile(person))
        {
            rows.Add(MusicRoster.Self(state, pearl.Current, community.Broadcasting, MarkedName(),
                display.OwnTimeZoneId));
        }

        AddRosterCircle(rows, seen, people, ownerId, "/followers");
        return rows;
    }

    private IReadOnlyList<MusicPerson> OtherFollowingProfiles(string ownerId)
    {
        var people = Roster();
        var rows = new List<MusicPerson>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ownerId };
        AddRosterCircle(rows, seen, people, ownerId, "/following");
        return rows;
    }

    private static void AddRosterCircle(List<MusicPerson> rows, HashSet<string> seen,
        IReadOnlyList<MusicPerson> people, string ownerId, string salt)
    {
        var picks = new List<MusicPerson>();
        for (var index = 0; index < people.Count; index++)
        {
            var person = people[index];
            if (person.Mine || !seen.Add(person.Id))
            {
                continue;
            }

            picks.Add(person);
        }

        if (picks.Count <= 4)
        {
            rows.AddRange(picks);
            return;
        }

        for (var index = 0; index < picks.Count && rows.Count < 12; index++)
        {
            var person = picks[index];
            var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(ownerId + salt + person.Id);
            if (Math.Abs(hash) % 3 == 0)
            {
                rows.Add(person);
            }
        }

        if (rows.Count == 0 && picks.Count > 0)
        {
            rows.Add(picks[0]);
        }
    }
}
