using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Stopwatch;

public sealed class StopwatchApplet : IApplet
{
    private const int MaxLaps = 8;

    public static readonly AppletManifest Manifest = new()
    {
        Id = "stopwatch",
        DisplayNameKey = "Stopwatch",
        Family = AppletFamily.Life,
        Glyph = "⏱",
        HomeOrder = 20,
    };

    private readonly IClock clock;
    private readonly List<TimeSpan> laps = [];
    private DateTimeOffset? startedAt;
    private TimeSpan accumulated;

    public StopwatchApplet(IClock clock)
    {
        this.clock = clock;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public void Enter(AppletEntry entry)
    {
    }

    public void Leave()
    {
    }

    public void Compose(in AppletFrame frame)
    {
        var elapsed = Elapsed();
        var content = frame.Content.Inset(frame.Units(16f));
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Stopwatch",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));

        var face = stack.Take(frame.Units(110f));
        CardChrome.Draw(frame, face, startedAt is not null ? 2f : 1f);
        frame.Text.DrawIn(face, Format(elapsed),
            new TextStyle(FontRole.Display, frame.Theme.Palette.Ink, TextAlign.Center));

        var running = startedAt is not null;
        var actions = stack.Take(frame.Units(48f));
        var third = (actions.Width - frame.Units(12f)) / 3f;
        DrawAction(frame, Rect.FromSize(actions.Min, new Vector2(third, actions.Height)),
            running ? "Stop" : "Start", frame.Theme.Palette.Accent, Toggle);
        DrawAction(frame, Rect.FromSize(new Vector2(actions.Min.X + third + frame.Units(6f), actions.Min.Y),
            new Vector2(third, actions.Height)), "Lap", frame.Theme.Palette.SurfaceRaised, Lap);
        DrawAction(frame, Rect.FromSize(new Vector2(actions.Max.X - third, actions.Min.Y),
            new Vector2(third, actions.Height)), "Reset", frame.Theme.Palette.SurfaceRaised, Reset);

        for (var index = laps.Count - 1; index >= 0; index--)
        {
            var row = stack.Take(frame.Units(32f));
            frame.Text.DrawIn(row.LeftSlice(row.Width * 0.4f),
                "Lap " + (index + 1).ToString(CultureInfo.InvariantCulture),
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            frame.Text.DrawIn(row, Format(laps[index]),
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink, TextAlign.Right));
        }
    }

    private TimeSpan Elapsed()
    {
        if (startedAt is { } start)
        {
            return accumulated + (clock.UtcNow - start);
        }

        return accumulated;
    }

    private void Toggle()
    {
        if (startedAt is { } start)
        {
            accumulated += clock.UtcNow - start;
            startedAt = null;
            return;
        }

        startedAt = clock.UtcNow;
    }

    private void Lap()
    {
        if (startedAt is null && accumulated <= TimeSpan.Zero)
        {
            return;
        }

        if (laps.Count >= MaxLaps)
        {
            laps.RemoveAt(0);
        }

        laps.Add(Elapsed());
    }

    private void Reset()
    {
        startedAt = null;
        accumulated = TimeSpan.Zero;
        laps.Clear();
    }

    private static void DrawAction(in AppletFrame frame, Rect cell, string label, Vector4 fill, Action onTap)
    {
        frame.Paint.Fill(cell, fill, frame.Units(14f));
        var ink = fill.Equals(frame.Theme.Palette.Accent) ? frame.Theme.Palette.AccentInk : frame.Theme.Palette.Ink;
        frame.Text.DrawIn(cell, label, new TextStyle(FontRole.BodyStrong, ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(cell))
        {
            onTap();
        }
    }

    private static string Format(TimeSpan span)
    {
        var minutes = (int)span.TotalMinutes;
        var seconds = span.Seconds;
        var tenths = span.Milliseconds / 100;
        return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
            seconds.ToString("00", CultureInfo.InvariantCulture) + "." +
            tenths.ToString(CultureInfo.InvariantCulture);
    }
}