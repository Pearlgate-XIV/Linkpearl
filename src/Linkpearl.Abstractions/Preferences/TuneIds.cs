namespace Linkpearl.Preferences;

public enum ClockFace : byte
{
    Local = 0,
    Eorzea = 1,
    Both = 2,
}

public enum ShadeLevel : byte
{
    Light = 0,
    Even = 1,
    Deep = 2,
}

public enum LetteringSize : byte
{
    Small = 0,
    Medium = 1,
    Large = 2,
}

public enum FightPresence : byte
{
    Stay = 0,
    Pocket = 1,
    Vanish = 2,
}

public enum TuneLayout : byte
{
    Touch = 0,
    List = 1,
}

public enum NameStyle : byte
{
    Full = 0,
    Given = 1,
    Family = 2,
}

public enum NameGlowWeight : byte
{
    Soft = 0,
    Medium = 1,
    Strong = 2,
}

public enum TitleMotion : byte
{
    Static = 0,
    Pulse = 1,
    Wave = 2,
}

public static class FounderFaces
{
    public const int SeatLimit = 500;
    public const string Inter = "inter";
    public const string Dreams = "dreams";

    public static readonly string[] All = { Inter, Dreams };

    public static bool Unlocked(bool signedIn, int founderSeat, bool granted, bool isDevelopment) =>
        isDevelopment || granted || (signedIn && founderSeat > 0 && founderSeat <= SeatLimit);

    public static string Sanitize(string id)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index], id, StringComparison.Ordinal))
            {
                return All[index];
            }
        }

        return Inter;
    }

    public static string Active(string id, bool unlocked) => unlocked ? Sanitize(id) : Inter;

    public static string Label(string id) => Sanitize(id) switch
    {
        Dreams => "Dreams",
        _ => "Inter",
    };

    public static string RelativeFile(string id) => Sanitize(id) switch
    {
        Dreams => Path.Combine("Founders", "StrangeDreams.ttf"),
        _ => "Inter-Bold.ttf",
    };
}

public static class ColorwayId
{
    public const string Night = "night";

    public static readonly string[] All = { Night };

    public static string Sanitize(string id) => Night;

    public static string Label(string id) => "Default";
}

public static class CoreId
{
    public const string White = "white";
    public const string Black = "black";
    public const string Red = "red";
    public const string Blue = "blue";
    public const string Green = "green";
    public const string Pink = "pink";
    public const string Orange = "orange";
    public const string Purple = "purple";
    public const string Yellow = "yellow";

    public static readonly string[] All =
    {
        White, Black, Red, Blue, Green, Pink, Orange, Purple, Yellow,
    };

    public static string Sanitize(string id)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index], id, StringComparison.Ordinal))
            {
                return All[index];
            }
        }

        return Blue;
    }

    public static string Label(string id) => Sanitize(id) switch
    {
        White => "White",
        Black => "Black",
        Red => "Red",
        Green => "Green",
        Pink => "Pink",
        Orange => "Orange",
        Purple => "Purple",
        Yellow => "Yellow",
        _ => "Blue",
    };

    public static Vector4 Swatch(string id) => Sanitize(id) switch
    {
        White => new Vector4(0.96f, 0.96f, 0.98f, 1f),
        Black => new Vector4(0.10f, 0.10f, 0.12f, 1f),
        Red => new Vector4(0.92f, 0.28f, 0.32f, 1f),
        Green => new Vector4(0.28f, 0.78f, 0.48f, 1f),
        Pink => new Vector4(0.96f, 0.42f, 0.68f, 1f),
        Orange => new Vector4(0.98f, 0.55f, 0.22f, 1f),
        Purple => new Vector4(0.68f, 0.42f, 0.92f, 1f),
        Yellow => new Vector4(0.96f, 0.84f, 0.28f, 1f),
        _ => new Vector4(0.32f, 0.55f, 0.96f, 1f),
    };
}
