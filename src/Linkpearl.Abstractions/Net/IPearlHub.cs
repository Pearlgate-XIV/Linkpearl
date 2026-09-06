namespace Linkpearl.Net;

public readonly record struct PearlChat(
    string Id,
    string Title,
    string Preview,
    int UnreadCount,
    long LastMessageAtUnix,
    bool IsGroup,
    string OtherUserId);

public readonly record struct PearlPerson(
    string Id,
    string DisplayName,
    string Handle,
    string PhoneNumber,
    bool IsMutual,
    string AvatarUrl,
    string Race = "",
    string World = "");

public readonly record struct PearlStory(
    string AuthorId,
    string AuthorName,
    int Count,
    bool HasUnseen);

public readonly record struct PearlAnnouncement(
    string Id,
    string Title,
    string Body,
    long CreatedAtUnix);

public readonly record struct PearlHit(string Kind, string Title, string Subtitle, string Id);

public readonly record struct PearlChatLine(bool Mine, string Body, string When, string Author);

public readonly record struct PearlMedia(string Id, string Url, int Width, int Height);

public readonly record struct PearlPost(
    string Id,
    string AuthorId,
    string AuthorName,
    string AuthorHandle,
    string AuthorAvatarUrl,
    string Body,
    string When,
    bool Mine,
    bool Liked,
    int Likes,
    int Comments,
    int Reposts,
    bool Reposted,
    string QuoteOf,
    string QuoteAuthor,
    string QuoteBody,
    PearlMedia[] Media);

public readonly record struct PearlComment(string Author, string Body, string When, bool Mine);

public readonly record struct PearlNote(
    string Id,
    string Kind,
    string ActorId,
    string ActorName,
    string Line,
    string PostId,
    string When);

public readonly record struct PearlRetainer(
    int Slot,
    string Name,
    long Gil,
    int ItemsOnSale,
    long VentureUntilUnix);

public readonly record struct PearlMarketWatch(
    int ItemId,
    string Label,
    string World,
    long NqGil,
    long HqGil,
    int Listed);

public sealed record PearlSnapshot
{
    public static PearlSnapshot Empty { get; } = new();

    public bool SignedIn { get; init; }

    public bool Busy { get; init; }

    public string Notice { get; init; } = string.Empty;

    public string ChallengeCode { get; init; } = string.Empty;

    public string MeId { get; init; } = string.Empty;

    public string MeName { get; init; } = string.Empty;

    public string MeWorld { get; init; } = string.Empty;

    public string MeHandle { get; init; } = string.Empty;

    public string MeBio { get; init; } = string.Empty;

    public string MeAvatarUrl { get; init; } = string.Empty;

    public string MyNumber { get; init; } = string.Empty;

    public int Followers { get; init; }

    public int Following { get; init; }

    public int FounderSeat { get; init; }

    public bool IsPatron { get; init; }

    public bool PatronLinked { get; init; }

    public string PatronLinkUrl { get; init; } = string.Empty;

    public PearlChat[] Chats { get; init; } = [];

    public PearlPerson[] People { get; init; } = [];

    public PearlStory[] Stories { get; init; } = [];

    public PearlAnnouncement[] Announcements { get; init; } = [];

    public PearlHit[] SearchHits { get; init; } = [];

    public PearlPost[] SearchPosts { get; init; } = [];

    public PearlPost[] Feed { get; init; } = [];

    public string FeedTab { get; init; } = "foryou";

    public bool FeedLive { get; init; }

    public PearlNote[] Notes { get; init; } = [];

    public bool NotesLive { get; init; }

    public PearlPost[] ProfilePosts { get; init; } = [];

    public PearlRetainer[] Retainers { get; init; } = [];

    public PearlMarketWatch[] MarketWatches { get; init; } = [];

    public string WatchedUserId { get; init; } = string.Empty;

    public string WatchedPostId { get; init; } = string.Empty;

    public int UnreadTotal { get; init; }

    public int Generation { get; init; }
}

public interface IPearlHub
{
    PearlSnapshot Current { get; }

    void BeginSignIn();

    void SignOut();

    void BeginPatronLink();

    void OpenPatronLink();

    void NoteQuery(string query);

    void WatchChat(string chatId);

    void SendChat(string chatId, string body);

    IReadOnlyList<PearlChatLine> LinesFor(string chatId);

    void WatchFeed(string tab);

    void WatchPost(string postId);

    void WatchProfile(string userId);

    void PublishPost(string body, bool everyone, IReadOnlyList<string> mediaPaths, string quoteOf);

    void LikePost(string postId, bool liked);

    void CommentOn(string postId, string body);

    void Repost(string postId);

    void Follow(string userId, bool follow);

    void AddFriend(string userId, string number);

    void RemoveFriend(string userId);

    void SetAvatar(string mediaPath);

    void PublishStory(string body, string mediaPath);

    IReadOnlyList<PearlComment> CommentsFor(string postId);

    PearlPost? PostById(string postId);

    void PrefetchMedia(string url);

    string? LocalMedia(string url);

    void WatchMarket(uint itemId, string label);

    void UnwatchMarket(uint itemId);
}
