using System.Text;
using Linkpearl.Geometry;
using Linkpearl.Net;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Vybe;

internal static class VybePostTags
{
    private static readonly char[] TagSeparators = { ' ', ',', ';' };

    public static readonly string[] Discover =
    {
        "glamour", "photography", "venue", "friends", "miqote", "event", "fashion", "roleplay",
        "gpose", "screenshot", "housing", "fc", "music", "dance", "concert", "lfg", "art", "oc",
        "club", "hangout",
    };

    public static readonly string[] Plus =
    {
        "AdultsOnly", "Spicy", "Lewd", "Suggestive", "Sensual", "Seductive",
        "Nude", "ArtisticNude", "Boudoir", "Lingerie", "Explicit", "Erotic",
        "Kink", "Fetish", "BDSM", "Bondage", "Dom", "Sub",
        "Roleplay", "ERP", "Couples", "Intimate", "NSFWGpose", "VYBEPlus",
    };

    public static string[] Lane(bool plus) => plus ? Plus : Discover;

    public static string[] Collect(PearlPost post)
    {
        var tags = new List<string>();
        if (post.Hashtags is { Length: > 0 })
        {
            for (var index = 0; index < post.Hashtags.Length; index++)
            {
                TryAdd(tags, post.Hashtags[index]);
            }
        }

        AbsorbBody(tags, post.Body);
        return tags.ToArray();
    }

    public static void AbsorbBody(List<string> tags, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        var from = 0;
        while (from < body.Length)
        {
            var hash = body.IndexOf('#', from);
            if (hash < 0)
            {
                return;
            }

            var end = hash + 1;
            while (end < body.Length && char.IsLetterOrDigit(body[end]))
            {
                end++;
            }

            if (end > hash + 1)
            {
                TryAdd(tags, body[hash..end]);
            }

            from = Math.Max(end, hash + 1);
        }
    }

    public static string Caption(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var built = new StringBuilder(body.Length);
        var from = 0;
        while (from < body.Length)
        {
            var hash = body.IndexOf('#', from);
            if (hash < 0)
            {
                built.Append(body.AsSpan(from));
                break;
            }

            built.Append(body.AsSpan(from, hash - from));
            var end = hash + 1;
            while (end < body.Length && char.IsLetterOrDigit(body[end]))
            {
                end++;
            }

            if (end <= hash + 1)
            {
                built.Append('#');
                from = hash + 1;
                continue;
            }

            from = end;
            while (from < body.Length && char.IsWhiteSpace(body[from]))
            {
                from++;
            }
        }

        return built.ToString().Trim();
    }

    public static string Format(string tag)
    {
        var shown = tag.Trim().TrimStart('#');
        return shown.Length == 0 ? string.Empty : "#" + shown;
    }

    public static string Show(string tag)
    {
        var slug = Normalize(tag);
        if (slug.Length == 0)
        {
            return string.Empty;
        }

        for (var index = 0; index < Plus.Length; index++)
        {
            if (string.Equals(Normalize(Plus[index]), slug, StringComparison.Ordinal))
            {
                return Format(Plus[index]);
            }
        }

        for (var index = 0; index < Discover.Length; index++)
        {
            if (string.Equals(Normalize(Discover[index]), slug, StringComparison.Ordinal))
            {
                return Format(Discover[index]);
            }
        }

        return Format(slug);
    }

    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = raw.Trim();
        var built = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var glyph = text[index];
            if (glyph is '#' or ' ')
            {
                continue;
            }

            if (char.IsLetterOrDigit(glyph))
            {
                built.Append(char.ToLowerInvariant(glyph));
            }
        }

        var slug = built.ToString();
        return slug.Length > 24 ? slug[..24] : slug;
    }

    public static bool HasAny(IReadOnlyList<string> tags) => tags.Count > 0;

    public static bool TryAdd(List<string> tags, string? raw)
    {
        var slug = Normalize(raw);
        if (slug.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < tags.Count; index++)
        {
            if (string.Equals(tags[index], slug, StringComparison.Ordinal))
            {
                return false;
            }
        }

        tags.Add(slug);
        return true;
    }

    public static void AbsorbTyped(List<string> tags, string raw)
    {
        var parts = raw.Split(TagSeparators, StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            TryAdd(tags, parts[index]);
        }
    }

    public static string Join(IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
        {
            return string.Empty;
        }

        var line = new StringBuilder();
        for (var index = 0; index < tags.Count; index++)
        {
            var mark = Show(tags[index]);
            if (mark.Length == 0)
            {
                continue;
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(mark);
        }

        return line.ToString();
    }

    public static float WrapHeight(in AppletFrame frame, Rect area, string[] options)
    {
        var gap = frame.Units(6f);
        var rowH = frame.Units(26f);
        var pad = frame.Units(10f);
        var x = 0f;
        var rows = 1;
        for (var index = 0; index < options.Length; index++)
        {
            var label = Format(options[index]);
            var wide = MathF.Min(area.Width,
                frame.Text.Measure(label, FontRole.CaptionStrong).X + pad * 2f);
            if (x > 0f && x + wide > area.Width)
            {
                rows++;
                x = 0f;
            }

            x += wide + gap;
        }

        return rowH * rows + gap * Math.Max(0, rows - 1);
    }
}
