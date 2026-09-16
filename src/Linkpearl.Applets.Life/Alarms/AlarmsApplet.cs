using System.Globalization;
using System.Text.Json;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Alarms;

public sealed class AlarmsApplet : IApplet, IDisposable
{
    public static readonly AppletManifest Manifest = new()
    {
        Id = "alarms",
        DisplayNameKey = "Alarms",
        Family = AppletFamily.Life,
        Glyph = "🔔",
        HomeOrder = 3,
        Capabilities = AppletCapabilities.BackgroundWork,
    };

    private readonly IClock clock;
    private readonly IFrameLoop frames;
    private readonly IChime chime;
    private readonly string path;
    private readonly List<AlarmItem> items = new();
    private int hour = 8;
    private int minute;

    public AlarmsApplet(IClock clock, IFrameLoop frames, IChime chime, HostPaths paths)
    {
        this.clock = clock;
        this.frames = frames;
        this.chime = chime;
        path = paths.State("alarms.json");
        Load();
        frames.Tick += OnTick;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public void Enter(AppletEntry entry)
    {
    }

    public void Leave()
    {
    }

    public void Dispose()
    {
        frames.Tick -= OnTick;
        Save();
    }

    public void Compose(in AppletFrame frame)
    {
        var content = frame.Content.Inset(frame.Units(16f));
        var stack = new LayoutFlow(content, StackAxis.Vertical, frame.Units(10f));
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Alarms",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));

        var setter = stack.Take(frame.Units(44f));
        DrawStepper(frame, setter.LeftSlice(setter.Width * 0.38f), ref hour, 0, 23, "h");
        DrawStepper(frame, setter.Inset(new Edges(setter.Width * 0.40f, 0f, setter.Width * 0.40f, 0f)), ref minute, 0,
            59, "m");
        var add = setter.RightSlice(setter.Width * 0.36f);
        CardChrome.DrawGold(frame, add);
        frame.Text.DrawIn(add, "Add",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
        if (frame.Input.ConsumeClick(add))
        {
            items.Add(new AlarmItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Hour = hour,
                Minute = minute,
                Enabled = true,
            });
            Save();
        }

        if (items.Count == 0)
        {
            var empty = stack.Take(frame.Units(72f));
            CardChrome.Draw(frame, empty);
            frame.Text.DrawIn(empty.Inset(frame.Units(12f)), "No bells set.",
                new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Center));
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            DrawItem(frame, stack.Take(frame.Units(56f)), items[index]);
        }
    }

    private void DrawItem(in AppletFrame frame, Rect row, AlarmItem item)
    {
        CardChrome.Draw(frame, row);
        var inset = row.Inset(frame.Units(12f));
        var stamp = item.Hour.ToString("00", CultureInfo.InvariantCulture) + ":" +
            item.Minute.ToString("00", CultureInfo.InvariantCulture);
        frame.Text.DrawIn(inset.LeftSlice(inset.Width * 0.36f), stamp,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        var toggle = inset.Inset(new Edges(inset.Width * 0.38f, 0f, inset.Width * 0.28f, 0f));
        CardChrome.Draw(frame, toggle, item.Enabled ? 2f : 1f);
        frame.Text.DrawIn(toggle, item.Enabled ? "On" : "Off",
            new TextStyle(FontRole.CaptionStrong, item.Enabled ? frame.Theme.Palette.Accent : frame.Theme.Palette.Ink,
                TextAlign.Center));
        if (frame.Input.ConsumeClick(toggle))
        {
            item.Enabled = !item.Enabled;
            Save();
        }

        var drop = inset.RightSlice(inset.Width * 0.24f);
        frame.Text.DrawIn(drop, "Clear",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.Negative, TextAlign.Center));
        if (frame.Input.ConsumeClick(drop))
        {
            items.Remove(item);
            Save();
        }
    }

    private static void DrawStepper(in AppletFrame frame, Rect area, ref int value, int min, int max, string suffix)
    {
        CardChrome.Draw(frame, area);
        var down = area.LeftSlice(area.Width * 0.28f);
        var up = area.RightSlice(area.Width * 0.28f);
        frame.Text.DrawIn(down, "−", new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, TextAlign.Center));
        frame.Text.DrawIn(up, "+", new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, TextAlign.Center));
        frame.Text.DrawIn(area, value.ToString("00", CultureInfo.InvariantCulture) + suffix,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(down))
        {
            value = value <= min ? max : value - 1;
        }

        if (frame.Input.ConsumeClick(up))
        {
            value = value >= max ? min : value + 1;
        }
    }

    private void OnTick(float _)
    {
        var now = clock.Now;
        var stamp = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dirty = false;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (!item.Enabled || item.LastFired == stamp)
            {
                continue;
            }

            if (now.Hour != item.Hour || now.Minute != item.Minute)
            {
                continue;
            }

            item.LastFired = stamp;
            dirty = true;
            chime.Ring("Alarm", item.Hour.ToString("00", CultureInfo.InvariantCulture) + ":" +
                item.Minute.ToString("00", CultureInfo.InvariantCulture));
        }

        if (dirty)
        {
            Save();
        }
    }

    private void Load()
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<List<AlarmItem>>(json);
            if (loaded is null)
            {
                return;
            }

            items.Clear();
            items.AddRange(loaded);
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, JsonSerializer.Serialize(items));
        }
        catch (IOException)
        {
        }
    }

    private sealed class AlarmItem
    {
        public string Id { get; set; } = string.Empty;

        public int Hour { get; set; }

        public int Minute { get; set; }

        public bool Enabled { get; set; }

        public string LastFired { get; set; } = string.Empty;
    }
}
