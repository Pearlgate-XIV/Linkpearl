using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Destinations;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Talk;
using Linkpearl.Time;

namespace Linkpearl.Device.Shell;

internal sealed class NoticeBanner
{
    private string title = string.Empty;
    private string detail = string.Empty;
    private string when = string.Empty;
    private string mark = "pearlchat";
    private string toastId = string.Empty;
    private string targetId = string.Empty;
    private DestinationTab tab;
    private int section;
    private float life;
    private float slide;

    public void Observe(PearlSnapshot snapshot, ITalk talk, IClock clock, NoticeLedger ledger, bool hush)
    {
        ledger.Ingest(snapshot, talk, clock);
        if (ledger.TakeArrival() is not { } notice)
        {
            return;
        }

        _ = hush;

        title = notice.Title.Length > 0 ? notice.Title : "Messages";
        detail = notice.Detail.Length > 0 ? notice.Detail : "New notification";
        when = notice.When.Length > 0 ? notice.When : clock.Now.ToString("h:mm tt", CultureInfo.InvariantCulture);
        mark = NoticeMarks.For(notice.Kind);
        toastId = notice.Id;
        targetId = notice.TargetId;
        tab = notice.Tab;
        section = notice.Section;
        life = 5.2f;
        slide = MathF.Max(slide, 0.35f);
    }

    public void Draw(in AppletFrame frame, Rect screen, DestinationHub hub, NoticeLedger ledger)
    {
        if (life <= 0f && slide <= 0.01f)
        {
            slide = 0f;
            return;
        }

        var showing = life > 0f;
        var speed = 10f;
        slide += ((showing ? 1f : 0f) - slide) * (1f - MathF.Exp(-speed * MathF.Max(frame.DeltaSeconds, 0f)));
        if (showing)
        {
            life -= frame.DeltaSeconds;
        }

        if (slide <= 0.01f)
        {
            return;
        }

        var width = screen.Width - frame.Units(20f);
        var height = frame.Units(58f);
        var rest = StatusStrip.Height(frame.Scale) + frame.Units(56f);
        var top = rest - (1f - slide) * (height + frame.Units(20f));
        var box = Rect.FromSize(new Vector2(screen.Center.X - width * 0.5f, top), new Vector2(width, height));
        var radius = height * 0.5f;
        var fill = new Vector4(0.14f, 0.15f, 0.16f, 1f);
        frame.Paint.Fill(box, fill, radius);
        var pad = frame.Units(8f);
        var icon = Rect.FromSize(new Vector2(box.Min.X + pad, box.Center.Y - frame.Units(21f)),
            new Vector2(frame.Units(42f), frame.Units(42f)));
        AppMarks.DrawFace(frame, icon, mark, false);
        var chevron = Rect.FromSize(new Vector2(box.Max.X - frame.Units(22f), box.Center.Y - frame.Units(8f)),
            new Vector2(frame.Units(14f), frame.Units(16f)));
        frame.Text.DrawIn(chevron, "v",
            new TextStyle(FontRole.Caption, new Vector4(0.72f, 0.72f, 0.74f, 1f), TextAlign.Center));
        var copy = new Rect(new Vector2(icon.Max.X + frame.Units(8f), box.Min.Y + frame.Units(10f)),
            new Vector2(chevron.Min.X - frame.Units(4f), box.Max.Y - frame.Units(10f)));
        var head = copy.TopSlice(frame.Units(18f));
        var name = title;
        var nameWidth = frame.Text.Measure(name, FontRole.CaptionStrong).X;
        var nameBox = new Rect(head.Min, new Vector2(MathF.Min(head.Max.X - frame.Units(52f), head.Min.X + nameWidth),
            head.Max.Y));
        frame.Text.DrawEllipsized(nameBox, name,
            new TextStyle(FontRole.CaptionStrong, new Vector4(0.96f, 0.96f, 0.97f, 1f)));
        var timeBox = new Rect(new Vector2(nameBox.Max.X + frame.Units(6f), head.Min.Y), head.Max);
        frame.Text.DrawEllipsized(timeBox, when,
            new TextStyle(FontRole.Caption, new Vector4(0.70f, 0.70f, 0.72f, 1f)));
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)), detail,
            new TextStyle(FontRole.Caption, new Vector4(0.70f, 0.70f, 0.72f, 1f)));
        if (frame.Input.ConsumeClick(box))
        {
            life = 0f;
            ledger.Dismiss(toastId);
            if (targetId.Length > 0 && tab == DestinationTab.Social && section == SocialPane.Messages)
            {
                hub.OpenTalk(targetId);
            }
            else if (targetId.Length > 0 && tab == DestinationTab.Social && section == SocialPane.People)
            {
                hub.OpenProfile(targetId);
            }
            else if (targetId.Length > 0 && tab == DestinationTab.Home && section == HomePane.Announcements)
            {
                hub.OpenAnnouncement(targetId);
            }
            else if (mark == "calendar")
            {
                hub.OpenApplet("calendar", targetId);
            }
            else
            {
                hub.Open(tab, section);
            }
        }
    }
}
