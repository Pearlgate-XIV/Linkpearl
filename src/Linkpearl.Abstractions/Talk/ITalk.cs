namespace Linkpearl.Talk;

public enum TalkKind : byte
{
    Party = 0,
    Alliance = 1,
    Tell = 2,
    Linkshell = 3,
    CrossWorldLinkshell = 4,
    Pearl = 5,
    FreeCompany = 6,
    Novice = 7,
}

public readonly record struct TalkThread(
    string Id,
    TalkKind Kind,
    int ChannelIndex,
    string Title,
    string Subtitle,
    string Preview,
    DateTimeOffset LastAt,
    int Unread,
    bool Pinned,
    bool CanSend);

public readonly record struct TalkLine(
    string Sender,
    string Body,
    DateTimeOffset At,
    bool Mine);

public readonly record struct TalkPeer(
    string Id,
    string TellId,
    string Name,
    string World,
    string Note,
    DateTimeOffset LastAt,
    int LineCount,
    bool OnPearlgate,
    string Handle,
    string Number);

public interface ITalk
{
    int Generation { get; }

    int UnreadTotal { get; }

    IReadOnlyList<TalkThread> Inbox();

    IReadOnlyList<TalkLine> Lines(string threadId);

    TalkThread? Find(string threadId);

    IReadOnlyList<TalkPeer> Peers();

    TalkPeer? FindPeer(string peerId);

    IReadOnlyList<TalkThread> Urgent(int max);

    IReadOnlyList<GamePeerHint> SuggestTells();

    IReadOnlyList<GamePeerHint> SearchNearby(string query);

    void MarkRead(string threadId);

    void Send(string threadId, string body);

    string StartTell(string name, string world);

    void SetNote(string peerId, string note);
}

public readonly record struct GamePeerHint(string Name, string World, string Reason);