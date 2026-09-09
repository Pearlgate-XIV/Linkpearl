using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Notices;
using Linkpearl.Talk;
using Linkpearl.Time;

namespace Linkpearl.Destinations.Home;

internal sealed class AnnouncementShelf
{
    private readonly IPearlHub pearl;
    private readonly IClock clock;
    private readonly NoticeLedger ledger;
    private bool open;
    private string selectedId = string.Empty;

    public AnnouncementShelf(IPearlHub pearl, ITalk talk, IClock clock, DestinationHub hub, NoticeLedger ledger)
    {
        this.pearl = pearl;
        this.clock = clock;
        this.ledger = ledger;
        _ = talk;
        _ = hub;
    }

    public bool IsOpen => open;

    public void ShowList()
    {
        open = true;
        selectedId = string.Empty;
    }

    public void ShowNotice(string announcementId)
    {
        open = true;
        selectedId = announcementId ?? string.Empty;
    }

    public void Close()
    {
        open = false;
        selectedId = string.Empty;
    }

    public bool Back()
    {
        if (selectedId.Length > 0)
        {
            selectedId = string.Empty;
            return true;
        }

        if (open)
        {
            Close();
            return true;
        }

        return false;
    }

    public float Compose(in AppletFrame frame)
    {
        var night = AnnouncementChrome.Night(clock.Now);
        var wash = new Rect(
            new Vector2(frame.Content.Min.X, frame.Content.Min.Y - frame.Units(400f)),
            new Vector2(frame.Content.Max.X, frame.Content.Max.Y + frame.Units(1200f)));
        AnnouncementChrome.Paint(frame, wash, night);
        var snapshot = pearl.Current;
        var selected = Find(snapshot.Announcements, selectedId);
        return selected is { } notice ? DrawDetail(frame, notice, night) : DrawList(frame, snapshot, night);
    }

    private float DrawList(in AppletFrame frame, PearlSnapshot snapshot, bool night)
    {
        var inset = frame.Units(16f);
        var content = frame.Content.Inset(new Edges(inset, frame.Units(8f), inset, frame.Units(10f)));
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(8f));
        AnnouncementChrome.Toolbar(frame, stack.Take(frame.Units(36f)), "Announcements", string.Empty, night, Close,
            null);

        var items = snapshot.Announcements ?? [];
        if (items.Length == 0)
        {
            AnnouncementChrome.Empty(frame, stack.Take(frame.Units(88f)),
                snapshot.SignedIn
                    ? "When Linkpearl posts, it will land here."
                    : "Sign in from You to load announcements from Pearlgate.", night);
            return (content.Height - stack.Remaining.Height) + inset * 2f;
        }

        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            AnnouncementChrome.Story(frame, stack.Take(frame.Units(72f)), item.Title, Snippet(item.Body),
                AnnouncementChrome.Ago(item.CreatedAtUnix, clock.Now), night, () => OpenPosted(item));
        }

        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private float DrawDetail(in AppletFrame frame, PearlAnnouncement notice, bool night)
    {
        var inset = frame.Units(16f);
        var content = frame.Content.Inset(new Edges(inset, frame.Units(8f), inset, frame.Units(10f)));
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        AnnouncementChrome.Toolbar(frame, stack.Take(frame.Units(36f)), "Story", string.Empty, night,
            () => selectedId = string.Empty, null);
        AnnouncementChrome.Article(frame, stack.TakeRemaining(), notice.Title,
            AnnouncementChrome.Ago(notice.CreatedAtUnix, clock.Now), notice.Body, night);
        return content.Height + inset * 2f;
    }

    private void OpenPosted(in PearlAnnouncement notice)
    {
        if (notice.Id.Length == 0)
        {
            return;
        }

        ledger.Dismiss("ann:" + notice.Id);
        selectedId = notice.Id;
    }

    private static string Snippet(string body)
    {
        var text = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 72 ? text : text[..72].TrimEnd() + "...";
    }

    private static PearlAnnouncement? Find(PearlAnnouncement[] items, string id)
    {
        if (id.Length == 0)
        {
            return null;
        }

        for (var index = 0; index < items.Length; index++)
        {
            if (string.Equals(items[index].Id, id, StringComparison.Ordinal))
            {
                return items[index];
            }
        }

        return null;
    }
}
