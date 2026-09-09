using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Clock;

public sealed partial class ClockApplet : IApplet, IDisposable
{
    public static readonly AppletManifest Manifest = new()
    {
        Id = "clock",
        DisplayNameKey = "Clock",
        Family = AppletFamily.Life,
        Glyph = "⏰",
        HomeOrder = 0,
        Capabilities = AppletCapabilities.BackgroundWork,
    };

    private static readonly string[] Panes = { "Alarm", "Clock", "Timer", "Stopwatch" };
    private static readonly int[] TimerPresets = { 60, 300, 600, 900 };

    private readonly IClock clock;
    private readonly DisplayPreferences display;
    private readonly IFrameLoop frames;
    private readonly IChime chime;
    private readonly ClockState state;

    public ClockApplet(IClock clock, DisplayPreferences display, IFrameLoop frames, IChime chime, HostPaths paths)
    {
        this.clock = clock;
        this.display = display;
        this.frames = frames;
        this.chime = chime;
        state = ClockState.Load(paths);
        frames.Tick += OnTick;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge
    {
        get
        {
            _ = state.TimerLeft(clock.UtcNow);
            return state.TimerDone ? new AppletBadge(1, asDot: true) : AppletBadge.None;
        }
    }

    public string Place => state.Page == ClockPage.Tabs ? Panes[(int)state.Pane] : state.Page.ToString();

    public bool CanGoBack => state.Page != ClockPage.Tabs;

    public void Enter(AppletEntry entry)
    {
        if (entry.RouteHint is not { Length: > 0 } hint)
        {
            return;
        }

        if (hint.Equals("alarm", StringComparison.OrdinalIgnoreCase))
        {
            state.OpenPane(ClockPane.Alarm);
        }
        else if (hint.Equals("timer", StringComparison.OrdinalIgnoreCase))
        {
            state.OpenPane(ClockPane.Timer);
        }
        else if (hint.Equals("stopwatch", StringComparison.OrdinalIgnoreCase))
        {
            state.OpenPane(ClockPane.Watch);
        }
        else if (hint.Equals("clock", StringComparison.OrdinalIgnoreCase))
        {
            state.OpenPane(ClockPane.World);
        }
    }

    public void Leave() => state.Save();

    public bool Back()
    {
        if (state.Page == ClockPage.Tabs)
        {
            return false;
        }

        state.Page = ClockPage.Tabs;
        state.Scroll = 0f;
        return true;
    }

    public void Dispose()
    {
        frames.Tick -= OnTick;
        state.Save();
    }

    public void Compose(in AppletFrame frame)
    {
        _ = state.TimerLeft(clock.UtcNow);
        if (state.TimerDone && !state.TimerRang)
        {
            state.TimerRang = true;
            state.Save();
            chime.Ring("Timer", "Time's up");
        }

        if (state.Page == ClockPage.EditAlarm)
        {
            DrawEditAlarm(frame);
            return;
        }

        if (state.Page == ClockPage.PickCity)
        {
            DrawPickCity(frame);
            return;
        }

        var nav = frame.Content.BottomSlice(frame.Units(ClockChrome.NavUnits));
        var body = frame.Content.Inset(new Edges(0f, 0f, 0f, nav.Height));
        switch (state.Pane)
        {
            case ClockPane.Alarm:
                DrawAlarms(frame, body);
                break;
            case ClockPane.Timer:
                DrawTimer(frame, body);
                break;
            case ClockPane.Watch:
                DrawWatch(frame, body);
                break;
            default:
                DrawWorld(frame, body);
                break;
        }

        DrawNav(frame, nav);
    }

    private void DrawNav(in AppletFrame frame, Rect row)
    {
        frame.Paint.Fill(row, frame.Theme.Palette.Surface with { W = 0.94f });
        frame.Paint.Line(new Vector2(row.Min.X, row.Min.Y), new Vector2(row.Max.X, row.Min.Y),
            frame.Theme.Palette.Separator, frame.Theme.Metrics.Hairline);
        var width = row.Width / Panes.Length;
        for (var index = 0; index < Panes.Length; index++)
        {
            var cell = Rect.FromSize(new Vector2(row.Min.X + width * index, row.Min.Y),
                new Vector2(width, row.Height));
            var pane = (ClockPane)index;
            var on = state.Pane == pane;
            var icon = cell.TopSlice(cell.Height * 0.62f);
            if (on)
            {
                var pill = Rect.FromSize(
                    new Vector2(icon.Center.X - frame.Units(20f), icon.Center.Y - frame.Units(13f)),
                    new Vector2(frame.Units(40f), frame.Units(26f)));
                frame.Paint.Fill(pill, Accent(frame) with { W = 0.22f }, pill.Height * 0.5f);
            }

            ClockChrome.NavGlyph(frame, icon.Center, frame.Units(9f), pane,
                on ? Accent(frame) : frame.Theme.Palette.InkMuted);
            frame.Text.DrawIn(cell.BottomSlice(frame.Units(18f)), Panes[index],
                new TextStyle(FontRole.Caption, on ? Accent(frame) : frame.Theme.Palette.InkMuted, TextAlign.Center,
                    scale: 0.88f));
            if (frame.Input.ConsumeClick(cell))
            {
                state.OpenPane(pane);
            }
        }
    }

    private void OnTick(float _)
    {
        state.TimerLeft(clock.UtcNow);
        if (state.TimerDone && !state.TimerRang)
        {
            state.TimerRang = true;
            state.Save();
            chime.Ring("Timer", "Time's up");
        }

        if (!state.FireBells(clock.Now, out var rang))
        {
            return;
        }

        for (var index = 0; index < rang.Count; index++)
        {
            var bell = rang[index];
            var title = bell.Label.Length > 0 ? bell.Label : "Alarm";
            chime.Ring(title, Stamp(bell.Hour, bell.Minute, display.Use24HourClock));
        }
    }

    private static Vector4 Accent(in AppletFrame frame) => frame.Theme.AccentFor(Manifest.Id);

    private static string Stamp(int hour, int minute, bool twentyFour)
    {
        if (twentyFour)
        {
            return hour.ToString("00", CultureInfo.InvariantCulture) + ":" +
                   minute.ToString("00", CultureInfo.InvariantCulture);
        }

        var hour12 = hour % 12;
        if (hour12 == 0)
        {
            hour12 = 12;
        }

        return hour12.ToString(CultureInfo.InvariantCulture) + ":" +
               minute.ToString("00", CultureInfo.InvariantCulture);
    }

    private static string Period(int hour) => hour < 12 ? "AM" : "PM";

    private static void HandsOf(DateTimeOffset when, out float hour, out float minute, out float second)
    {
        hour = when.Hour + when.Minute / 60f + when.Second / 3600f;
        minute = when.Minute + when.Second / 60f + when.Millisecond / 60000f;
        second = when.Second + when.Millisecond / 1000f;
    }

    private static void HandsOfEorzea(DateTimeOffset utc, out float hour, out float minute, out float second)
    {
        var et = utc.ToUnixTimeSeconds() * EorzeaTime.EarthToEorzea;
        var day = et % 86400.0;
        if (day < 0.0)
        {
            day += 86400.0;
        }

        hour = (float)(day / 3600.0);
        minute = (float)((day % 3600.0) / 60.0);
        second = (float)(day % 60.0);
    }

    private bool TryCityTime(string id, out DateTimeOffset when, out bool eorzea)
    {
        eorzea = string.Equals(id, "eorzea", StringComparison.OrdinalIgnoreCase);
        if (eorzea)
        {
            when = clock.UtcNow;
            return true;
        }

        try
        {
            when = TimeZoneInfo.ConvertTime(clock.Now, TimeZoneInfo.FindSystemTimeZoneById(id));
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            when = default;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            when = default;
            return false;
        }
    }

    private static string ClockLabel(TimeSpan span)
    {
        var total = Math.Max((int)Math.Ceiling(span.TotalSeconds), 0);
        var hours = total / 3600;
        var minutes = total % 3600 / 60;
        var seconds = total % 60;
        if (hours > 0)
        {
            return hours.ToString(CultureInfo.InvariantCulture) + ":" +
                   minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
                   seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
               seconds.ToString("00", CultureInfo.InvariantCulture);
    }

    private static string WatchLabel(TimeSpan span)
    {
        var minutes = (int)span.TotalMinutes;
        var seconds = span.Seconds;
        var hundredths = span.Milliseconds / 10;
        return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
               seconds.ToString("00", CultureInfo.InvariantCulture) + "." +
               hundredths.ToString("00", CultureInfo.InvariantCulture);
    }

    private static string DayLetter(int index) => index switch
    {
        0 => "S",
        1 => "M",
        2 => "T",
        3 => "W",
        4 => "T",
        5 => "F",
        _ => "S",
    };
}
