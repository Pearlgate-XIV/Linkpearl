using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Net;
using Linkpearl.Painting;

namespace Linkpearl.Device.Shell;

internal static class StaffNoticeSheet
{
    public static bool Draw(in AppletFrame frame, Rect glass, PearlSnapshot snap, IPearlHub pearl)
    {
        var pending = Pending(snap);
        if (!snap.Banned && pending is null)
        {
            return false;
        }

        frame.Input.Claim(glass);
        frame.Paint.Fill(glass, new Vector4(0.04f, 0.05f, 0.07f, 0.94f));
        var card = glass.Inset(new Edges(frame.Units(18f), frame.Units(52f), frame.Units(18f), frame.Units(28f)));
        var kind = snap.Banned ? "ban" : pending?.Kind ?? "message";
        var ink = KindInk(kind);
        frame.Paint.Fill(card, new Vector4(0.10f, 0.11f, 0.14f, 0.98f), frame.Units(18f));
        frame.Paint.Stroke(card, ink with { W = 0.85f }, frame.Units(1.6f), frame.Units(18f));
        var inner = card.Inset(new Edges(frame.Units(16f), frame.Units(18f), frame.Units(16f), frame.Units(16f)));
        var mark = inner.TopSlice(frame.Units(18f));
        frame.Text.DrawIn(mark, KindLabel(kind),
            new TextStyle(FontRole.CaptionStrong, ink));
        var title = pending is { Title.Length: > 0 } ? pending.Value.Title
            : snap.Banned ? "Account suspended" : "Staff notice";
        var head = Rect.FromSize(new Vector2(inner.Min.X, mark.Max.Y + frame.Units(8f)),
            new Vector2(inner.Width, frame.Units(44f)));
        frame.Text.DrawWrapped(head, title, new TextStyle(FontRole.Title, Vector4.One));
        var body = pending is { Body.Length: > 0 } ? pending.Value.Body
            : snap.Notice.Length > 0 ? snap.Notice
            : snap.Banned ? "Pearlgate staff locked this handset." : "";
        var copyTop = head.Max.Y + frame.Units(10f);
        var ok = snap.Banned
            ? Rect.Empty
            : inner.BottomSlice(frame.Units(44f));
        var copy = new Rect(new Vector2(inner.Min.X, copyTop),
            new Vector2(inner.Max.X, ok.IsEmpty ? inner.Max.Y : ok.Min.Y - frame.Units(12f)));
        frame.Text.DrawWrapped(copy, body, new TextStyle(FontRole.Body, new Vector4(0.88f, 0.89f, 0.92f, 1f)));
        if (snap.Banned)
        {
            var lockLine = inner.BottomSlice(frame.Units(36f));
            frame.Text.DrawIn(lockLine, "This handset stays locked until staff restore it.",
                new TextStyle(FontRole.Caption, ink, TextAlign.Center));
            return true;
        }

        frame.Paint.Fill(ok, ink, ok.Height * 0.5f);
        frame.Text.DrawIn(ok, "OK", new TextStyle(FontRole.BodyStrong, Vector4.One, TextAlign.Center));
        if (pending is { } open && frame.Input.ConsumeClick(ok))
        {
            pearl.MarkStaffNotice(open.Id);
        }

        return true;
    }

    private static PearlStaffNotice? Pending(PearlSnapshot snap)
    {
        var notices = snap.StaffNotices;
        for (var index = 0; index < notices.Length; index++)
        {
            if (!notices[index].Read && notices[index].Id.Length > 0)
            {
                return notices[index];
            }
        }

        return null;
    }

    private static string KindLabel(string kind) => kind switch
    {
        "ban" => "SUSPENDED",
        "mute" => "MUTED",
        "warn" => "WARNING",
        "unban" => "RESTORED",
        _ => "STAFF",
    };

    private static Vector4 KindInk(string kind) => kind switch
    {
        "ban" => new Vector4(0.92f, 0.32f, 0.36f, 1f),
        "mute" => new Vector4(0.98f, 0.72f, 0.28f, 1f),
        "warn" => new Vector4(1f, 0.82f, 0.38f, 1f),
        "unban" => new Vector4(0.42f, 0.82f, 0.56f, 1f),
        _ => new Vector4(0.62f, 0.78f, 1f, 1f),
    };
}
