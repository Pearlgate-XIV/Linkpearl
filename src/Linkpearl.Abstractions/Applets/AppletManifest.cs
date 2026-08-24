namespace Linkpearl.Applets;

[Flags]
public enum AppletCapabilities : uint
{
    None = 0,
    RequiresAccount = 1 << 0,
    RequiresNetwork = 1 << 1,
    PlaysAudio = 1 << 2,
    RendersVideo = 1 << 3,
    UsesCamera = 1 << 4,
    ReadsGameChat = 1 << 5,
    WritesGameChat = 1 << 6,
    WantsTransparentScreen = 1 << 7,
    WantsLandscape = 1 << 8,
    AgeRestricted = 1 << 9,
    BackgroundWork = 1 << 10,
}

public enum AppletFamily : byte
{
    System = 0,
    Life = 1,
    World = 2,
    Media = 3,
    Social = 4,
    Arcade = 5,
}

public sealed class AppletManifest
{
    public required string Id { get; init; }

    public required string DisplayNameKey { get; init; }

    public required AppletFamily Family { get; init; }

    public required string Glyph { get; init; }

    public AppletCapabilities Capabilities { get; init; } = AppletCapabilities.None;

    public bool RemovableFromHome { get; init; } = true;

    public bool EnabledByDefault { get; init; } = true;

    public int HomeOrder { get; init; }

    public bool Has(AppletCapabilities capability) => (Capabilities & capability) == capability;
}
