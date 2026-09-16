using System.Globalization;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Timer;

public sealed class TimerApplet : IApplet
{
    private static readonly int[] Presets = { 60, 300, 900, 1800 };

    public static readonly AppletManifest Manifest = new()
    {
        Id = "timer",
        DisplayNameKey = "Timer",
        Family = AppletFamily.Life,
        Glyph = "⏲",
        HomeOrder = 15,
    };

    private readonly IClock clock;
    private TimeSpan duration = TimeSpan.FromMinutes(5);
    private DateTimeOffset? endsAt;
    private TimeSpan remainingWhenPaused = TimeSpan.FromMinutes(5);
    private bool finished;

    public TimerApplet(IClock clock)
    {
        this.clock = clock;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge
    {
        get
        {
            Remaining();
            return finished ? new AppletBadge(1, asDot: true) : AppletBadge.None;
        }
    }

    public void Enter(AppletEntry entry)
    {
    }

    public void Leave()
    {
    }

    public void Compose(in AppletFrame frame)
    {
        var remaining = Remaining();
        var content = frame.Content.Inset(frame.Units(16f));
        var stack = new LayoutFlow(content, StackAxis.Vertical, frame.Units(10f));
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Timer",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));

        var face = stack.Take(frame.Units(110f));
        CardChrome.Draw(frame, face, finished || remaining <= TimeSpan.Zero && endsAt is not null ? 2f : 1f);
        var label = finished ? "Done" : Format(remaining);
        frame.Text.DrawIn(face, label,
            new TextStyle(FontRole.Display, finished ? frame.Theme.Palette.Accent : frame.Theme.Palette.Ink,
                TextAlign.Center));

        var presets = stack.Take(frame.Units(40f));
        DrawPresets(frame, presets);

        var bump = stack.Take(frame.Units(40f));
        DrawBump(frame, bump.LeftSlice((bump.Width - frame.Units(8f)) * 0.5f), "− 30s", -30);
        DrawBump(frame, bump.RightSlice((bump.Width - frame.Units(8f)) * 0.5f), "+ 30s", 30);

        var running = endsAt is not null && !finished;
        var actions = stack.Take(frame.Units(48f));
        DrawAction(frame, actions.LeftSlice((actions.Width - frame.Units(8f)) * 0.5f),
            running ? "Pause" : remaining <= TimeSpan.Zero ? "Again" : "Start", frame.Theme.Palette.Accent, Toggle);
        DrawAction(frame, actions.RightSlice((actions.Width - frame.Units(8f)) * 0.5f), "Reset",
            frame.Theme.Palette.SurfaceRaised, Reset);
    }

    private TimeSpan Remaining()
    {
        if (endsAt is { } end)
        {
            var left = end - clock.UtcNow;
            if (left <= TimeSpan.Zero)
            {
                finished = true;
                endsAt = null;
                remainingWhenPaused = TimeSpan.Zero;
                return TimeSpan.Zero;
            }

            return left;
        }

        return remainingWhenPaused;
    }

    private void DrawPresets(in AppletFrame frame, Rect row)
    {
        var gap = frame.Units(6f);
        var width = (row.Width - gap * (Presets.Length - 1)) / Presets.Length;
        for (var index = 0; index < Presets.Length; index++)
        {
            var seconds = Presets[index];
            var cell = Rect.FromSize(new Vector2(row.Min.X + index * (width + gap), row.Min.Y),
                new Vector2(width, row.Height));
            var active = Math.Abs(duration.TotalSeconds - seconds) < 0.5 && endsAt is null;
            CardChrome.Draw(frame, cell, active ? 2f : 1f);
            frame.Text.DrawIn(cell, Label(seconds),
                new TextStyle(FontRole.CaptionStrong, active ? frame.Theme.Palette.Accent : frame.Theme.Palette.Ink,
                    TextAlign.Center));
            if (frame.Input.ConsumeClick(cell))
            {
                SetDuration(TimeSpan.FromSeconds(seconds));
            }
        }
    }

    private void DrawBump(in AppletFrame frame, Rect cell, string label, int seconds)
    {
        CardChrome.Draw(frame, cell);
        frame.Text.DrawIn(cell, label, new TextStyle(FontRole.Body, frame.Theme.Palette.Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(cell) && endsAt is null)
        {
            var next = duration + TimeSpan.FromSeconds(seconds);
            if (next < TimeSpan.FromSeconds(30))
            {
                next = TimeSpan.FromSeconds(30);
            }

            SetDuration(next);
        }
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

    private void SetDuration(TimeSpan value)
    {
        duration = value;
        remainingWhenPaused = value;
        endsAt = null;
        finished = false;
    }

    private void Toggle()
    {
        if (finished || remainingWhenPaused <= TimeSpan.Zero)
        {
            remainingWhenPaused = duration;
            finished = false;
            endsAt = clock.UtcNow + duration;
            return;
        }

        if (endsAt is { } end)
        {
            remainingWhenPaused = end - clock.UtcNow;
            if (remainingWhenPaused < TimeSpan.Zero)
            {
                remainingWhenPaused = TimeSpan.Zero;
            }

            endsAt = null;
            return;
        }

        endsAt = clock.UtcNow + remainingWhenPaused;
        finished = false;
    }

    private void Reset()
    {
        remainingWhenPaused = duration;
        endsAt = null;
        finished = false;
    }

    private static string Format(TimeSpan span)
    {
        var total = Math.Max((int)Math.Ceiling(span.TotalSeconds), 0);
        var minutes = total / 60;
        var seconds = total % 60;
        return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
            seconds.ToString("00", CultureInfo.InvariantCulture);
    }

    private static string Label(int seconds) =>
        seconds >= 60
            ? (seconds / 60).ToString(CultureInfo.InvariantCulture) + "m"
            : seconds.ToString(CultureInfo.InvariantCulture) + "s";
}