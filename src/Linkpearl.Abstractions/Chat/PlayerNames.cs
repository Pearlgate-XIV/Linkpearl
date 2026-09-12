namespace Linkpearl.Chat;

public static class PlayerNames
{
    public static string Fold(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[name.Length];
        var count = 0;
        var space = false;
        for (var index = 0; index < name.Length; index++)
        {
            var glyph = name[index];
            if (glyph == '@')
            {
                break;
            }

            if (char.IsLetterOrDigit(glyph) || glyph is '-' or '\'')
            {
                buffer[count++] = char.ToLowerInvariant(glyph);
                space = false;
                continue;
            }

            if (glyph is ' ' or '.' && count > 0 && !space)
            {
                buffer[count++] = ' ';
                space = true;
            }
        }

        while (count > 0 && buffer[count - 1] == ' ')
        {
            count--;
        }

        return count == 0 ? string.Empty : new string(buffer[..count]);
    }

    public static string Clean(string name)
    {
        var folded = Fold(name);
        if (folded.Length == 0)
        {
            return string.Empty;
        }

        var chars = folded.ToCharArray();
        var cap = true;
        for (var index = 0; index < chars.Length; index++)
        {
            if (chars[index] == ' ')
            {
                cap = true;
                continue;
            }

            chars[index] = cap ? char.ToUpperInvariant(chars[index]) : chars[index];
            cap = false;
        }

        return new string(chars);
    }

    public static bool Same(string left, string right)
    {
        var a = Fold(left);
        var b = Fold(right);
        return a.Length > 0 && a.Equals(b, StringComparison.Ordinal);
    }
}
