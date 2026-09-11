namespace Linkpearl.Net;

public enum StreamProviderKind : byte
{
    Twitch = 0,
}

public enum StreamStatus : byte
{
    Unknown = 0,
    Offline = 1,
    Live = 2,
}

public readonly record struct StreamProviderAccount(
    StreamProviderKind Provider,
    string ExternalUserId,
    string Username,
    string DisplayName,
    bool Verified,
    bool Connected);

public readonly record struct StreamInfo(
    string Id,
    StreamProviderKind Provider,
    StreamStatus Status,
    string ExternalUserId,
    string Username,
    string DisplayName,
    string Title,
    string WatchUrl,
    string ArtUrl,
    int Viewers,
    string Genre,
    string Bio,
    string VenueLine,
    string Lifestream,
    string Source);

public readonly record struct StreamScheduleMark(
    string Id,
    string Title,
    string DjName,
    string VenueName,
    string Place,
    string TwitchLogin,
    string WatchUrl,
    string Lifestream,
    DateTimeOffset Starts,
    DateTimeOffset Ends,
    string Kind);

public interface IStreamDesk : IDisposable
{
    IReadOnlyList<StreamInfo> Live { get; }

    IReadOnlyList<StreamScheduleMark> CommunitySchedule { get; }

    StreamProviderAccount Own { get; }

    string Notice { get; }

    bool Busy { get; }

    void Refresh();

    void RequestConnect();

    void Disconnect();

    StreamInfo? Find(string idOrLogin);
}
