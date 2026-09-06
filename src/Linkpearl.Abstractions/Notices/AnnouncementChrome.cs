using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;

namespace Linkpearl.Notices;

public static class AnnouncementChrome
{
    public static bool Night(DateTimeOffset now)
    {
        _ = now;
        return true;
    }

    public static Vector4 Ink(bool night)
    {
        _ = night;
        return new Vector4(0.96f, 0.95f, 1f, 0.96f);
    }

    public static Vector4 Hush(bool night)
    {
        _ = night;
        return new Vector4(0.86f, 0.84f, 0.96f, 0.64f);
    }

    public static Vector4 Accent(bool night)
    {
        _ = night;
        return new Vector4(0.74f, 0.64f, 1f, 1f);
    }

    public static void Paint(in AppletFrame frame, Rect area, bool night) => Wash(frame, area, night, 0f);

    public static void Dock(in AppletFrame frame, Rect area, string title, string body, string when, int more,
        DateTimeOffset now)
    {
        var night = Night(now);
        var radius = Corner(frame, area);
        Wash(frame, area, night, radius);
        var ink = Ink(night);
        var hush = Hush(night);
        var accent = Accent(night);
        var inset = area.Inset(new Edges(frame.Units(12f), frame.Units(10f), frame.Units(12f), frame.Units(8f)));
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(3f));
        DrawSource(frame, stack.Take(frame.Units(16f)), more, hush, accent);
        var hero = stack.Take(MathF.Max(frame.Units(22f), inset.Height * 0.34f));
        frame.Text.DrawFitted(hero, title, new TextStyle(FontRole.Title, ink));
        if (stack.Remaining.Height >= frame.Units(28f) && body.Length > 0)
        {
            frame.Text.DrawWrapped(stack.Take(MathF.Min(frame.Units(40f), stack.Remaining.Height - frame.Units(16f))),
                body, new TextStyle(FontRole.Caption, hush, TextAlign.Left, 1.12f));
        }

        if (stack.Remaining.Height >= frame.Units(14f))
        {
            var foot = stack.Take(frame.Units(14f));
            frame.Text.DrawEllipsized(foot.LeftSlice(foot.Width * 0.55f), when.Length > 0 ? when : "Linkpearl",
                new TextStyle(FontRole.Caption, accent));
            frame.Text.DrawIn(foot.RightSlice(foot.Width * 0.42f), "Read",
                new TextStyle(FontRole.CaptionStrong, hush, TextAlign.Right));
        }
    }

    public static void Toolbar(in AppletFrame frame, Rect row, string title, string action, bool night, Action back,
        Action? pressed)
    {
        var ink = Ink(night);
        var accent = Accent(night);
        var backSlot = row.LeftSlice(frame.Units(28f));
        DrawChevron(frame, backSlot, ink);
        var actionSlot = action.Length > 0 ? row.RightSlice(frame.Units(72f)) : default;
        var titleRight = action.Length > 0 ? actionSlot.Min.X - frame.Units(8f) : row.Max.X;
        frame.Text.DrawEllipsized(new Rect(new Vector2(backSlot.Max.X + frame.Units(4f), row.Min.Y),
            new Vector2(titleRight, row.Max.Y)), title, new TextStyle(FontRole.Title, ink));
        if (action.Length > 0)
        {
            frame.Text.DrawIn(actionSlot, action, new TextStyle(FontRole.CaptionStrong, accent, TextAlign.Right));
            if (pressed is not null && frame.Input.ConsumeClick(actionSlot))
            {
                pressed();
            }
        }

        if (frame.Input.ConsumeClick(backSlot) || frame.Input.ConsumeClick(row.LeftSlice(frame.Units(40f))))
        {
            back();
        }
    }

    public static void Story(in AppletFrame frame, Rect row, string title, string detail, string when, bool night,
        Action pressed)
    {
        var ink = Ink(night);
        var hush = Hush(night);
        var accent = Accent(night);
        Glass(frame, row, night);
        var bar = row.LeftSlice(frame.Units(4f)).Inset(new Edges(0f, frame.Units(10f), 0f, frame.Units(10f)));
        frame.Paint.Fill(bar, accent, bar.Width);
        var inner = row.Inset(new Edges(frame.Units(16f), frame.Units(10f), frame.Units(12f), frame.Units(10f)));
        frame.Text.DrawEllipsized(inner.TopSlice(frame.Units(18f)), title,
            new TextStyle(FontRole.BodyStrong, ink));
        var rest = inner.Inset(new Edges(0f, frame.Units(20f), 0f, 0f));
        if (when.Length > 0)
        {
            frame.Text.DrawIn(rest.RightSlice(frame.Units(36f)).BottomSlice(frame.Units(14f)), when,
                new TextStyle(FontRole.Caption, accent, TextAlign.Right));
            rest = rest.Inset(new Edges(0f, 0f, frame.Units(40f), 0f));
        }

        if (detail.Length > 0)
        {
            frame.Text.DrawEllipsized(rest, detail, new TextStyle(FontRole.Caption, hush));
        }

        if (frame.Input.ConsumeClick(row))
        {
            pressed();
        }
    }

    public static void Article(in AppletFrame frame, Rect page, string title, string when, string body, bool night)
    {
        var ink = Ink(night);
        var hush = Hush(night);
        var accent = Accent(night);
        var stack = new Stack(page, StackAxis.Vertical, frame.Units(8f));
        frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Linkpearl",
            new TextStyle(FontRole.CaptionStrong, accent));
        frame.Text.DrawWrapped(stack.Take(frame.Units(48f)), title, new TextStyle(FontRole.Title, ink));
        if (when.Length > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(16f)), when, new TextStyle(FontRole.Caption, hush));
        }

        var card = stack.TakeRemaining();
        if (card.Height < frame.Units(48f))
        {
            frame.Text.DrawWrapped(card, body, new TextStyle(FontRole.Body, ink, TextAlign.Left, 1.18f));
            return;
        }

        Glass(frame, card, night);
        frame.Text.DrawWrapped(card.Inset(frame.Units(14f)), body,
            new TextStyle(FontRole.Body, ink, TextAlign.Left, 1.18f));
    }

    public static void Empty(in AppletFrame frame, Rect area, string copy, bool night)
    {
        Glass(frame, area, night);
        frame.Text.DrawWrapped(area.Inset(frame.Units(14f)), copy,
            new TextStyle(FontRole.Body, Hush(night)));
    }

    public static string Ago(long unixSeconds, DateTimeOffset now)
    {
        if (unixSeconds <= 0)
        {
            return string.Empty;
        }

        var then = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var delta = now - then;
        if (delta.TotalSeconds < 45)
        {
            return "Now";
        }

        if (delta.TotalMinutes < 60)
        {
            return ((int)delta.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m";
        }

        if (delta.TotalHours < 24)
        {
            return ((int)delta.TotalHours).ToString(CultureInfo.InvariantCulture) + "h";
        }

        return ((int)delta.TotalDays).ToString(CultureInfo.InvariantCulture) + "d";
    }

    private static void DrawSource(in AppletFrame frame, Rect row, int more, Vector4 hush, Vector4 accent)
    {
        frame.Text.DrawEllipsized(row.LeftSlice(row.Width * 0.7f), "Linkpearl",
            new TextStyle(FontRole.CaptionStrong, accent));
        if (more <= 0)
        {
            return;
        }

        var chip = row.RightSlice(frame.Units(36f));
        frame.Paint.Fill(chip, accent with { W = 0.18f }, chip.Height * 0.5f);
        frame.Text.DrawIn(chip, "+" + more.ToString(CultureInfo.InvariantCulture),
            new TextStyle(FontRole.CaptionStrong, hush, TextAlign.Center, 1f, 0.86f));
    }

    private static void DrawChevron(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var size = MathF.Min(area.Width, area.Height) * 0.18f;
        var c = area.Center;
        var thick = MathF.Max(1.4f, frame.Units(1.6f));
        frame.Paint.Line(c + new Vector2(-size, 0f), c + new Vector2(size * 0.2f, -size), ink, thick);
        frame.Paint.Line(c + new Vector2(-size, 0f), c + new Vector2(size * 0.2f, size), ink, thick);
    }

    private static void Glass(in AppletFrame frame, Rect area, bool night)
    {
        _ = night;
        var fill = new Vector4(0.12f, 0.10f, 0.18f, 0.52f);
        var edge = new Vector4(1f, 1f, 1f, 0.08f);
        var radius = frame.Units(16f);
        frame.Paint.Fill(area, fill, radius);
        frame.Paint.Stroke(area, edge, frame.Theme.Metrics.Hairline, radius);
    }

    private static void Wash(in AppletFrame frame, Rect area, bool night, float radius)
    {
        _ = night;
        var top = new Vector4(0.14f, 0.11f, 0.24f, 1f);
        var bottom = new Vector4(0.06f, 0.06f, 0.10f, 1f);

        if (radius <= 0.5f)
        {
            frame.Paint.FillGradient(area, top, bottom, GradientAxis.Vertical);
            return;
        }

        const int bands = 20;
        var height = area.Height;
        var overlap = MathF.Max(1.2f, height / bands * 0.35f);
        for (var index = 0; index < bands; index++)
        {
            var start = index / (float)bands;
            var end = (index + 1) / (float)bands;
            var slice = new Rect(
                new Vector2(area.Min.X, area.Min.Y + height * start),
                new Vector2(area.Max.X, MathF.Min(area.Max.Y, area.Min.Y + height * end + overlap)));
            var color = top + (bottom - top) * ((start + end) * 0.5f);
            if (index == 0)
            {
                frame.Paint.Fill(slice, color, radius, Painting.Corner.Top);
                continue;
            }

            if (index == bands - 1)
            {
                frame.Paint.Fill(slice, color, radius, Painting.Corner.Bottom);
                continue;
            }

            frame.Paint.Fill(slice, color);
        }
    }

    private static float Corner(in AppletFrame frame, Rect area) =>
        MathF.Max(frame.Units(18f), MathF.Min(area.Width, area.Height) * 0.12f);
}
