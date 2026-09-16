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
    public string Name { get; }
    public string World { get; }

    public GamePeer(string name, string world)
    {
        Name = name;
        World = world;
    }
}

public readonly struct GameFriend
{
    public string Name { get; }
    public string World { get; }
    public string Place { get; }
    public bool Online { get; }

    public GameFriend(string name, string world, string place, bool online)
    {
        Name = name;
        World = world;
        Place = place;
        Online = online;
    }
}

public readonly struct GameChatLine
{
    public GameChannel Channel { get; }
    public int ChannelIndex { get; }
    public string Sender { get; }
    public string SenderWorld { get; }
    public string Body { get; }
    public DateTimeOffset Received { get; }
    public bool Mine { get; }

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

    IReadOnlyList<GameFriend> Friends { get; }

    bool ShouldOfferFriend(string characterName, string world);

    void RequestFriend(string characterName, string world);

    void Send(GameChannel channel, int channelIndex, string body);

    void SendTell(string characterName, string world, string body);

    void InviteToParty(string characterName, string world);

    void TargetPlayer(string characterName, string world);

    bool TrySendEmote(string body);

    void Print(string body);

    void OpenGameMenu(GameMenu menu);

    IReadOnlyList<string> LinkshellNames(bool crossWorld);
}
