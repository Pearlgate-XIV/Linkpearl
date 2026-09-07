namespace Linkpearl.Emoji;

public static class EmojiHunt
{
    public static List<EmojiMark> Find(string query, IReadOnlyList<EmojiMark> shelf)
    {
        var hits = new List<EmojiMark>();
        var needle = query.Trim().TrimStart('#').ToLowerInvariant();
        if (needle.Length == 0)
        {
            return hits;
        }

        for (var index = 0; index < shelf.Count; index++)
        {
            var mark = shelf[index];
            if (mark.Kind != EmojiKind.Unicode)
            {
                continue;
            }

            if (mark.Name.Contains(needle, StringComparison.Ordinal) ||
                mark.Keys.Contains(needle, StringComparison.Ordinal) ||
                mark.Value.Contains(needle, StringComparison.Ordinal))
            {
                hits.Add(mark);
            }
        }

        return hits;
    }
}
