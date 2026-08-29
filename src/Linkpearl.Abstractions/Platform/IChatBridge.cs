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

public readonly struct GamePeer
{
    public readonly string Name;
    public readonly string World;

    public GamePeer(string name, string world)
    {
        Name = name;
        World = world;
    }
}

public readonly struct GameChatLine
{
    public readonly GameChannel Channel;
    public readonly int ChannelIndex;
    public readonly string Sender;
    public readonly string SenderWorld;
    public readonly string Body;
    public readonly DateTimeOffset Received;
    public readonly bool Mine;

    public GameChatLine(GameChannel channel, int channelIndex, string sender, string senderWorld, string body,
        DateTimeOffset received, bool mine)
    {
        Channel = channel;
        ChannelIndex = channelIndex;
        Sender = sender;
        SenderWorld = senderWorld;
        Body = body;
        Received = received;
        Mine = mine;
    }
}

public interface IChatBridge
{
    event Action<GameChatLine>? LineReceived;

    bool CanSend { get; }

    bool InParty { get; }

    bool InAlliance { get; }

    IReadOnlyList<GamePeer> PartyMembers { get; }

    IReadOnlyList<GamePeer> NearbyPlayers { get; }

    void Send(GameChannel channel, int channelIndex, string body);

    void SendTell(string characterName, string world, string body);

    void Print(string body);

    IReadOnlyList<string> LinkshellNames(bool crossWorld);
}
