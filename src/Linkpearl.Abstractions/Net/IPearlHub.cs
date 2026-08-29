namespace Linkpearl.Net;

public readonly record struct PearlChat(
    string Id,
    string Title,
    string Preview,
    int UnreadCount,
    long LastMessageAtUnix);

public readonly record struct PearlPerson(
    string Id,
    string DisplayName,
    string Handle,
    string PhoneNumber,
    bool IsMutual);

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

public readonly record struct PearlHit(string Kind, string Title, string Subtitle);

public sealed record PearlSnapshot
{
    public static PearlSnapshot Empty { get; } = new();

    public bool SignedIn { get; init; }

    public bool Busy { get; init; }

    public string Notice { get; init; } = string.Empty;

    public string ChallengeCode { get; init; } = string.Empty;

    public string MeName { get; init; } = string.Empty;

    public string MeWorld { get; init; } = string.Empty;

    public string MeHandle { get; init; } = string.Empty;

    public string MeBio { get; init; } = string.Empty;

    public string MyNumber { get; init; } = string.Empty;

    public int Followers { get; init; }

    public int Following { get; init; }

    public PearlChat[] Chats { get; init; } = [];

    public PearlPerson[] People { get; init; } = [];

    public PearlStory[] Stories { get; init; } = [];

    public PearlAnnouncement[] Announcements { get; init; } = [];

    public PearlHit[] SearchHits { get; init; } = [];

    public int UnreadTotal { get; init; }

    public int Generation { get; init; }
}

public interface IPearlHub
{
    PearlSnapshot Current { get; }

    void BeginSignIn();

    void SignOut();

    void NoteQuery(string query);
}
