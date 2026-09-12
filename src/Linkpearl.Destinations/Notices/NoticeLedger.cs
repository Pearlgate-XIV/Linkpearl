using System.Globalization;
using Linkpearl.Net;
using Linkpearl.Notices;
using Linkpearl.Talk;
using Linkpearl.Time;

namespace Linkpearl.Destinations;

public enum NoticeKind : byte
{
    Announcement = 0,
    Chat = 1,
    People = 2,
    Music = 3,
    Calendar = 4,
    Venue = 5,
    Staff = 6,
}

public static class NoticeMarks
{
    public static string For(NoticeKind kind) => kind switch
    {
        NoticeKind.People => "friends",
        NoticeKind.Announcement => "events",
        NoticeKind.Music => "music",
        NoticeKind.Calendar => "calendar",
        NoticeKind.Venue => "venues",
        NoticeKind.Staff => "events",
        _ => "pearlchat",
    };
}

public readonly record struct GlassNotice(
    string Id,
    NoticeKind Kind,
    string Title,
    string Detail,
    string When,
    DestinationTab Tab,
    int Section,
    string TargetId);

public sealed class NoticeLedger : INoticeTray
{
    private const int Cap = 24;
    private readonly List<GlassNotice> tray = [];
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private readonly List<GlassNotice> arrived = [];
    private bool seeded;
    private int lastUnread;

    public IReadOnlyList<GlassNotice> Visible(PearlSnapshot snapshot, ITalk talk, IClock clock)
    {
        Ingest(snapshot, talk, clock);
        return tray;
    }

    public int Count(PearlSnapshot snapshot, ITalk talk, IClock clock)
    {
        Ingest(snapshot, talk, clock);
        return tray.Count;
    }

    public int AppBadge(string appId, ITalk talk)
    {
        if (appId.Length == 0)
        {
            return 0;
        }

        if (string.Equals(appId, "pearlchat", StringComparison.Ordinal))
        {
            return Math.Max(0, talk.UnreadTotal);
        }

        var kind = KindOf(appId);
        if (kind is not { } match)
        {
            return 0;
        }

        var count = 0;
        for (var index = 0; index < tray.Count; index++)
        {
            if (tray[index].Kind == match)
            {
                count++;
            }
        }

        return count;
    }

    private static NoticeKind? KindOf(string appId) => appId switch
    {
        "friends" => NoticeKind.People,
        "music" => NoticeKind.Music,
        "announcements" => NoticeKind.Announcement,
        "calendar" => NoticeKind.Calendar,
        "venues" => NoticeKind.Venue,
        _ => null,
    };

    public void Ingest(PearlSnapshot snapshot, ITalk talk, IClock clock)
    {
        IngestStaff(snapshot, clock);
        var unread = talk.UnreadTotal + snapshot.UnreadTotal;
        if (!seeded)
        {
            var notices = snapshot.Announcements ?? [];
            for (var index = 0; index < notices.Length; index++)
            {
                seen.Add("ann:" + notices[index].Id);
            }

            if (snapshot.People.Length > 0)
            {
                seen.Add("people:" + snapshot.People[0].Id);
            }

            lastUnread = unread;
            seeded = true;
            return;
        }

        var posted = snapshot.Announcements ?? [];
        for (var index = 0; index < posted.Length; index++)
        {
            var item = posted[index];
            var id = "ann:" + item.Id;
            if (!seen.Add(id))
            {
                continue;
            }

            Keep(new GlassNotice(id, NoticeKind.Announcement, item.Title, Snippet(item.Body),
                Stamp(clock), DestinationTab.Home, HomePane.Announcements, item.Id));
        }

        if (unread > lastUnread)
        {
            var title = "Messages";
            var detail = "New message";
            var target = string.Empty;
            var inbox = talk.Inbox();
            for (var index = 0; index < inbox.Count; index++)
            {
                if (inbox[index].Unread <= 0)
                {
                    continue;
                }

                title = inbox[index].Title.Length > 0 ? inbox[index].Title : title;
                detail = inbox[index].Preview.Length > 0 ? inbox[index].Preview : detail;
                target = inbox[index].Id;
                break;
            }

            var id = "chat:" + target + ":" + unread + ":" + tray.Count.ToString(CultureInfo.InvariantCulture);
            Keep(new GlassNotice(id, NoticeKind.Chat, title, detail, Stamp(clock),
                DestinationTab.Social, SocialPane.Messages, target));
        }

        lastUnread = unread;
        if (snapshot.People.Length > 0)
        {
            var person = snapshot.People[0];
            var id = "people:" + person.Id;
            if (seen.Add(id))
            {
                Keep(new GlassNotice(id, NoticeKind.People, person.DisplayName,
                    person.IsMutual ? "On the glass" : person.Handle, Stamp(clock),
                    DestinationTab.Social, SocialPane.People, TalkIds.Person(person.Id)));
            }
        }
    }

    private void IngestStaff(PearlSnapshot snapshot, IClock clock)
    {
        var notices = snapshot.StaffNotices ?? [];
        for (var index = 0; index < notices.Length; index++)
        {
            var item = notices[index];
            if (item.Read || item.Id.Length == 0)
            {
                continue;
            }

            var id = "staff:" + item.Id;
            if (!seen.Add(id))
            {
                continue;
            }

            Keep(new GlassNotice(id, NoticeKind.Staff, item.Title, Snippet(item.Body),
                Stamp(clock), DestinationTab.Settings, 0, item.Id));
        }
    }

    public GlassNotice? TakeArrival()
    {
        if (arrived.Count == 0)
        {
            return null;
        }

        var notice = arrived[^1];
        arrived.Clear();
        return notice;
    }

    public void ForgetArrivals() => arrived.Clear();

    public void PostCalendar(string itemId, string title, string detail, IClock clock)
    {
        var id = "cal:" + itemId + ":" + clock.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        seen.Add(id);
        Keep(new GlassNotice(id, NoticeKind.Calendar, title.Length > 0 ? title : "Calendar",
            Snippet(detail), Stamp(clock), DestinationTab.Home, HomePane.Dashboard, itemId));
    }

    public void PostMusic(string stationId, string title, string detail, IClock clock)
    {
        var target = stationId.Trim();
        if (target.Length == 0)
        {
            return;
        }

        var id = "music:live:" + target + ":" + clock.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        seen.Add(id);
        Keep(new GlassNotice(id, NoticeKind.Music, title.Length > 0 ? title : "Live",
            Snippet(detail), Stamp(clock), DestinationTab.Home, HomePane.Dashboard, target));
    }

    public void PostVenue(string venueId, string title, string detail, IClock clock)
    {
        var target = venueId.Trim();
        if (target.Length == 0)
        {
            return;
        }

        var id = "venue:" + target + ":" + clock.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        seen.Add(id);
        Keep(new GlassNotice(id, NoticeKind.Venue, title.Length > 0 ? title : "Venue",
            Snippet(detail), Stamp(clock), DestinationTab.Home, HomePane.Dashboard, target));
    }

    public void Dismiss(string id)
    {
        if (id.Length == 0)
        {
            return;
        }

        for (var index = tray.Count - 1; index >= 0; index--)
        {
            if (string.Equals(tray[index].Id, id, StringComparison.Ordinal))
            {
                tray.RemoveAt(index);
            }
        }
    }

    public void Clear(PearlSnapshot snapshot, ITalk talk)
    {
        tray.Clear();
    }

    private void Keep(in GlassNotice notice)
    {
        tray.Insert(0, notice);
        arrived.Add(notice);
        while (tray.Count > Cap)
        {
            tray.RemoveAt(tray.Count - 1);
        }
    }

    private static string Snippet(string body)
    {
        var text = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 72 ? text : text[..72].TrimEnd() + "...";
    }

    private static string Stamp(IClock clock) =>
        clock.Now.ToString("h:mm tt", CultureInfo.InvariantCulture);
}
