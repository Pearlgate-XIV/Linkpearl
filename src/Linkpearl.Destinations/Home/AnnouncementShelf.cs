using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Time;

namespace Linkpearl.Destinations.Home;

internal sealed class AnnouncementShelf
{
    private readonly IPearlHub pearl;
    private readonly IClock clock;
    private bool open;
    private string selectedId = string.Empty;

    public AnnouncementShelf(IPearlHub pearl, IClock clock)
    {
        this.pearl = pearl;
        this.clock = clock;
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
        DrawHeader(frame, stack.Take(frame.Units(36f)), "Announcements", Close);

        if (!snapshot.SignedIn)
        {
            DrawEmpty(frame, stack.Take(frame.Units(88f)), "Sign in from You to load announcements.");
            return (content.Height - stack.Remaining.Height) + inset * 2f;
        }

        var items = snapshot.Announcements;
        if (items.Length == 0)
        {
            DrawEmpty(frame, stack.Take(frame.Units(88f)),
                "Nothing posted yet. Notices from Linkpearl will sit here.");
            return (content.Height - stack.Remaining.Height) + inset * 2f;
        }

        for (var index = 0; index < items.Length; index++)
        {
            DrawRow(frame, stack.Take(frame.Units(88f)), items[index]);
        }

        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private float DrawDetail(in AppletFrame frame, PearlAnnouncement notice)
    {
        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        DrawHeader(frame, stack.Take(frame.Units(36f)), "Announcement", () => selectedId = string.Empty);

        var when = UnixAgo.Format(notice.CreatedAtUnix, clock.Now);
        frame.Text.DrawEllipsized(stack.Take(frame.Units(28f)), notice.Title,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
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

    private void DrawRow(in AppletFrame frame, Rect row, PearlAnnouncement notice)
    {
        CardChrome.DrawGold(frame, row);
        var gold = frame.Theme.Palette.WarmAccent;
        var inner = row.Inset(frame.Units(12f));
        HomeMarks.Draw(frame.Paint, inner.RightSlice(frame.Units(12f)), HomeMark.Chevron, gold with { W = 0.65f });
        var copy = inner.Inset(new Edges(0f, 0f, frame.Units(16f), 0f));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(20f)), notice.Title,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(copy.Inset(new Edges(0f, frame.Units(22f), 0f, frame.Units(18f))),
            Snippet(notice.Body), new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var when = UnixAgo.Format(notice.CreatedAtUnix, clock.Now);
        if (when.Length > 0)
        {
            frame.Text.DrawIn(copy.BottomSlice(frame.Units(16f)), when,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint));
        }

        if (frame.Input.ConsumeClick(row))
        {
            selectedId = notice.Id;
        }
    }

    private static void DrawHeader(in AppletFrame frame, Rect header, string title, Action back)
    {
        var chevron = header.LeftSlice(frame.Units(28f));
        frame.Text.DrawIn(chevron, "‹",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Accent, TextAlign.Center));
        frame.Text.DrawIn(header.Inset(new Edges(frame.Units(32f), 0f, 0f, 0f)), title,
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

    internal static string Snippet(string body)
    {
        var text = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 72 ? text : text[..72].TrimEnd() + "...";
    }
}
