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
    public const string Crystal = "crystal";
    public const string Pearl = "pearl";
    public const string Ember = "ember";
    public const string Night = "night";

    public static readonly string[] All = { Crystal, Pearl, Ember, Night };

    public static string Sanitize(string id)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index], id, StringComparison.Ordinal))
            {
                return All[index];
            }
        }

        return Crystal;
    }

    public static string Label(string id) => Sanitize(id) switch
    {
        Pearl => "Pearl",
        Ember => "Ember",
        Night => "Night",
        _ => "Crystal",
    };
}

public static class CoreId
{
    public const string Gold = "gold";
    public const string Violet = "violet";
    public const string Rose = "rose";
    public const string Sage = "sage";

    public static readonly string[] All = { Gold, Violet, Rose, Sage };

    public static string Sanitize(string id)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index], id, StringComparison.Ordinal))
            {
                return All[index];
            }
        }

        return Gold;
    }

    public static string Label(string id) => Sanitize(id) switch
    {
        Violet => "Violet",
        Rose => "Rose",
        Sage => "Sage",
        _ => "Gold",
    };
}
