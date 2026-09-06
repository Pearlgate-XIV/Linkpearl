namespace Linkpearl.Phone;

public static class LineNumbers
{
    public static string Compact(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var built = new char[raw.Length];
        var count = 0;
        for (var index = 0; index < raw.Length; index++)
        {
            var ch = raw[index];
            if (char.IsDigit(ch) || (ch == '+' && count == 0))
            {
                built[count++] = ch;
            }
        }

        return new string(built, 0, count);
    }

    public static string Canonical(string? raw, int country = 0)
    {
        var compact = Compact(raw);
        if (compact.Length == 0)
        {
            return string.Empty;
        }

        var digits = compact[0] == '+' ? compact[1..] : compact;
        if (compact[0] == '+' && digits.Length > 0 && digits[0] is >= '1' and <= '4')
        {
            country = digits[0] - '0';
            digits = digits[1..];
        }

        if (digits.Length > 7)
        {
            digits = digits[^7..];
        }

        if (digits.Length == 0)
        {
            return string.Empty;
        }

        return country is >= 1 and <= 4 && digits.Length == 7
            ? "+" + country + digits
            : digits;
    }

    public static string Show(string? raw)
    {
        var compact = Compact(raw);
        if (compact.Length == 0)
        {
            return string.Empty;
        }

        var country = 0;
        var digits = compact;
        if (compact[0] == '+' && compact.Length > 1)
        {
            country = compact[1] - '0';
            digits = compact[2..];
        }

        if (digits.Length > 7)
        {
            digits = digits[^7..];
        }

        var local = digits.Length == 7 ? digits[..3] + "-" + digits[3..] : digits;
        return country is >= 1 and <= 4 && digits.Length == 7 ? "+" + country + " " + local : local;
    }

    public static bool Same(string? left, string? right)
    {
        var a = Canonical(left);
        var b = Canonical(right);
        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return true;
        }

        var localA = Local(a);
        var localB = Local(b);
        if (localA.Length != 7 || !string.Equals(localA, localB, StringComparison.Ordinal))
        {
            return false;
        }

        var countryA = Country(a);
        var countryB = Country(b);
        return countryA == 0 || countryB == 0 || countryA == countryB;
    }

    private static int Country(string compact)
    {
        if (compact.Length >= 2 && compact[0] == '+' && compact[1] is >= '1' and <= '4')
        {
            return compact[1] - '0';
        }

        return 0;
    }

    private static string Local(string compact)
    {
        var digits = compact.Length > 0 && compact[0] == '+' ? compact[1..] : compact;
        if (digits.Length > 0 && Country(compact) > 0)
        {
            digits = digits[1..];
        }

        return digits.Length > 7 ? digits[^7..] : digits;
    }
}
