using System.Globalization;
using Linkpearl.Net;
using Linkpearl.Talk;
using Linkpearl.Time;

namespace Linkpearl.Destinations;

public enum NoticeKind : byte
{
    Announcement = 0,
    Chat = 1,
    People = 2,
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

public sealed class NoticeLedger
{
    private readonly HashSet<string> dismissed = new(StringComparer.Ordinal);
    private readonly List<GlassNotice> visible = [];

    public IReadOnlyList<GlassNotice> Visible(PearlSnapshot snapshot, ITalk talk, IClock clock)
    {
        visible.Clear();
        var notices = snapshot.Announcements ?? [];
        for (var index = 0; index < notices.Length; index++)
        {
            var item = notices[index];
            var id = "ann:" + item.Id;
            if (dismissed.Contains(id))
            {
                continue;
            }

            visible.Add(new GlassNotice(id, NoticeKind.Announcement, item.Title, Snippet(item.Body),
                When(clock, item.CreatedAtUnix), DestinationTab.Home, HomePane.Announcements, item.Id));
        }

        var unread = talk.UnreadTotal + snapshot.UnreadTotal;
        if (unread > 0 && !dismissed.Contains("chat"))
        {
            var title = "PearlChat";
            var detail = unread.ToString(CultureInfo.InvariantCulture) + " unread";
            var inbox = talk.Inbox();
            for (var index = 0; index < inbox.Count; index++)
            {
                if (inbox[index].Unread > 0 && inbox[index].Title.Length > 0)
                {
                    title = inbox[index].Title;
                    detail = inbox[index].Preview.Length > 0 ? inbox[index].Preview : detail;
                    break;
                }
            }

            visible.Add(new GlassNotice("chat", NoticeKind.Chat, title, detail, "Now",
                DestinationTab.Social, SocialPane.Messages, string.Empty));
        }

        if (snapshot.People.Length > 0 && !dismissed.Contains("people"))
        {
            var person = snapshot.People[0];
            visible.Add(new GlassNotice("people", NoticeKind.People, person.DisplayName,
                person.IsMutual ? "On the glass" : person.Handle, "Now",
                DestinationTab.Social, SocialPane.People, person.Id));
        }

        return visible;
    }

    public int Count(PearlSnapshot snapshot, ITalk talk, IClock clock) => Visible(snapshot, talk, clock).Count;

    public void Clear(PearlSnapshot snapshot, ITalk talk)
    {
        var notices = snapshot.Announcements ?? [];
        for (var index = 0; index < notices.Length; index++)
        {
            dismissed.Add("ann:" + notices[index].Id);
        }

        if (talk.UnreadTotal + snapshot.UnreadTotal > 0)
        {
            dismissed.Add("chat");
        }

        if (snapshot.People.Length > 0)
        {
            dismissed.Add("people");
        }
    }

    private static string Snippet(string body)
    {
        var text = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 72 ? text : text[..72].TrimEnd() + "...";
    }

    private static string When(IClock clock, long unix)
    {
        if (unix <= 0)
        {
            return "Now";
        }

        var age = clock.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unix);
        if (age.TotalMinutes < 1)
        {
            return "Now";
        }

        if (age.TotalHours < 1)
        {
            return ((int)age.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m";
        }

        if (age.TotalDays < 1)
        {
            return ((int)age.TotalHours).ToString(CultureInfo.InvariantCulture) + "h";
        }

        return ((int)age.TotalDays).ToString(CultureInfo.InvariantCulture) + "d";
    }
}
