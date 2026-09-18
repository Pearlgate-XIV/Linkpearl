using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Device.Chassis;
using Linkpearl.Geometry;
using Linkpearl.Painting;
using Linkpearl.Preferences;

namespace Linkpearl.Device.Shell;

// Miniature phone: icon, lock, or communicator. All three carry the slide-to-wake grab.
public static class MinimizedFace
{
    public const float ClockUnits = 22f;
    public const float NoticeUnits = 32f;
    public const float DateUnits = 16f;
    public const float GapUnits = 4f;
    public const float SliderUnits = 56f;
    public const float IconUnits = 48f;

    public static PocketFace Face(float pocketScale) => HandsetShapePreference.FaceFor(pocketScale);

    public static float LayoutDip(Rect screen, float uiScale, PocketFace face, bool notice)
    {
        float units;
        if (face == PocketFace.Icon)
        {
            units = IconUnits + GapUnits + SliderUnits;
        }
        else
        {
            units = ClockUnits + GapUnits + DateUnits + GapUnits + SliderUnits;
            if (notice)
            {
                units += NoticeUnits + GapUnits;
            }
        }

        var inner = GlassSafe.Inner(screen, uiScale);
        var fit = inner.Height / MathF.Max(units, 1f);
        return MathF.Max(0.2f, MathF.Min(uiScale, fit));
    }

    public static float TrackTop(float dip, bool notice, PocketFace face = PocketFace.Slider)
    {
        if (face == PocketFace.Icon)
        {
            return GapUnits * dip;
        }

        return (notice
            ? ClockUnits + GapUnits + NoticeUnits + GapUnits
            : ClockUnits + GapUnits) * dip;
    }

    public static float TrackBottom(float dip, PocketFace face = PocketFace.Slider) =>
        face == PocketFace.Icon ? GapUnits * dip : (DateUnits + GapUnits) * dip;

    public static Rect UnlockWell(Rect inner, float dip, PocketFace face)
    {
        if (face != PocketFace.Icon || inner.IsEmpty)
        {
            return inner;
        }

        var iconH = IconHeight(inner, dip);
        var top = inner.Min.Y + iconH + GapUnits * dip;
        if (top >= inner.Max.Y)
        {
            return inner;
        }

        return new Rect(new Vector2(inner.Min.X, top), inner.Max);
    }

    public static Rect UnlockHit(Rect screen, float dip, bool notice, PocketFace face)
    {
        var inner = GlassSafe.Inner(screen, dip);
        var well = UnlockWell(inner, dip, face);
        var track = PocketUnlock.HitOn(well, dip, notice && face != PocketFace.Icon, face);
        if (track.IsEmpty)
        {
            return well.IsEmpty ? Rect.Empty : well.Expand(MathF.Max(6f, 8f * dip));
        }

        return track.Expand(MathF.Max(8f, 12f * dip));
    }

    public static Rect NoticeOn(Rect screen, float dip, bool notice, PocketFace face = PocketFace.Slider)
    {
        if (!notice)
        {
            return Rect.Empty;
        }

        var inner = GlassSafe.Inner(screen, dip);
        if (face == PocketFace.Icon)
        {
            return inner.TopSlice(IconHeight(inner, dip));
        }

        var width = MathF.Max(0f, inner.Width);
        var height = MathF.Min(NoticeUnits * dip, inner.Height * 0.28f);
        var y = inner.Min.Y + (ClockUnits + GapUnits) * dip;
        if (y + height > inner.Max.Y - DateUnits * dip)
        {
            y = MathF.Max(inner.Min.Y, inner.Max.Y - DateUnits * dip - GapUnits * dip - height);
        }

        return Rect.FromSize(new Vector2(inner.Center.X - width * 0.5f, y), new Vector2(width, height));
    }

    public static bool Draw(in AppletFrame frame, Rect screen, PocketUnlock unlock, bool allowSlide, string clockText,
        string dateText, int noticeCount, string noticeMark, PocketFace face)
    {
        var ui = MathF.Max(frame.Scale, 0.2f);
        var notice = noticeCount > 0;
        var dip = LayoutDip(screen, ui, face, notice);
        var inner = GlassSafe.Inner(screen, dip);
        if (inner.IsEmpty)
        {
            return false;
        }

        if (face == PocketFace.Icon)
        {
            var iconH = IconHeight(inner, dip);
            DrawIconFace(frame, inner.TopSlice(iconH), noticeCount, noticeMark);
        }
        else
        {
            frame.Text.DrawIn(inner.TopSlice(ClockUnits * dip), clockText,
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink, TextAlign.Center));
            if (notice)
            {
                DrawNoticeChip(frame, NoticeOn(screen, dip, true, face), noticeCount, noticeMark);
            }

            frame.Text.DrawIn(inner.BottomSlice(DateUnits * dip), dateText,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink, TextAlign.Center));
        }

        var well = UnlockWell(inner, dip, face);
        return unlock.Draw(frame.Paint, frame.Input, frame.Theme, well, dip, frame.DeltaSeconds, allowSlide,
            notice && face != PocketFace.Icon, face);
    }

    private static float IconHeight(Rect inner, float dip)
    {
        var slider = MathF.Min(PocketUnlock.HeightUnits * dip, inner.Height * 0.55f);
        var room = MathF.Max(0f, inner.Height - slider - GapUnits * dip);
        return MathF.Min(MathF.Min(inner.Width, IconUnits * dip), room);
    }

    private static void DrawIconFace(in AppletFrame frame, Rect inner, int count, string mark)
    {
        var side = MathF.Min(inner.Width, inner.Height) * 0.72f;
        var icon = Rect.FromSize(inner.Center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
        AppMarks.DrawFace(frame, icon, mark.Length > 0 ? mark : "pearlchat", false);
        AppMarks.DrawCount(frame, icon, count);
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
        var pad = MathF.Max(3f, MathF.Min(box.Height * 0.18f, frame.Units(6f)));
        var iconSide = MathF.Max(8f, box.Height - pad * 2f);
        var icon = Rect.FromSize(new Vector2(box.Min.X + pad, box.Center.Y - iconSide * 0.5f),
            new Vector2(iconSide, iconSide));
        AppMarks.DrawFace(frame, icon, mark.Length > 0 ? mark : "pearlchat", false);
        var label = count > 99 ? "99+" : count.ToString(CultureInfo.InvariantCulture);
        var copy = new Rect(new Vector2(icon.Max.X + pad, box.Min.Y),
            new Vector2(box.Max.X - pad, box.Max.Y));
        frame.Text.DrawIn(copy, label,
            new TextStyle(FontRole.CaptionStrong, new Vector4(0.96f, 0.96f, 0.97f, 1f), TextAlign.Center));
    }
}
