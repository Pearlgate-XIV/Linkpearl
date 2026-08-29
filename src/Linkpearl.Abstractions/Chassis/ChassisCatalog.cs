using Linkpearl.Geometry;

namespace Linkpearl.Chassis;

// Bundled device skins. Insets and button bands are fractions of the texture, which is also the
// window: Phone and Tablet windows keep this aspect so the nubs stay on the metal.
public readonly struct ChassisPlate
{
    public readonly string FileName;
    public readonly float Aspect;
    public readonly float ScreenLeft;
    public readonly float ScreenTop;
    public readonly float ScreenRight;
    public readonly float ScreenBottom;
    public readonly float VolumeTop;
    public readonly float VolumeBottom;
    public readonly float PowerTop;
    public readonly float PowerBottom;
    public readonly float BodyLeft;
    public readonly float BodyTop;
    public readonly float BodyRight;
    public readonly float BodyBottom;
    public readonly float Corner;

    public ChassisPlate(string fileName, float width, float height, float screenLeft, float screenTop,
        float screenRight, float screenBottom, float volumeTop, float volumeBottom, float powerTop,
        float powerBottom, float bodyLeft, float bodyTop, float bodyRight, float bodyBottom, float corner)
    {
        FileName = fileName;
        Aspect = width / height;
        ScreenLeft = screenLeft;
        ScreenTop = screenTop;
        ScreenRight = screenRight;
        ScreenBottom = screenBottom;
        VolumeTop = volumeTop;
        VolumeBottom = volumeBottom;
        PowerTop = powerTop;
        PowerBottom = powerBottom;
        BodyLeft = bodyLeft;
        BodyTop = bodyTop;
        BodyRight = bodyRight;
        BodyBottom = bodyBottom;
        Corner = corner;
    }

    public Rect ScreenOn(Rect window) =>
        window.Inset(new Edges(window.Width * ScreenLeft, window.Height * ScreenTop,
            window.Width * ScreenRight, window.Height * ScreenBottom));

    public Rect BodyOn(Rect window) =>
        window.Inset(new Edges(window.Width * BodyLeft, window.Height * BodyTop,
            window.Width * BodyRight, window.Height * BodyBottom));

    public float CornerOn(Rect window) => window.Width * Corner;

    public Rect PowerOn(Rect window) => SideNub(window, PowerTop, PowerBottom);

    public Rect VolumeOn(Rect window) => SideNub(window, VolumeTop, VolumeBottom);

    private Rect SideNub(Rect window, float topFrac, float bottomFrac)
    {
        var left = window.Max.X - window.Width * ScreenRight;
        var top = window.Min.Y + window.Height * topFrac;
        var bottom = window.Min.Y + window.Height * bottomFrac;
        return new Rect(new Vector2(left, top), new Vector2(window.Max.X, bottom));
    }
}

public static class ChassisCatalog
{
    public const string Folder = "Chassis";

    public static ChassisPlate Phone { get; } = new("phone.png", 517f, 1008f,
        17f / 517f, 17f / 1008f, 23f / 517f, 16f / 1008f,
        206f / 1008f, 332f / 1008f, 393f / 1008f, 462f / 1008f,
        4f / 517f, 4f / 1008f, 9f / 517f, 4f / 1008f, 27f / 517f);

    public static ChassisPlate Tablet { get; } = new("tablet.png", 710f, 987f,
        18f / 710f, 17f / 987f, 23f / 710f, 16f / 987f,
        206f / 987f, 310f / 987f, 382f / 987f, 411f / 987f,
        5f / 710f, 4f / 987f, 9f / 710f, 4f / 987f, 33f / 710f);

    public static ChassisPlate For(HandsetForm form) => form == HandsetForm.Tablet ? Tablet : Phone;
}
