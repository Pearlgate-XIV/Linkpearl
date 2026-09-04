using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Talk;
using Linkpearl.Time;

namespace Linkpearl.Destinations.Home;

internal sealed class AnnouncementShelf
{
    private readonly IPearlHub pearl;
    private readonly ITalk talk;
    private readonly IClock clock;
    private readonly DestinationHub hub;
    private readonly NoticeLedger ledger;
    private bool open;
    private string selectedId = string.Empty;

    public AnnouncementShelf(IPearlHub pearl, ITalk talk, IClock clock, DestinationHub hub, NoticeLedger ledger)
    {
        this.pearl = pearl;
        this.talk = talk;
        this.clock = clock;
        this.hub = hub;
        this.ledger = ledger;
    }

    public bool IsOpen => open;

    public void ShowList()
    {
        open = true;
        selectedId = string.Empty;
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
        var snapshot = pearl.Current;
        var selected = Find(snapshot.Announcements, selectedId);
        return selected is { } notice ? DrawDetail(frame, notice) : DrawList(frame, snapshot);
    }

    private float DrawList(in AppletFrame frame, PearlSnapshot snapshot)
    {
        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        DrawHeader(frame, stack.Take(frame.Units(36f)), snapshot);

        var items = ledger.Visible(snapshot, talk, clock);
        if (items.Count == 0)
        {
            DrawEmpty(frame, stack.Take(frame.Units(88f)),
                snapshot.SignedIn
                    ? "Nothing waiting."
                    : "Sign in from You to load notices from Pearlgate.");
            return (content.Height - stack.Remaining.Height) + inset * 2f;
        }

        for (var index = 0; index < items.Count; index++)
        {
            DrawRow(frame, stack.Take(frame.Units(72f)), items[index]);
        }

        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private float DrawDetail(in AppletFrame frame, PearlAnnouncement notice)
    {
        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        DrawBack(frame, stack.Take(frame.Units(36f)), "Announcement", () => selectedId = string.Empty);

        frame.Text.DrawEllipsized(stack.Take(frame.Units(28f)), notice.Title,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        var when = UnixAgo.Format(notice.CreatedAtUnix, clock.Now);
        if (when.Length > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(16f)), when,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        var wrapWidth = content.Width;
        var bodySize = frame.Text.MeasureWrapped(notice.Body, FontRole.Body, wrapWidth);
        var bodyHeight = MathF.Max(frame.Units(72f), bodySize.Y + frame.Units(8f));
        frame.Text.DrawWrapped(stack.Take(bodyHeight), notice.Body,
            new TextStyle(FontRole.Body, frame.Theme.Palette.Ink, TextAlign.Left, 1.15f));

        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private void DrawHeader(in AppletFrame frame, Rect header, PearlSnapshot snapshot)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        DrawBack(frame, header, "Notifications", Close);
        var clear = header.RightSlice(frame.Units(78f));
        frame.Text.DrawIn(clear, "Clear all",
            new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Right));
        if (frame.Input.ConsumeClick(clear))
        {
            ledger.Clear(snapshot, talk);
        }
    }

    private void DrawRow(in AppletFrame frame, Rect row, in GlassNotice notice)
    {
        CardChrome.DrawGold(frame, row);
        var gold = frame.Theme.Palette.WarmAccent;
        var inner = row.Inset(frame.Units(12f));
        var mark = inner.LeftSlice(frame.Units(22f));
        HomeMarks.Draw(frame, mark, MarkFor(notice.Kind), gold);
        HomeMarks.Draw(frame.Paint, inner.RightSlice(frame.Units(12f)), HomeMark.Chevron, gold with { W = 0.65f });
        var copy = inner.Inset(new Edges(frame.Units(28f), 0f, frame.Units(16f), 0f));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(18f)), notice.Title,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(copy.Inset(new Edges(0f, frame.Units(20f), frame.Units(40f), 0f)),
            notice.Detail, new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        frame.Text.DrawIn(copy.RightSlice(frame.Units(40f)).BottomSlice(frame.Units(16f)), notice.When,
            new TextStyle(FontRole.Caption, gold with { W = 0.8f }, TextAlign.Right));
        if (frame.Input.ConsumeClick(row))
        {
            Open(notice);
        }
    }

    private void Open(in GlassNotice notice)
    {
        if (notice.Kind == NoticeKind.Announcement && notice.TargetId.Length > 0)
        {
            selectedId = notice.TargetId;
            return;
        }

        Close();
        hub.Open(notice.Tab, notice.Section);
    }

    private static HomeMark MarkFor(NoticeKind kind) => kind switch
    {
        NoticeKind.Chat => HomeMark.Chat,
        NoticeKind.People => HomeMark.Friends,
        _ => HomeMark.Bell,
    };

    private static void DrawBack(in AppletFrame frame, Rect header, string title, Action back)
    {
        var chevron = header.LeftSlice(frame.Units(28f));
        frame.Text.DrawIn(chevron, "‹",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Accent, TextAlign.Center));
        frame.Text.DrawIn(header.Inset(new Edges(frame.Units(32f), 0f, frame.Units(80f), 0f)), title,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        if (frame.Input.ConsumeClick(chevron) || frame.Input.ConsumeClick(header.LeftSlice(frame.Units(40f))))
        {
            back();
        }
    }

    private static void DrawEmpty(in AppletFrame frame, Rect area, string copy)
    {
        CardChrome.DrawGold(frame, area);
        frame.Text.DrawWrapped(area.Inset(frame.Units(14f)), copy,
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted));
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
