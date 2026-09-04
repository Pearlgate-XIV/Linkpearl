using Linkpearl.Destinations;

namespace Linkpearl.Applets;

public enum AppKind : byte
{
    Applet = 0,
    Shortcut = 1,
}

public enum AppChip : byte
{
    All = 0,
    Social = 1,
    Music = 2,
    Utility = 3,
    System = 4,
    Favorites = 5,
}

public enum AppGroup : byte
{
    Essentials = 0,
    Life = 1,
    Tools = 2,
}

public readonly struct AppSpec
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Caption { get; init; }

    public required AppGroup Group { get; init; }

    public required AppChip Chip { get; init; }

    public required AppKind Kind { get; init; }

    public DestinationTab Tab { get; init; }

    public int Pane { get; init; }

    public bool OnShelfByDefault { get; init; }

    public string IconAsset { get; init; }
}

public static class AppShelf
{
    public static readonly AppSpec[] Catalog =
    {
        Spec("pearlchat", "PearlChat", "Messaging", AppGroup.Essentials, AppChip.Social, AppKind.Shortcut,
            DestinationTab.Social, SocialPane.Messages, true, "pearlchat.png"),
        Spec("phone", "Phone", "Communication", AppGroup.Essentials, AppChip.Social, AppKind.Shortcut,
            DestinationTab.Social, SocialPane.Phone, true, "phone.png"),
        Spec("music", "Music", "Media Player", AppGroup.Essentials, AppChip.Music, AppKind.Applet, default, 0, true,
            "music.png"),
        Spec("afterdark", "Daylight", "World Map", AppGroup.Essentials, AppChip.Social, AppKind.Applet, default, 0,
            true, AppIconCatalog.DaylightAsset),
        Spec("weather", "Weather", "Forecast", AppGroup.Essentials, AppChip.Utility, AppKind.Applet, default, 0, true,
            "weather.png"),
        Spec("calendar", "Calendar", "Schedule", AppGroup.Essentials, AppChip.Utility, AppKind.Applet, default, 0,
            true, "calendar.png"),
        Spec("wallet", "Pearls", "Currency", AppGroup.Essentials, AppChip.Utility, AppKind.Applet, default, 0, true,
            "wallet.png"),
        Spec("camera", "Camera", "Capture", AppGroup.Essentials, AppChip.Utility, AppKind.Applet, default, 0, true,
            "camera.png"),
        Spec("friends", "Friends", "Social", AppGroup.Life, AppChip.Social, AppKind.Shortcut, DestinationTab.Social,
            SocialPane.People, true, "friends.png"),
        Spec("market", "Market Watch", "Economy", AppGroup.Life, AppChip.Utility, AppKind.Shortcut,
            DestinationTab.Explore, ExplorePane.Places, true, "market_watch.png"),
        Spec("retainer", "Retainer", "Companions", AppGroup.Life, AppChip.Utility, AppKind.Shortcut, DestinationTab.Home,
            HomePane.Dashboard, true, "retainer.png"),
        Spec("events", "Events", "Activities", AppGroup.Life, AppChip.Social, AppKind.Shortcut, DestinationTab.Explore,
            ExplorePane.Events, true, "events.png"),
        Spec("place", "Place", "Navigation", AppGroup.Life, AppChip.Utility, AppKind.Applet, default, 0, true,
            "place.png"),
        Spec("eorzea", "Eorzea", "Game menus", AppGroup.Life, AppChip.Utility, AppKind.Applet, default, 0, true,
            "events.png"),
        Spec("settings", "Settings", "System", AppGroup.Tools, AppChip.System, AppKind.Shortcut, DestinationTab.Settings,
            SettingsPane.Front, true, "settings.png"),
        Spec("feedback", "Feedback", "Discord", AppGroup.Tools, AppChip.System, AppKind.Applet, default, 0, true,
            "announcement.png"),
        Spec("notes", "Notes", "Lists", AppGroup.Tools, AppChip.Utility, AppKind.Applet, default, 0, true, "notes.png"),
        Spec("alarms", "Alarms", "Reminders", AppGroup.Tools, AppChip.Utility, AppKind.Applet, default, 0, true,
            "alarms.png"),
        Spec("clock", "Clock", "Time", AppGroup.Tools, AppChip.Utility, AppKind.Applet, default, 0, false, "clock.png"),
        Spec("calculator", "Calculator", "Math", AppGroup.Tools, AppChip.Utility, AppKind.Applet, default, 0, false,
            "calculator.png"),
        Spec("timer", "Timer", "Countdown", AppGroup.Tools, AppChip.Utility, AppKind.Applet, default, 0, false,
            "timer.png"),
        Spec("stopwatch", "Stopwatch", "Laps", AppGroup.Tools, AppChip.Utility, AppKind.Applet, default, 0, false,
            "stopwatch.png"),
    };

    public static readonly string[] DefaultInstalled =
    {
        "pearlchat", "phone", "music", "afterdark", "weather", "calendar", "wallet", "camera", "friends", "settings",
        "feedback", "market", "retainer", "events", "place", "eorzea", "notes", "alarms",
    };

    public static readonly string[] ChipLabels = { "All", "Social", "Music", "Utility", "System", "Favorites" };

    public static readonly string[] GroupLabels = { "Essentials", "Life & Productivity", "Tools" };

    public static AppSpec? Find(string id)
    {
        for (var index = 0; index < Catalog.Length; index++)
        {
            if (string.Equals(Catalog[index].Id, id, StringComparison.Ordinal))
            {
                return Catalog[index];
            }
        }

        return null;
    }

    public static bool Known(string id) => Find(id) is not null || id.StartsWith("folder:", StringComparison.Ordinal);

    private static AppSpec Spec(string id, string name, string caption, AppGroup group, AppChip chip, AppKind kind,
        DestinationTab tab, int pane, bool onShelf, string iconAsset) =>
        new()
        {
            Id = id,
            Name = name,
            Caption = caption,
            Group = group,
            Chip = chip,
            Kind = kind,
            Tab = tab,
            Pane = pane,
            OnShelfByDefault = onShelf,
            IconAsset = iconAsset,
        };
}
