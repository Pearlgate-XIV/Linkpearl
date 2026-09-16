namespace Linkpearl.Chat;

public readonly record struct ChatLink(int Start, int Length, string Href);

public static class ChatLinks
{
    public static void Find(string body, List<ChatLink> dest)
    {
        dest.Clear();
        if (string.IsNullOrEmpty(body))
        {
            return;
        }

        var index = 0;
        while (index < body.Length)
        {
            var start = Next(body, index);
            if (start < 0)
            {
                return;
            }

            var end = start;
            while (end < body.Length && !Stop(body[end]))
            {
                end++;
            }

            while (end > start && Trail(body[end - 1]))
            {
                end--;
            }

            var length = end - start;
            if (length < 4)
            {
                index = start + 1;
                continue;
            }

            if (!Safe(body.AsSpan(start, length).ToString(), out var href))
            {
                index = start + 1;
                continue;
            }

            dest.Add(new ChatLink(start, length, href));
            index = end;
        }
    }

    public static bool First(string body, out ChatLink link)
    {
        scratch.Clear();
        Find(body, scratch);
        if (scratch.Count == 0)
        {
            link = default;
            return false;
        }

        link = scratch[0];
        return true;
    }

    public static bool At(string body, int index, List<ChatLink> links, out ChatLink link)
    {
        for (var i = 0; i < links.Count; i++)
        {
            var hit = links[i];
            if (index >= hit.Start && index < hit.Start + hit.Length)
            {
                link = hit;
                return true;
            }
        }

        _ = body;
        link = default;
        return false;
    }

    public static bool Safe(string raw, out string href)
    {
        href = string.Empty;
        var text = (raw ?? string.Empty).Trim();
        if (text.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            uri.Host.Length == 0)
        {
            return false;
        }

        href = uri.AbsoluteUri;
        return true;
    }

    private static int Next(string body, int index)
    {
        var https = body.IndexOf("https://", index, StringComparison.OrdinalIgnoreCase);
        var http = body.IndexOf("http://", index, StringComparison.OrdinalIgnoreCase);
        var www = body.IndexOf("www.", index, StringComparison.OrdinalIgnoreCase);
        var start = Pick(https, http);
        start = Pick(start, Edge(body, www));
        return start;
    }

    private static int Edge(string body, int www)
    {
        if (www < 0)
        {
            return -1;
        }

        if (www > 0 && !Stop(body[www - 1]) && body[www - 1] != '(')
        {
            return Edge(body, body.IndexOf("www.", www + 1, StringComparison.OrdinalIgnoreCase));
        }

        return www;
    }

    private static int Pick(int left, int right)
    {
        if (left < 0)
        {
            return right;
        }

        if (right < 0)
        {
            return left;
        }

        return Math.Min(left, right);
    }

    private static bool Stop(char value) =>
        char.IsWhiteSpace(value) || value is '<' or '>' or '"' or '\'' or '`' or '|';

    private static bool Trail(char value) =>
        value is '.' or ',' or ';' or ':' or '!' or '?' or ')' or ']' or '}';

    private static readonly List<ChatLink> scratch = [];
}
