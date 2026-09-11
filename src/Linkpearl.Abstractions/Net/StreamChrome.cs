using System.Diagnostics;
using System.Globalization;

namespace Linkpearl.Net;

public static class StreamChrome
{
    public static string LoginOf(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim();
        if (text.StartsWith("live:", StringComparison.OrdinalIgnoreCase))
        {
            text = text["live:".Length..];
        }

        if (text.StartsWith("twitch:", StringComparison.OrdinalIgnoreCase))
        {
            text = text["twitch:".Length..];
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
            uri.Host.Contains("twitch.tv", StringComparison.OrdinalIgnoreCase))
        {
            text = uri.AbsolutePath.Trim('/');
            var slash = text.IndexOf('/', StringComparison.Ordinal);
            if (slash >= 0)
            {
                text = text[..slash];
            }
        }

        return text.Trim().TrimStart('@');
    }

    public static string WatchUrl(string loginOrUrl)
    {
        var login = LoginOf(loginOrUrl);
        return login.Length == 0 ? string.Empty : "https://www.twitch.tv/" + login;
    }

    public static string StreamId(string login) =>
        login.Length == 0 ? string.Empty : "twitch:" + LoginOf(login);

    public static void OpenWatch(string url)
    {
        if (url.Length == 0)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // The OS can refuse a browse; the row still stays tappable.
        }
    }

    public static string PlaceLine(string? world, string? district, int? ward, int? plot)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(world))
        {
            parts.Add(world.Trim());
        }

        if (!string.IsNullOrWhiteSpace(district))
        {
            parts.Add(district.Trim());
        }

        if (ward is > 0)
        {
            parts.Add("W" + ward.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (plot is > 0)
        {
            parts.Add("P" + plot.Value.ToString(CultureInfo.InvariantCulture));
        }

        return string.Join(" · ", parts);
    }

    public static string LiCommand(string? world, string? district, int? ward, int? plot)
    {
        if (string.IsNullOrWhiteSpace(world) || string.IsNullOrWhiteSpace(district) || ward is not > 0 ||
            plot is not > 0)
        {
            return string.Empty;
        }

        return "/li " + world.Trim() + " " + district.Trim() + " " +
               ward.Value.ToString(CultureInfo.InvariantCulture) + " " +
               plot.Value.ToString(CultureInfo.InvariantCulture);
    }

    public static bool TryParseLi(string? text, out string world, out string district, out int ward, out int plot)
    {
        world = string.Empty;
        district = string.Empty;
        ward = 0;
        plot = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var raw = text.Trim();
        if (raw.StartsWith("/li", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw[3..].Trim();
        }

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 4)
        {
            return false;
        }

        if (!int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out plot) ||
            !int.TryParse(parts[^2], NumberStyles.Integer, CultureInfo.InvariantCulture, out ward) ||
            ward <= 0 || plot <= 0)
        {
            return false;
        }

        world = parts[0];
        district = string.Join(' ', parts[1..^2]);
        return world.Length > 0 && district.Length > 0;
    }
}
