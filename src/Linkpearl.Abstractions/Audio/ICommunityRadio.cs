namespace Linkpearl.Audio;

public readonly record struct CommunityStation(
    string Id,
    string Name,
    string Host,
    string Genre,
    bool Live,
    string ListenUrl,
    int Listeners,
    string Bio = "",
    string ArtPath = "",
    string Mount = "",
    int Likes = 0,
    bool Liked = false);

public interface ICommunityRadio
{
    IReadOnlyList<CommunityStation> Directory { get; }

    IReadOnlyList<CommunityStation> Live { get; }

    IReadOnlyList<CommunityStation> Mine { get; }

    bool Broadcasting { get; }

    string Notice { get; }

    bool SignedIn { get; }

    string OwnedId { get; }

    string OwnedListenUrl { get; }

    string OwnedMount { get; }

    string OwnedIngestUrl { get; }

    void UseIcecast(string listenBase, string sourceUser, string sourcePassword);

    void Refresh();

    void EnsureStation(string name, string host, string genre, string bio, string artPath, string mount);

    void GoLive(string name, string genre);

    void EndLive();

    int StationLikes(string stationId);

    bool StationLiked(string stationId);

    void ToggleStationLike(string stationId);

    void ToggleStationLike(string stationId, int shownCount);
}
