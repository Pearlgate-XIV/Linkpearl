using Linkpearl.Modules;

namespace Linkpearl.Applets;

// Live launcher faces are drawn as S25 squircles in AppMarks. The detailed PNG pack is kept
// under Icons/original so it can be brought back by flipping UseOriginalArt.
public static class AppIconCatalog
{
    public const string Folder = "Icons";

    public const string OriginalFolder = "original";

    public const string GlyphFolder = "glyphs";

    public const bool UseOriginalArt = false;

    public const string DaylightAsset = "daylight.png";

    public const string AnnouncementAsset = "announcement.png";

    public const string HomeGearAsset = "home_gear.png";

    public const string HomeMessagesAsset = "home_messages.png";

    public const string HomeMarketAsset = "home_market.png";

    public const string HomeEventAsset = "home_event.png";

    public const string HomeFriendsAsset = "home_friends.png";

    public const string HomePartyAsset = "home_party.png";

    public const string HomeRetainerAsset = "home_retainer.png";

    public const string HomeEditAsset = "home_edit.png";

    public const string HomePlaceAsset = "home_place.png";

    public const string HomePinAsset = "home_pin.png";

    public static string Absolute(HostPaths paths, string fileName) =>
        paths.Asset(Path.Combine(Folder, fileName));

    public static string Original(HostPaths paths, string fileName) =>
        paths.Asset(Path.Combine(Folder, OriginalFolder, fileName));

    public static string Glyph(HostPaths paths, string fileName) =>
        paths.Asset(Path.Combine(Folder, GlyphFolder, fileName));
}
