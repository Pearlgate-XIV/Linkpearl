using System.Globalization;
using System.Text.Json;
using Linkpearl.Modules;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Clock;

internal enum ClockPane : byte
{
    Alarm = 0,
    World = 1,
    Timer = 2,
    Watch = 3,
}

internal enum ClockPage : byte
{
    Tabs = 0,
    EditAlarm = 1,
    PickCity = 2,
}

internal sealed class ClockBell
{
    public string Id { get; set; } = string.Empty;

    public int Hour { get; set; }

    public int Minute { get; set; }

    public int Days { get; set; }

    public bool Enabled { get; set; } = true;

    public string Label { get; set; } = string.Empty;

    public string LastFired { get; set; } = string.Empty;
}

internal sealed class ClockState
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static readonly ClockCity[] Catalog = FromWorld();

    private static ClockCity[] FromWorld()
    {
        var src = WorldZones.Catalog;
        var dest = new ClockCity[src.Length];
        for (var index = 0; index < src.Length; index++)
        {
            dest[index] = new ClockCity(src[index].Id, src[index].City, src[index].Place);
        }

        return dest;
    }

    private readonly string path;

    public ClockPane Pane { get; set; } = ClockPane.World;

    public ClockPage Page { get; set; } = ClockPage.Tabs;

    public float Scroll;

    public string CityHunt { get; set; } = string.Empty;

    public List<string> Cities { get; } = ["eorzea"];

    public List<ClockBell> Bells { get; } = [];

    public int DraftHour { get; set; } = 8;

    public int DraftMinute { get; set; }

    public int DraftDays { get; set; } = 0b0111110;

    public string DraftLabel { get; set; } = string.Empty;

    public string DraftId { get; set; } = string.Empty;

    public int TimerHour { get; set; }

    public int TimerMinute { get; set; } = 5;

    public int TimerSecond { get; set; }

    public DateTimeOffset? TimerEnds { get; set; }

    public TimeSpan TimerHold { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan TimerSet { get; set; } = TimeSpan.FromMinutes(5);

    public bool TimerDone { get; set; }

    public bool TimerRang { get; set; }

    public DateTimeOffset? WatchStart { get; set; }

    public TimeSpan WatchHold { get; set; }

    public List<TimeSpan> Laps { get; } = [];

    private ClockState(string path)
    {
        this.path = path;
    }

    public static ClockState Load(HostPaths paths)
    {
        var state = new ClockState(paths.State("clock.json"));
        if (!File.Exists(state.path))
        {
            return state;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<ClockSave>(File.ReadAllText(state.path), Json);
            if (dto is null)
            {
                return state;
            }

            state.Pane = (ClockPane)Math.Clamp(dto.Pane, 0, 3);
            state.Cities.Clear();
            if (dto.Cities is { Length: > 0 })
            {
                for (var index = 0; index < dto.Cities.Length; index++)
                {
                    var id = dto.Cities[index];
                    if (id.Length > 0 && !state.Cities.Contains(id, StringComparer.OrdinalIgnoreCase))
                    {
                        state.Cities.Add(id);
                    }
                }
            }

            if (state.Cities.Count == 0)
            {
                state.Cities.Add("eorzea");
            }

            if (dto.Bells is { Length: > 0 })
            {
                state.Bells.AddRange(dto.Bells);
            }

            if (dto.TimerSeconds > 0)
            {
                state.SetTimer(TimeSpan.FromSeconds(dto.TimerSeconds));
            }

            if (dto.TimerHoldMs > 0)
            {
                state.TimerHold = TimeSpan.FromMilliseconds(dto.TimerHoldMs);
            }

            if (dto.TimerEndUnix > 0)
            {
                state.TimerEnds = DateTimeOffset.FromUnixTimeMilliseconds(dto.TimerEndUnix);
                state.TimerDone = false;
                state.TimerRang = false;
            }
            else if (dto.TimerDone)
            {
                state.TimerDone = true;
                state.TimerHold = TimeSpan.Zero;
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }

        return state;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, JsonSerializer.Serialize(new ClockSave
            {
                Pane = (int)Pane,
                Cities = Cities.ToArray(),
                Bells = Bells.ToArray(),
                TimerSeconds = (int)Math.Round(TimerSet.TotalSeconds),
                TimerHoldMs = (long)Math.Round(TimerHold.TotalMilliseconds),
                TimerEndUnix = TimerEnds?.ToUnixTimeMilliseconds() ?? 0,
                TimerDone = TimerDone,
            }, Json));
        }
        catch (IOException)
        {
        }
    }

    public void OpenPane(ClockPane pane)
    {
        Pane = pane;
        Page = ClockPage.Tabs;
        Scroll = 0f;
        Save();
    }

    public void BeginAlarm(string id)
    {
        DraftId = id;
        if (FindBell(id) is { } bell)
        {
            DraftHour = bell.Hour;
            DraftMinute = bell.Minute;
            DraftDays = bell.Days;
            DraftLabel = bell.Label;
        }
        else
        {
            DraftHour = 8;
            DraftMinute = 0;
            DraftDays = 0b0111110;
            DraftLabel = string.Empty;
        }

        Page = ClockPage.EditAlarm;
        Scroll = 0f;
    }

    public void CommitAlarm()
    {
        var bell = FindBell(DraftId);
        if (bell is null)
        {
            bell = new ClockBell { Id = Guid.NewGuid().ToString("N") };
            Bells.Add(bell);
        }

        bell.Hour = Math.Clamp(DraftHour, 0, 23);
        bell.Minute = Math.Clamp(DraftMinute, 0, 59);
        bell.Days = DraftDays & 0b1111111;
        bell.Label = (DraftLabel ?? string.Empty).Trim();
        bell.Enabled = true;
        bell.LastFired = string.Empty;
        Page = ClockPage.Tabs;
        Scroll = 0f;
        Save();
    }

    public void DropAlarm(string id)
    {
        Bells.RemoveAll(row => string.Equals(row.Id, id, StringComparison.Ordinal));
        Page = ClockPage.Tabs;
        Scroll = 0f;
        Save();
    }

    public void ToggleAlarm(string id)
    {
        if (FindBell(id) is not { } bell)
        {
            return;
        }

        bell.Enabled = !bell.Enabled;
        if (bell.Enabled)
        {
            bell.LastFired = string.Empty;
        }

        Save();
    }

    public void AddCity(string id)
    {
        if (id.Length == 0 || Cities.Contains(id, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        Cities.Add(id);
        Page = ClockPage.Tabs;
        Scroll = 0f;
        Save();
    }

    public void DropCity(string id)
    {
        if (Cities.Count <= 1)
        {
            return;
        }

        Cities.RemoveAll(row => string.Equals(row, id, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    public void SetTimer(TimeSpan span)
    {
        if (span < TimeSpan.FromSeconds(1))
        {
            span = TimeSpan.FromSeconds(1);
        }

        TimerSet = span;
        TimerHold = span;
        TimerEnds = null;
        TimerDone = false;
        TimerRang = false;
        TimerHour = (int)span.TotalHours;
        TimerMinute = span.Minutes;
        TimerSecond = span.Seconds;
    }

    public void ApplyTimerDigits()
    {
        SetTimer(TimeSpan.FromHours(Math.Clamp(TimerHour, 0, 23)) +
                 TimeSpan.FromMinutes(Math.Clamp(TimerMinute, 0, 59)) +
                 TimeSpan.FromSeconds(Math.Clamp(TimerSecond, 0, 59)));
        Save();
    }

    public TimeSpan TimerLeft(DateTimeOffset utc)
    {
        if (TimerEnds is { } end)
        {
            var left = end - utc;
            if (left <= TimeSpan.Zero)
            {
                TimerDone = true;
                TimerEnds = null;
                TimerHold = TimeSpan.Zero;
                return TimeSpan.Zero;
            }

            return left;
        }

        return TimerHold;
    }

    public void ToggleTimer(DateTimeOffset utc)
    {
        var left = TimerLeft(utc);
        if (TimerDone || left <= TimeSpan.Zero)
        {
            TimerHold = TimerSet;
            TimerDone = false;
            TimerRang = false;
            TimerEnds = utc + TimerSet;
            return;
        }

        if (TimerEnds is { } end)
        {
            TimerHold = end - utc;
            if (TimerHold < TimeSpan.Zero)
            {
                TimerHold = TimeSpan.Zero;
            }

            TimerEnds = null;
            return;
        }

        TimerEnds = utc + TimerHold;
        TimerDone = false;
        TimerRang = false;
    }

    public void ResetTimer()
    {
        TimerHold = TimerSet;
        TimerEnds = null;
        TimerDone = false;
        TimerRang = false;
        TimerHour = (int)TimerSet.TotalHours;
        TimerMinute = TimerSet.Minutes;
        TimerSecond = TimerSet.Seconds;
    }

    public TimeSpan WatchElapsed(DateTimeOffset utc) =>
        WatchStart is { } start ? WatchHold + (utc - start) : WatchHold;

    public void ToggleWatch(DateTimeOffset utc)
    {
        if (WatchStart is { } start)
        {
            WatchHold += utc - start;
            WatchStart = null;
            return;
        }

        WatchStart = utc;
    }

    public void LapWatch(DateTimeOffset utc)
    {
        var elapsed = WatchElapsed(utc);
        if (elapsed <= TimeSpan.Zero)
        {
            return;
        }

        Laps.Add(elapsed);
    }

    public void ResetWatch()
    {
        WatchStart = null;
        WatchHold = TimeSpan.Zero;
        Laps.Clear();
    }

    public ClockBell? FindBell(string id)
    {
        if (id.Length == 0)
        {
            return null;
        }

        for (var index = 0; index < Bells.Count; index++)
        {
            if (string.Equals(Bells[index].Id, id, StringComparison.Ordinal))
            {
                return Bells[index];
            }
        }

        return null;
    }

    public static ClockCity? FindCity(string id)
    {
        for (var index = 0; index < Catalog.Length; index++)
        {
            if (string.Equals(Catalog[index].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return Catalog[index];
            }
        }

        return null;
    }

    public static bool DayOn(int days, DayOfWeek day) => (days & (1 << (int)day)) != 0;

    public static int ToggleDay(int days, DayOfWeek day) => days ^ (1 << (int)day);

    public static string DayLine(int days)
    {
        if (days == 0)
        {
            return "Once";
        }

        if (days == 0b1111111)
        {
            return "Every day";
        }

        if (days == 0b0111110)
        {
            return "Weekdays";
        }

        if (days == 0b1000001)
        {
            return "Weekends";
        }

        var marks = "SMTWTFS";
        var text = new char[7];
        var write = 0;
        for (var index = 0; index < 7; index++)
        {
            if ((days & (1 << index)) == 0)
            {
                continue;
            }

            text[write++] = marks[index];
        }

        return write == 0 ? "Once" : new string(text, 0, write);
    }

    public static DateTimeOffset NextRing(ClockBell bell, DateTimeOffset now)
    {
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, bell.Hour, bell.Minute, 0, now.Offset);
        if (bell.Days == 0)
        {
            return today > now ? today : today.AddDays(1);
        }

        for (var step = 0; step < 8; step++)
        {
            var next = today.AddDays(step);
            if (next <= now)
            {
                continue;
            }

            if (DayOn(bell.Days, next.DayOfWeek))
            {
                return next;
            }
        }

        return today.AddDays(1);
    }

    public static string UntilLine(ClockBell bell, DateTimeOffset now)
    {
        if (!bell.Enabled)
        {
            return "Off";
        }

        var wait = NextRing(bell, now) - now;
        if (wait < TimeSpan.Zero)
        {
            wait = TimeSpan.Zero;
        }

        var hours = (int)wait.TotalHours;
        var minutes = wait.Minutes;
        if (hours <= 0 && minutes <= 0)
        {
            return "Now";
        }

        if (hours <= 0)
        {
            return "In " + minutes.ToString(CultureInfo.InvariantCulture) + " min";
        }

        if (hours < 24)
        {
            return "In " + hours.ToString(CultureInfo.InvariantCulture) + " hr " +
                   minutes.ToString(CultureInfo.InvariantCulture) + " min";
        }

        var days = (int)wait.TotalDays;
        return "In " + days.ToString(CultureInfo.InvariantCulture) + (days == 1 ? " day" : " days");
    }

    public bool FireBells(DateTimeOffset now, out List<ClockBell> rang)
    {
        rang = [];
        var stamp = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dirty = false;
        for (var index = 0; index < Bells.Count; index++)
        {
            var bell = Bells[index];
            if (!bell.Enabled || string.Equals(bell.LastFired, stamp, StringComparison.Ordinal))
            {
                continue;
            }

            if (now.Hour != bell.Hour || now.Minute != bell.Minute)
            {
                continue;
            }

            if (bell.Days != 0 && !DayOn(bell.Days, now.DayOfWeek))
            {
                continue;
            }

            bell.LastFired = stamp;
            if (bell.Days == 0)
            {
                bell.Enabled = false;
            }

            rang.Add(bell);
            dirty = true;
        }

        if (dirty)
        {
            Save();
        }

        return dirty;
    }

    private sealed class ClockSave
    {
        public int Pane { get; set; } = 1;

        public string[]? Cities { get; set; }

        public ClockBell[]? Bells { get; set; }

        public int TimerSeconds { get; set; }

        public long TimerHoldMs { get; set; }

        public long TimerEndUnix { get; set; }

        public bool TimerDone { get; set; }
    }
}

internal readonly record struct ClockCity(string Id, string Name, string Place);
