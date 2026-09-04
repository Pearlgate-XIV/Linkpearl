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

    public static string ForGlass(DisplayPreferences display, string linked, bool allowCustom = false)
    {
        if (allowCustom)
        {
            var own = Sanitize(display.OwnName);
            if (own.Length > 0)
            {
                return own;
            }
        }

        return ApplyStyle(linked, display.NameStyle);
    }

    public static string ApplyStyle(string name, NameStyle style)
    {
        if (name.Length == 0 || style != NameStyle.Given)
        {
            return name;
        }

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
