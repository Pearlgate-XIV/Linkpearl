using System.Globalization;

namespace Linkpearl.Emoji;

public static class EmojiBits
{
    public static readonly string[] Tones = { "", "\U0001F3FB", "\U0001F3FC", "\U0001F3FD", "\U0001F3FE", "\U0001F3FF" };

    public static string WithTone(string glyph, string tone) =>
        tone.Length == 0 ? glyph : glyph + tone;

    public static string BaseOf(string glyph)
    {
        if (glyph.Length == 0)
        {
            return glyph;
        }

        var cut = glyph.IndexOf('\uD83C');
        if (cut > 0 && cut + 1 < glyph.Length)
        {
            var next = glyph[cut + 1];
            if (next is >= '\uDFFB' and <= '\uDFFF')
            {
                return glyph[..cut];
            }
        }

        return glyph;
    }

    public static int ClampIndex(string text, int index)
    {
        if (index <= 0)
        {
            return 0;
        }

        if (index >= text.Length)
        {
            return text.Length;
        }

        if (char.IsLowSurrogate(text[index]) && index > 0 && char.IsHighSurrogate(text[index - 1]))
        {
            return index - 1;
        }

        return index;
    }

    public const char WireMark = '\uFFFC';

    public static string Insert(string text, int index, string glyph)
    {
        var at = ClampIndex(text, index);
        return text[..at] + glyph + text[at..];
    }

    public static bool HasFace(string text)
    {
        var walk = StringInfo.GetTextElementEnumerator(text);
        while (walk.MoveNext())
        {
            if (LooksEmoji(walk.GetTextElement()))
            {
                return true;
            }
        }

        return false;
    }

    public static string ToWire(string text, List<string> faces)
    {
        faces.Clear();
        if (text.Length == 0)
        {
            return text;
        }

        var wire = new System.Text.StringBuilder(text.Length);
        var walk = StringInfo.GetTextElementEnumerator(text);
        while (walk.MoveNext())
        {
            var part = walk.GetTextElement();
            if (LooksEmoji(part))
            {
                faces.Add(part);
                wire.Append(WireMark);
                continue;
            }

            wire.Append(part);
        }

        return wire.ToString();
    }

    public static string FromWire(string wire, IReadOnlyList<string> faces)
    {
        if (faces.Count == 0)
        {
            return wire;
        }

        var text = new System.Text.StringBuilder(wire.Length + 8);
        var take = 0;
        foreach (var rune in wire.EnumerateRunes())
        {
            if (rune.Value == WireMark)
            {
                if (take < faces.Count)
                {
                    text.Append(faces[take]);
                    take++;
                }

                continue;
            }

            text.Append(rune);
        }

        return text.ToString();
    }

    public static int ClusterCount(string text)
    {
        var count = 0;
        var walk = StringInfo.GetTextElementEnumerator(text);
        while (walk.MoveNext())
        {
            var part = walk.GetTextElement();
            if (!string.IsNullOrWhiteSpace(part))
            {
                count++;
            }
        }

        return count;
    }

    public static bool OnlyEmoji(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        var walk = StringInfo.GetTextElementEnumerator(text);
        var any = false;
        while (walk.MoveNext())
        {
            var part = walk.GetTextElement();
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            if (!LooksEmoji(part))
            {
                return false;
            }

            any = true;
        }

        return any;
    }

    public static int OnlyCount(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : OnlyEmoji(text) ? ClusterCount(text) : 0;

    public static bool LooksEmoji(string part)
    {
        var emoji = false;
        foreach (var rune in part.EnumerateRunes())
        {
            var value = rune.Value;
            if (value is 0x200D or 0xFE0F || value is >= 0x1F3FB and <= 0x1F3FF)
            {
                continue;
            }

            if (value == 0x20E3)
            {
                emoji = true;
                continue;
            }

            if (value is >= 0x30 and <= 0x39 or 0x23 or 0x2A)
            {
                continue;
            }

            if (IsEmojiRune(value))
            {
                emoji = true;
                continue;
            }

            return false;
        }

        return emoji;
    }

    public static bool IsEmojiRune(int value) =>
        value is >= 0x1F300 and <= 0x1FAFF
            or >= 0x1F1E6 and <= 0x1F1FF
            or >= 0x2600 and <= 0x27BF
            or >= 0x2300 and <= 0x23FF
            or >= 0x2B00 and <= 0x2BFF
            or 0x3030 or 0x303D or 0x3297 or 0x3299
            or 0x00A9 or 0x00AE or 0x2122;
}
