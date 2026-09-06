using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

// Miniature phone: clock, optional notice chip, slide-to-wake, date on the glass.
public static class MinimizedFace
{
    public const float ClockUnits = 26f;
    public const float NoticeUnits = 36f;
    public const float DateUnits = 18f;
    public const float GapUnits = 5f;

    public static float TrackTop(float dip, bool notice) =>
        (notice
            ? ClockUnits + GapUnits + NoticeUnits + GapUnits
            : ClockUnits + GapUnits) * dip;

    public static float TrackBottom(float dip) => (DateUnits + GapUnits) * dip;

    public static Rect NoticeOn(Rect screen, float dip, bool notice)
    {
        if (!notice)
        {
            return Rect.Empty;
        }

        var width = MathF.Max(0f, screen.Width - 14f * dip);
        var height = NoticeUnits * dip;
        var y = screen.Min.Y + (ClockUnits + GapUnits) * dip;
        return Rect.FromSize(new Vector2(screen.Center.X - width * 0.5f, y), new Vector2(width, height));
    }

    public static bool Draw(in AppletFrame frame, Rect screen, PocketUnlock unlock, bool allowSlide, string clockText,
        string dateText, int noticeCount, string noticeMark)
    {
        var dip = frame.Scale;
        var inset = screen.Inset(dip * 8f);
        if (inset.IsEmpty)
        {
            return false;
        }

        var notice = noticeCount > 0;
        frame.Text.DrawIn(inset.TopSlice(ClockUnits * dip), clockText,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink, TextAlign.Center));
        if (notice)
        {
            DrawNoticeChip(frame, NoticeOn(screen, dip, true), noticeCount, noticeMark);
        }

        frame.Text.DrawIn(inset.BottomSlice(DateUnits * dip), dateText,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink, TextAlign.Center));
        return unlock.Draw(frame.Paint, frame.Input, frame.Theme, screen, dip, frame.DeltaSeconds, allowSlide, notice);
    }

    private static void DrawNoticeChip(in AppletFrame frame, Rect box, int count, string mark)
    {
        if (box.IsEmpty)
        {
            return;
        }

        var radius = box.Height * 0.5f;
        var fill = new Vector4(0.14f, 0.15f, 0.16f, 1f);
        frame.Paint.Fill(box, fill, radius);
        var pad = MathF.Max(4f, frame.Units(6f));
        var iconSide = MathF.Min(box.Height - pad, frame.Units(28f));
        var icon = Rect.FromSize(new Vector2(box.Min.X + pad, box.Center.Y - iconSide * 0.5f),
            new Vector2(iconSide, iconSide));
        AppMarks.DrawFace(frame, icon, mark.Length > 0 ? mark : "pearlchat", false);
        var label = count > 99 ? "99+" : count.ToString(CultureInfo.InvariantCulture);
        var copy = new Rect(new Vector2(icon.Max.X + frame.Units(6f), box.Min.Y),
            new Vector2(box.Max.X - pad, box.Max.Y));
        frame.Text.DrawIn(copy, label,
            new TextStyle(FontRole.CaptionStrong, new Vector4(0.96f, 0.96f, 0.97f, 1f), TextAlign.Center));
    }
}
