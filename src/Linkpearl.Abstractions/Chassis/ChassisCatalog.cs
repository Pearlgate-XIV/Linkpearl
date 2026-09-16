using Linkpearl.Geometry;

namespace Linkpearl.Chassis;

// Bundled device skins. Insets and button bands are fractions of the texture, which is also the
// window: Phone and Tablet windows keep this aspect so the nubs stay on the metal.
public readonly struct ChassisPlate
{
    public string FileName { get; }
    public float Aspect { get; }
    public float ScreenLeft { get; }
    public float ScreenTop { get; }
    public float ScreenRight { get; }
    public float ScreenBottom { get; }
    public float VolumeTop { get; }
    public float VolumeBottom { get; }
    public float PowerTop { get; }
    public float PowerBottom { get; }
    public float BodyLeft { get; }
    public float BodyTop { get; }
    public float BodyRight { get; }
    public float BodyBottom { get; }
    public float Corner { get; }
    public float ScreenCorner { get; }
    public float Gasket { get; }
    public string BackFileName { get; }

    public ChassisPlate(string fileName, float width, float height, float screenLeft, float screenTop,
        float screenRight, float screenBottom, float volumeTop, float volumeBottom, float powerTop,
        float powerBottom, float bodyLeft, float bodyTop, float bodyRight, float bodyBottom, float corner,
        float screenCorner = 0f, float gasket = 0f, string backFileName = "")
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
        ScreenCorner = screenCorner;
        Gasket = gasket;
        BackFileName = backFileName ?? string.Empty;
    }

    public bool HasBack => BackFileName.Length > 0;

    public Rect ScreenOn(Rect window) =>
        window.Inset(new Edges(window.Width * ScreenLeft, window.Height * ScreenTop,
            window.Width * ScreenRight, window.Height * ScreenBottom));

    public Rect BodyOn(Rect window) =>
        window.Inset(new Edges(window.Width * BodyLeft, window.Height * BodyTop,
            window.Width * BodyRight, window.Height * BodyBottom));

    public float CornerOn(Rect window) => window.Width * Corner;

    public float ScreenRadiusOn(Rect window)
    {
        float raw;
        if (ScreenCorner > 0.0001f)
        {
            raw = window.Width * ScreenCorner;
        }
        else
        {
            var body = BodyOn(window);
            var glass = ScreenOn(window);
            var inset = MathF.Max(0f, MathF.Min(glass.Min.X - body.Min.X, glass.Min.Y - body.Min.Y));
            raw = MathF.Max(CornerOn(window) - inset, 0f);
        }

        return TightenHole(window, raw);
    }

    public float GasketOn(Rect window)
    {
        if (Gasket > 0.0001f)
        {
            return window.Width * Gasket;
        }

        return ScreenCorner > 0.0001f ? MathF.Max(1.9f, window.Width * 0.007f) * 0.7225f : 0f;
    }

    public Rect GlassOn(Rect window)
    {
        var gasket = GasketOn(window);
        return gasket <= 0f ? ScreenOn(window) : ScreenOn(window).Inset(gasket);
    }

    public float GlassRadiusOn(Rect window)
    {
        var gasket = GasketOn(window);
        if (gasket <= 0f)
        {
            return ScreenRadiusOn(window);
        }

        var shrink = Gasket > 0.0001f ? gasket : gasket * 0.55f;
        return MathF.Max(0f, ScreenRadiusOn(window) - shrink);
    }

    // Pocket windows shrink the rim with width, so the curve reads as a hairline.
    // Hold a minimum metal thickness at the corner until the handset is full-size again.
    private float TightenHole(Rect window, float radius)
    {
        var keep = CornerRimKeep(window);
        if (keep <= 0f)
        {
            return radius;
        }

        return MathF.Max(0f, MathF.Min(radius, CornerOn(window) - keep));
    }

    private float CornerRimKeep(Rect window)
    {
        const float pocket = 200f;
        if (window.Width >= pocket)
        {
            return 0f;
        }

        var painted = ScreenCorner > 0.0001f ? ScreenCorner : Corner * 0.7f;
        var natural = MathF.Max(0f, CornerOn(window) - window.Width * painted);
        var extra = 7.2f * (1f - window.Width / pocket);
        return MathF.Min(natural + extra, CornerOn(window) * 0.75f);
    }

    public Rect PowerOn(Rect window) => SideNub(window, PowerTop, PowerBottom);

    public Rect VolumeOn(Rect window) => SideNub(window, VolumeTop, VolumeBottom);

    private Rect SideNub(Rect window, float topFrac, float bottomFrac)
    {
        var jut = window.Width * MathF.Max(BodyRight, 0.02f);
        var top = window.Min.Y + window.Height * topFrac;
        var bottom = window.Min.Y + window.Height * bottomFrac;
        return new Rect(new Vector2(window.Max.X - jut, top), new Vector2(window.Max.X, bottom));
    }
}

public static class ChassisCatalog
{
    public const string Folder = "Chassis";

    public static ChassisPlate Phone { get; } = new("phone.png", 517f, 1008f,
        17f / 517f, 17f / 1008f, 23f / 517f, 16f / 1008f,
        206f / 1008f, 332f / 1008f, 393f / 1008f, 462f / 1008f,
        4f / 517f, 4f / 1008f, 9f / 517f, 4f / 1008f, 27f / 517f,
        backFileName: "phone-back.png");

    public static ChassisPlate PhoneEtched { get; } = new("phone-etched.png", 517f, 1008f,
        17f / 517f, 17f / 1008f, 23f / 517f, 16f / 1008f,
        206f / 1008f, 332f / 1008f, 393f / 1008f, 462f / 1008f,
        4f / 517f, 4f / 1008f, 9f / 517f, 4f / 1008f, 27f / 517f,
        backFileName: "phone-etched-back.png");

    public static ChassisPlate Tablet { get; } = new("tablet.png", 710f, 987f,
        18f / 710f, 17f / 987f, 23f / 710f, 16f / 987f,
        206f / 987f, 310f / 987f, 382f / 987f, 411f / 987f,
        5f / 710f, 4f / 987f, 9f / 710f, 4f / 987f, 33f / 710f,
        backFileName: "tablet-back.png");

    public static ChassisPlate TabletEtched { get; } = new("tablet-etched.png", 710f, 987f,
        18f / 710f, 17f / 987f, 23f / 710f, 16f / 987f,
        206f / 987f, 310f / 987f, 382f / 987f, 411f / 987f,
        5f / 710f, 4f / 987f, 9f / 710f, 4f / 987f, 33f / 710f,
        backFileName: "tablet-etched-back.png");

    public static ChassisPlate Android { get; } = new("android.png", 462f, 938f,
        5f / 462f, 5f / 938f, 12f / 462f, 5f / 938f,
        173f / 938f, 298f / 938f, 348f / 938f, 419f / 938f,
        0f, 0f, 7f / 462f, 0f, 26f / 462f, 21f / 462f, 4f / 462f);

    public static ChassisPlate For(HandsetForm form) => For(form, HandsetCase.Pearl);

    public static ChassisPlate For(HandsetForm form, HandsetCase casing) =>
        For(form, casing, HandsetFinish.Crystal);

    public static ChassisPlate For(HandsetForm form, HandsetCase casing, HandsetFinish finish)
    {
        if (casing == HandsetCase.Android && form != HandsetForm.Tablet)
        {
            return Android;
        }

        if (form == HandsetForm.Tablet)
        {
            return finish == HandsetFinish.Etched ? TabletEtched : Tablet;
        }

        return finish == HandsetFinish.Etched ? PhoneEtched : Phone;
    }
}
