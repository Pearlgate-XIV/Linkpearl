using System.Globalization;
using Linkpearl.Diagnostics;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Notices;
using Linkpearl.Persistence;
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
    string TargetId,
    NoticeDismissalScope Scope = NoticeDismissalScope.Device,
    string OwnerKey = "");

public sealed class NoticeLedger : INoticeTray
{
    private const int Cap = 24;
    private readonly List<GlassNotice> tray = [];
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private readonly List<GlassNotice> arrived = [];
    private readonly NoticeDismissalBook book;
    private bool quiet = true;
    private ulong bound;
    private string accountKey = string.Empty;
    private long nowUnix;

    public NoticeLedger(HostPaths paths, ILinkpearlLog log)
    {
        book = new NoticeDismissalBook(paths, log);
    }

    public ulong BoundId => bound;

    public NoticeDismissalBook Dismissals => book;

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

    public bool BindCharacter(ulong contentId)
    {
        if (contentId == bound)
        {
            return true;
        }

        DropScope(NoticeDismissalScope.Character);
        bound = contentId;
        quiet = true;
        return true;
    }

    public void Ingest(PearlSnapshot snapshot, ITalk talk, IClock clock)
    {
        nowUnix = clock.UtcNow.ToUnixTimeSeconds();
        BindAccount(snapshot);
        PruneSettled(snapshot, talk);
        OfferAnnouncements(snapshot, clock);
        OfferStaff(snapshot, clock);
        OfferChat(talk, clock);
        quiet = false;
    }

    private void BindAccount(PearlSnapshot snapshot)
    {
        var next = snapshot.SignedIn && snapshot.MeId.Length > 0 ? snapshot.MeId.Trim() : string.Empty;
        if (string.Equals(next, accountKey, StringComparison.Ordinal))
        {
            return;
        }

        for (var index = tray.Count - 1; index >= 0; index--)
        {
            if (tray[index].Scope != NoticeDismissalScope.Account)
            {
                continue;
            }

            seen.Remove(Track(tray[index]));
            tray.RemoveAt(index);
        }

        accountKey = next;
        quiet = true;
    }

    private void OfferAnnouncements(PearlSnapshot snapshot, IClock clock)
    {
        var posted = snapshot.Announcements ?? [];
        var live = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < posted.Length; index++)
        {
            var item = posted[index];
            if (item.Id.Length == 0)
            {
                continue;
            }

            var id = "ann:" + item.Id + ":" + item.CreatedAtUnix.ToString(CultureInfo.InvariantCulture);
            live.Add(id);
            Offer(new GlassNotice(id, NoticeKind.Announcement, item.Title, Snippet(item.Body),
                Stamp(clock), DestinationTab.Home, HomePane.Announcements, item.Id,
                NoticeDismissalScope.Device, string.Empty));
        }

        DropMissing(NoticeKind.Announcement, live);
    }

    private void OfferStaff(PearlSnapshot snapshot, IClock clock)
    {
        var notices = snapshot.StaffNotices ?? [];
        var live = new HashSet<string>(StringComparer.Ordinal);
        var owner = accountKey;
        for (var index = 0; index < notices.Length; index++)
        {
            var item = notices[index];
            if (item.Read || item.Id.Length == 0)
            {
                continue;
            }

            var id = "staff:" + item.Id + ":" + item.CreatedAtUnix.ToString(CultureInfo.InvariantCulture);
            live.Add(id);
            Offer(new GlassNotice(id, NoticeKind.Staff, item.Title, Snippet(item.Body),
                Stamp(clock), DestinationTab.Settings, 0, item.Id, NoticeDismissalScope.Account, owner));
        }

        DropMissing(NoticeKind.Staff, live);
    }

    private void OfferChat(ITalk talk, IClock clock)
    {
        var inbox = talk.Inbox();
        var live = new HashSet<string>(StringComparer.Ordinal);
        var owner = CharacterStatePaths.Hex(bound);
        for (var index = 0; index < inbox.Count; index++)
        {
            var thread = inbox[index];
            if (thread.Unread <= 0 || thread.Id.Length == 0)
            {
                continue;
            }

            var generation = thread.LastAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            var id = "chat:" + thread.Id + ":" + generation;
            live.Add(id);
            var title = thread.Title.Length > 0 ? thread.Title : "Messages";
            var detail = thread.Preview.Length > 0 ? thread.Preview : "New message";
            Offer(new GlassNotice(id, NoticeKind.Chat, title, detail, Stamp(clock),
                DestinationTab.Social, SocialPane.Messages, thread.Id,
                NoticeDismissalScope.Character, owner));
        }

        DropMissing(NoticeKind.Chat, live);
    }

    private void PruneSettled(PearlSnapshot snapshot, ITalk talk)
    {
        var inbox = talk.Inbox();
        for (var index = tray.Count - 1; index >= 0; index--)
        {
            var item = tray[index];
            if (item.Kind != NoticeKind.Chat)
            {
                continue;
            }

            if (snapshot.UnreadTotal + talk.UnreadTotal <= 0)
            {
                tray.RemoveAt(index);
                continue;
            }

            if (item.TargetId.Length == 0)
            {
                continue;
            }

            var unread = 0;
            for (var row = 0; row < inbox.Count; row++)
            {
                if (string.Equals(inbox[row].Id, item.TargetId, StringComparison.Ordinal))
                {
                    unread = inbox[row].Unread;
                    break;
                }
            }

            if (unread <= 0)
            {
                tray.RemoveAt(index);
            }
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

    public void PostCalendar(string itemId, string occurrence, string title, string detail, IClock clock)
    {
        var stamp = occurrence.Trim();
        if (itemId.Length == 0 || stamp.Length == 0)
        {
            return;
        }

        var id = "cal:" + itemId + ":" + stamp;
        var owner = CharacterStatePaths.Hex(bound);
        Offer(new GlassNotice(id, NoticeKind.Calendar, title.Length > 0 ? title : "Calendar",
            Snippet(detail), Stamp(clock), DestinationTab.Home, HomePane.Dashboard, itemId,
            NoticeDismissalScope.Character, owner));
    }

    public void PostMusic(string stationId, string title, string detail, IClock clock)
    {
        var target = stationId.Trim();
        if (target.Length == 0)
        {
            return;
        }

        nowUnix = clock.UtcNow.ToUnixTimeSeconds();
        var id = "music:" + target + ":" + nowUnix.ToString(CultureInfo.InvariantCulture);
        var lifestream = string.Equals(target, "lifestream", StringComparison.Ordinal);
        var scope = lifestream ? NoticeDismissalScope.Device : NoticeDismissalScope.Account;
        var owner = lifestream ? string.Empty : accountKey;
        Offer(new GlassNotice(id, NoticeKind.Music, title.Length > 0 ? title : "Live",
            Snippet(detail), Stamp(clock), DestinationTab.Home, HomePane.Dashboard, target, scope, owner));
    }

    public void PostVenue(string venueId, string occurrence, string title, string detail, IClock clock)
    {
        var target = venueId.Trim();
        var stamp = occurrence.Trim();
        if (target.Length == 0 || stamp.Length == 0)
        {
            return;
        }

        var id = "venue:" + target + ":" + stamp;
        var owner = CharacterStatePaths.Hex(bound);
        Offer(new GlassNotice(id, NoticeKind.Venue, title.Length > 0 ? title : "Venue",
            Snippet(detail), Stamp(clock), DestinationTab.Home, HomePane.Dashboard, target,
            NoticeDismissalScope.Character, owner));
    }

    public bool TryGet(string id, out GlassNotice item)
    {
        if (id.Length > 0)
        {
            for (var index = 0; index < tray.Count; index++)
            {
                if (string.Equals(tray[index].Id, id, StringComparison.Ordinal))
                {
                    item = tray[index];
                    return true;
                }
            }
        }

        item = default;
        return false;
    }

    public void Dismiss(string id)
    {
        if (id.Length == 0)
        {
            return;
        }

        for (var index = tray.Count - 1; index >= 0; index--)
        {
            if (!Matches(tray[index].Id, id))
            {
                continue;
            }

            var item = tray[index];
            tray.RemoveAt(index);
            seen.Add(Track(item));
            PersistDismiss(item);
        }
    }

    public void Clear(PearlSnapshot snapshot, ITalk talk)
    {
        tray.Clear();
    }

    private void Offer(in GlassNotice notice)
    {
        if (book.Holds(notice.Id, notice.Scope, notice.OwnerKey))
        {
            seen.Add(Track(notice));
            return;
        }

        if (!seen.Add(Track(notice)))
        {
            return;
        }

        Keep(notice, toast: !quiet);
    }

    private void Keep(in GlassNotice notice, bool toast)
    {
        tray.Insert(0, notice);
        if (toast)
        {
            arrived.Add(notice);
        }

        while (tray.Count > Cap)
        {
            tray.RemoveAt(tray.Count - 1);
        }
    }

    private void DropMissing(NoticeKind kind, HashSet<string> live)
    {
        for (var index = tray.Count - 1; index >= 0; index--)
        {
            if (tray[index].Kind != kind)
            {
                continue;
            }

            if (live.Contains(tray[index].Id))
            {
                continue;
            }

            tray.RemoveAt(index);
        }
    }

    private void DropScope(NoticeDismissalScope scope)
    {
        for (var index = tray.Count - 1; index >= 0; index--)
        {
            if (tray[index].Scope != scope)
            {
                continue;
            }

            seen.Remove(Track(tray[index]));
            tray.RemoveAt(index);
        }

        arrived.Clear();
    }

    private void PersistDismiss(in GlassNotice item)
    {
        if (item.Kind is NoticeKind.People)
        {
            return;
        }

        if (!NoticeDismissalBook.TryOwner(item.Scope, item.OwnerKey, out _))
        {
            return;
        }

        book.Remember(item.Id, item.Scope, item.OwnerKey, nowUnix);
    }

    private static string Track(in GlassNotice notice) =>
        ((int)notice.Scope).ToString(CultureInfo.InvariantCulture) + ":" + notice.OwnerKey + ":" + notice.Id;

    private static bool Matches(string trayId, string asked)
    {
        if (string.Equals(trayId, asked, StringComparison.Ordinal))
        {
            return true;
        }

        return asked.Length > 0 && trayId.StartsWith(asked + ":", StringComparison.Ordinal);
    }

    private static string Snippet(string body)
    {
        var text = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 72 ? text : text[..72].TrimEnd() + "...";
    }

    private static string Stamp(IClock clock) =>
        clock.Now.ToString("h:mm tt", CultureInfo.InvariantCulture);
}
