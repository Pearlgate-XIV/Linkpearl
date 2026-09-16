using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Destinations;
using Linkpearl.Device.Chassis;
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
    private NoticeKind kind;
    private DestinationTab tab;
    private int section;
    private float life;
    private float slide;
    private bool held;
    private float grabY;
    private float lift;

    public bool BlocksPager => held || life > 0f;

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
        kind = notice.Kind;
        mark = NoticeMarks.For(notice.Kind);
        toastId = notice.Id;
        targetId = notice.TargetId;
        tab = notice.Tab;
        section = notice.Section;
        life = 5.2f;
        slide = MathF.Max(slide, 0.35f);
        held = false;
        lift = 0f;
    }

    public void Capture(in AppletFrame frame, Rect screen, DestinationHub hub, NoticeLedger ledger)
    {
        if (life <= 0f && !held && slide <= 0.01f)
        {
            return;
        }

        var box = RestBox(frame, screen);
        if (box.IsEmpty)
        {
            return;
        }

        if (life > 0f || held || slide > 0.02f)
        {
            frame.Input.Claim(box);
        }

        if (held && frame.Input.EscapePressed())
        {
            held = false;
            lift = 0f;
            life = 0f;
            ledger.Dismiss(toastId);
            return;
        }

        if (!held && life > 0f && frame.Input.WasPressed(box))
        {
            held = true;
            grabY = frame.Input.Pointer.Y;
            lift = 0f;
            frame.Input.Claim(box);
            return;
        }

        if (held && frame.Input.IsHeld())
        {
            lift = MathF.Max(0f, grabY - frame.Input.Pointer.Y);
            frame.Input.Claim(box.Translate(new Vector2(0f, -lift)).Expand(frame.Units(8f)));
            return;
        }

        if (held)
        {
            var toss = lift >= frame.Units(22f);
            held = false;
            lift = 0f;
            if (toss)
            {
                life = 0f;
                ledger.Dismiss(toastId);
                return;
            }

            Open(hub, ledger);
        }
    }

    public void Draw(in AppletFrame frame, Rect screen, DestinationHub hub, NoticeLedger ledger)
    {
        _ = hub;
        _ = ledger;
        if (life <= 0f && slide <= 0.01f && !held)
        {
            slide = 0f;
            lift = 0f;
            return;
        }

        var showing = life > 0f || held;
        var speed = 10f;
        if (held)
        {
            slide = MathF.Max(slide, 0.98f);
        }
        else
        {
            slide += ((showing ? 1f : 0f) - slide) * (1f - MathF.Exp(-speed * MathF.Max(frame.DeltaSeconds, 0f)));
            if (life > 0f)
            {
                life -= frame.DeltaSeconds;
            }
        }

        if (slide <= 0.01f && !held)
        {
            slide = 0f;
            lift = 0f;
            return;
        }

        var rest = RestBox(frame, screen);
        var box = rest.Translate(new Vector2(0f, -lift));
        var fade = 1f - Math.Clamp(lift / MathF.Max(box.Height + frame.Units(16f), 1f), 0f, 0.85f);
        var radius = box.Height * 0.5f;
        var fill = new Vector4(0.14f, 0.15f, 0.16f, fade);
        frame.Paint.Fill(box, fill, radius);
        var pad = MathF.Max(frame.Units(6f), box.Height * 0.14f);
        var iconSide = MathF.Min(frame.Units(32f), MathF.Max(8f, box.Height - pad * 2f));
        var icon = Rect.FromSize(new Vector2(box.Min.X + pad, box.Center.Y - iconSide * 0.5f),
            new Vector2(iconSide, iconSide));
        AppMarks.DrawFace(frame, icon, mark, false);
        var copy = new Rect(new Vector2(icon.Max.X + pad, box.Min.Y + pad),
            new Vector2(box.Max.X - pad, box.Max.Y - pad));
        var head = copy.TopSlice(frame.Units(18f));
        var name = title;
        var nameWidth = frame.Text.Measure(name, FontRole.CaptionStrong).X;
        var nameBox = new Rect(head.Min, new Vector2(MathF.Min(head.Max.X - frame.Units(52f), head.Min.X + nameWidth),
            head.Max.Y));
        var ink = new Vector4(0.96f, 0.96f, 0.97f, fade);
        var mute = new Vector4(0.70f, 0.70f, 0.72f, fade);
        frame.Text.DrawEllipsized(nameBox, name, new TextStyle(FontRole.CaptionStrong, ink));
        var timeBox = new Rect(new Vector2(nameBox.Max.X + frame.Units(6f), head.Min.Y), head.Max);
        frame.Text.DrawEllipsized(timeBox, when, new TextStyle(FontRole.Caption, mute));
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)), detail,
            new TextStyle(FontRole.Caption, mute));
    }

    private void Open(DestinationHub hub, NoticeLedger ledger)
    {
        life = 0f;
        lift = 0f;
        ledger.Dismiss(toastId);
        NoticeLaunch.ToHub(hub, kind, tab, section, targetId);
    }

    private Rect RestBox(in AppletFrame frame, Rect screen)
    {
        var pad = GlassSafe.Pad(screen, frame.Scale);
        var width = MathF.Max(0f, screen.Width - pad * 2f);
        var height = frame.Units(58f);
        var rest = StatusStrip.Height(frame.Scale) + frame.Units(56f);
        var top = rest - (1f - slide) * (height + frame.Units(20f));
        top = MathF.Max(screen.Min.Y + pad, top);
        return Rect.FromSize(new Vector2(screen.Center.X - width * 0.5f, top), new Vector2(width, height));
    }
}

internal static class NoticeLaunch
{
    public static void ToHub(DestinationHub hub, NoticeKind kind, DestinationTab tab, int section, string targetId)
    {
        if (kind == NoticeKind.Calendar)
        {
            hub.OpenApplet("calendar", targetId);
            return;
        }

        if (kind == NoticeKind.Music)
        {
            hub.OpenApplet("music", targetId.Length > 0 ? "live:" + targetId : "player");
            return;
        }

        if (kind == NoticeKind.Venue)
        {
            hub.OpenApplet("venues", targetId);
            return;
        }

        if (targetId.Length > 0 && tab == DestinationTab.Social && section == SocialPane.Messages)
        {
            hub.OpenTalk(targetId);
            return;
        }

        if (targetId.Length > 0 && tab == DestinationTab.Social && section == SocialPane.People)
        {
            hub.OpenProfile(targetId);
            return;
        }

        if (targetId.Length > 0 && tab == DestinationTab.Home && section == HomePane.Announcements)
        {
            hub.OpenAnnouncement(targetId);
            return;
        }

        hub.Open(tab, section);
    }

    public static ControlCenterResult ToResult(in GlassNotice item)
    {
        if (item.Kind == NoticeKind.Calendar)
        {
            return new ControlCenterResult(false, false, false, false, null, 0, "calendar",
                RouteHint: item.TargetId);
        }

        if (item.Kind == NoticeKind.Music)
        {
            return new ControlCenterResult(false, false, false, false, null, 0, "music",
                RouteHint: item.TargetId.Length > 0 ? "live:" + item.TargetId : "player");
        }

        if (item.Kind == NoticeKind.Venue)
        {
            return new ControlCenterResult(false, false, false, false, null, 0, "venues",
                RouteHint: item.TargetId);
        }

        return new ControlCenterResult(false, false, false, false, item.Tab, item.Section, string.Empty,
            TalkId: item.Kind == NoticeKind.Chat ? item.TargetId : string.Empty,
            ProfileId: item.Kind == NoticeKind.People ? item.TargetId : string.Empty,
            NoticeId: item.Kind is NoticeKind.Announcement or NoticeKind.Staff ? item.TargetId : string.Empty);
    }
}
