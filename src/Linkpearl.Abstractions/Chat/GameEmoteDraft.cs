namespace Linkpearl.Chat;

/// <summary>
/// Turns a Feed composer line into a game emote command when the player typed
/// <c>/dance</c>, <c>/emote dance</c>, or a phone shortcode such as <c>:wave:</c>.
/// Unknown slash commands stay chat text.
/// </summary>
public static class GameEmoteDraft
{
    public static bool TryCommand(string body, IReadOnlyDictionary<string, string> commands, out string command)
    {
        command = string.Empty;
        var trimmed = (body ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (trimmed.Length == 0 || commands.Count == 0)
        {
            return false;
        }

        if (TryShortcode(trimmed, out var token))
        {
            return Lookup(token, commands, out command);
        }

        if (TrySticker(trimmed, out token))
        {
            return Lookup(token, commands, out command);
        }

        var slash = trimmed[0] == '/';
        if (!slash)
        {
            return false;
        }

        var rest = trimmed[1..].Trim();
        if (rest.Length == 0)
        {
            return false;
        }

        var space = rest.IndexOf(' ');
        var head = space < 0 ? rest : rest[..space];
        var tail = space < 0 ? string.Empty : rest[(space + 1)..].Trim();
        if (head.Equals("emote", StringComparison.OrdinalIgnoreCase))
        {
            if (tail.Length == 0)
            {
                command = "/emote";
                return true;
            }

            if (tail.Contains(' ') || !LooksToken(tail))
            {
                return false;
            }

            var name = tail.TrimStart('/');
            command = Lookup(name, commands, out var canon) ? canon : "/emote " + name.ToLowerInvariant();
            return true;
        }

        return tail.Length == 0 && Lookup(head, commands, out command);
    }

    private static bool TryShortcode(string body, out string token)
    {
        token = string.Empty;
        if (body.Length < 3 || body[0] != ':' || body[^1] != ':')
        {
            return false;
        }

        var inner = body[1..^1];
        if (!LooksToken(inner))
        {
            return false;
        }

        token = inner;
        return true;
    }

    private static bool TrySticker(string body, out string token)
    {
        token = string.Empty;
        var pack = ChatPack.Stickers;
        for (var index = 0; index < pack.Length; index++)
        {
            if (pack[index].Glyph.Equals(body, StringComparison.Ordinal))
            {
                token = pack[index].Id;
                return true;
            }
        }

        return false;
    }

    private static bool LooksToken(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var c = value[index];
            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool Lookup(string token, IReadOnlyDictionary<string, string> commands, out string command)
    {
        command = string.Empty;
        if (token.Length == 0)
        {
            return false;
        }

        return commands.TryGetValue(token.TrimStart('/').ToLowerInvariant(), out command!);
    }
}
