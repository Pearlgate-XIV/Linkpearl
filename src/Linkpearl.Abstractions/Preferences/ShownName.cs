namespace Linkpearl.Preferences;

public static class ShownName
{
    public const int OwnNameLimit = 48;

    public const int OwnTitleLimit = 32;

    public static string Linked(string inGameName, string pearlName)
    {
        if (inGameName.Length > 0)
        {
            return inGameName;
        }

        return pearlName.Length > 0 ? pearlName : string.Empty;
    }

    public static string Source(DisplayPreferences display, string linked, bool allowCustom = true)
    {
        if (allowCustom)
        {
            var own = Sanitize(display.OwnName);
            if (own.Length > 0)
            {
                return own;
            }
        }

        return linked.Trim();
    }

    public static string ForGlass(DisplayPreferences display, string linked, bool allowCustom = false) =>
        ApplyStyle(Source(display, linked, allowCustom), display.NameStyle);

    public static string Preferred(DisplayPreferences display, string linked, string stored, string fallback)
    {
        var handset = Source(display, linked);
        var local = Sanitize(stored);
        var raw = local.Length > 0 ? local : handset;
        if (display.NameStyle == NameStyle.Full &&
            local.IndexOf(' ') < 0 &&
            handset.IndexOf(' ') >= 0 &&
            (local.Length == 0 || handset.StartsWith(local, StringComparison.Ordinal)))
        {
            raw = handset;
        }

        return raw.Length > 0 ? ApplyStyle(raw, display.NameStyle) : fallback;
    }

    public static string ApplyStyle(string name, NameStyle style)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return name;
        }

        return style == NameStyle.Given ? FirstToken(trimmed) : trimmed;
    }

    private static string FirstToken(string name)
    {
        var space = name.IndexOf(' ');
        return space < 0 ? name : name[..space];
    }

    public static string ClampLive(string value) => ClampLive(value, OwnNameLimit);

    public static string ClampTitle(string value) => ClampLive(value, OwnTitleLimit);

    public static string ClampLive(string value, int runeLimit)
    {
        var next = (value ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ');
        var taken = 0;
        foreach (var rune in next.EnumerateRunes())
        {
            taken++;
            if (taken > runeLimit)
            {
                var keep = 0;
                var end = 0;
                foreach (var kept in next.EnumerateRunes())
                {
                    if (keep == runeLimit)
                    {
                        break;
                    }

                    end += kept.Utf16SequenceLength;
                    keep++;
                }

                return next[..end];
            }
        }

        return next;
    }

    public static string Sanitize(string value) => ClampLive(value).Trim();
}
