namespace Linkpearl.Emoji;

public readonly record struct EmojiMark(
    EmojiKind Kind,
    string Value,
    string Name,
    string Keys,
    EmojiGroup Group,
    bool Tones);
