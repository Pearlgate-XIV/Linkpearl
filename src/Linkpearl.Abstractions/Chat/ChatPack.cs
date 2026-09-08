namespace Linkpearl.Chat;

public readonly record struct ChatFace(string Id, string Glyph, string Label, string File = "");

public static class ChatPack
{
    public static readonly string[] Emotes =
    {
        "😀", "😃", "😄", "😁", "😆", "😅", "😂", "🤣", "😊", "😇",
        "🙂", "😉", "😍", "🥰", "😘", "😋", "😜", "🤪", "😎", "🤩",
        "😏", "😒", "🙄", "😔", "😢", "😭", "😤", "😡", "🥺", "😳",
        "🤔", "🫡", "😴", "🤤", "😷", "🤒", "😈", "💀", "👻", "👀",
        "🔥", "✨", "⭐", "💯", "💜", "❤️", "💔", "🌙", "☀️", "⚡",
        "🎉", "🎵", "📸", "💪", "🫶", "👍", "👎", "👏", "🙏", "✌️",
    };

    public static readonly ChatFace[] Stickers =
    {
        new("wave", "👋", "Wave"),
        new("heart", "💜", "Heart"),
        new("fire", "🔥", "Fire"),
        new("moon", "🌙", "Night"),
        new("spark", "✨", "Spark"),
        new("laugh", "😂", "Laugh"),
        new("wink", "😉", "Wink"),
        new("dance", "💃", "Dance"),
    };

    public static readonly ChatFace[] Gifs =
    {
        new("sunset", "🌇", "Sunset", "sunset.png"),
        new("club", "🎶", "Club", "club-a.png"),
        new("city", "🌃", "City", "city.png"),
        new("pose", "📸", "Pose", "pose.png"),
        new("fc", "🏠", "FC night", "fc.png"),
    };

    public static ChatFace Sticker(string id) => Find(Stickers, id, "💜", "Sticker");

    public static ChatFace Gif(string id) => Find(Gifs, id, "🎞", "GIF");

    private static ChatFace Find(ChatFace[] pack, string id, string glyph, string label)
    {
        for (var index = 0; index < pack.Length; index++)
        {
            if (string.Equals(pack[index].Id, id, StringComparison.Ordinal))
            {
                return pack[index];
            }
        }

        return new ChatFace(id, glyph, label);
    }
}
