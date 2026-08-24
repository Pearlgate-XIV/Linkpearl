namespace Linkpearl.Platform;

public enum GameChannel : byte
{
    Unknown = 0,
    Say = 1,
    Yell = 2,
    Shout = 3,
    Tell = 4,
    Party = 5,
    Alliance = 6,
    FreeCompany = 7,
    Novice = 8,
    Linkshell = 9,
    CrossWorldLinkshell = 10,
    Echo = 11,
    System = 12,
    Emote = 13,
}

public readonly struct GameChatLine
{
    public readonly GameChannel Channel;
    public readonly int ChannelIndex;
    public readonly string Sender;
    public readonly string SenderWorld;
    public readonly string Body;
    public readonly DateTimeOffset Received;

    public GameChatLine(GameChannel channel, int channelIndex, string sender, string senderWorld, string body,
        DateTimeOffset received)
    {
        Channel = channel;
        ChannelIndex = channelIndex;
        Sender = sender;
        SenderWorld = senderWorld;
        Body = body;
        Received = received;
    }
}

public interface IChatBridge
{
    event Action<GameChatLine>? LineReceived;

    bool CanSend { get; }

    void Send(GameChannel channel, int channelIndex, string body);

    void SendTell(string characterName, string world, string body);

    void Print(string body);

    IReadOnlyList<string> LinkshellNames(bool crossWorld);
}
