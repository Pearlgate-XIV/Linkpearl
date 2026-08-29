using Linkpearl.Diagnostics;
using Linkpearl.Platform;
using Linkpearl.Time;

namespace Linkpearl.Net;

public sealed class PearlHub : IPearlHub, IDisposable
{
    private const float RefreshSeconds = 15f;

    private readonly GateClient client;
    private readonly IGameSession game;
    private readonly IFrameLoop frames;
    private readonly ILinkpearlLog log;
    private readonly Action<string?> persistToken;
    private readonly object gate = new();
    private PearlSnapshot snapshot = PearlSnapshot.Empty;
    private readonly CancellationTokenSource lifetime = new();
    private int generation;
    private float sinceRefresh = RefreshSeconds;
    private bool refreshQueued;
    private bool signInQueued;
    private bool signOutQueued;
    private string pendingQuery = string.Empty;
    private string activeQuery = string.Empty;
    private bool searchQueued;

    public PearlHub(string baseUrl, string? savedToken, IGameSession game, IFrameLoop frames, ILinkpearlLog log,
        Action<string?> persistToken)
    {
        client = new GateClient(string.IsNullOrWhiteSpace(baseUrl) ? GateClient.DefaultBaseUrl : baseUrl);
        this.game = game;
        this.frames = frames;
        this.log = log;
        this.persistToken = persistToken;
        if (!string.IsNullOrWhiteSpace(savedToken))
        {
            client.SetBearer(savedToken);
            Replace(new PearlSnapshot { SignedIn = true, Busy = true, Notice = "Connecting to Pearlgate..." });
            QueueRefresh();
        }

        frames.Tick += OnTick;
    }

    public PearlSnapshot Current
    {
        get
        {
            lock (gate)
            {
                return snapshot;
            }
        }
    }

    public void BeginSignIn() => signInQueued = true;

    public void SignOut() => signOutQueued = true;

    public void NoteQuery(string query)
    {
        var trimmed = query.Trim();
        if (string.Equals(pendingQuery, trimmed, StringComparison.Ordinal))
        {
            return;
        }

        pendingQuery = trimmed;
        if (trimmed.Length >= 2)
        {
            searchQueued = true;
        }
    }

    public void Dispose()
    {
        frames.Tick -= OnTick;
        lifetime.Cancel();
        lifetime.Dispose();
        client.Dispose();
    }

    private void OnTick(float deltaSeconds)
    {
        sinceRefresh += deltaSeconds;
        if (signInQueued)
        {
            signInQueued = false;
            Start(RunSignInAsync);
        }

        if (signOutQueued)
        {
            signOutQueued = false;
            Start(RunSignOutAsync);
        }

        if (searchQueued)
        {
            searchQueued = false;
            Start(RunSearchAsync);
        }

        if (refreshQueued || (Current.SignedIn && sinceRefresh >= RefreshSeconds))
        {
            refreshQueued = false;
            sinceRefresh = 0f;
            Start(RunRefreshAsync);
        }
    }

    private void QueueRefresh() => refreshQueued = true;

    private void Start(Func<CancellationToken, Task> work)
    {
        var token = lifetime.Token;
        _ = Task.Run(() => work(token), token);
    }

    private async Task RunSignInAsync(CancellationToken token)
    {
        var character = game.Character;
        if (!character.IsKnown || character.Name.Length == 0 || character.WorldName.Length == 0)
        {
            Replace(Current with { Notice = "Log into a character first, then try again.", Busy = false });
            return;
        }

        Replace(Current with { Busy = true, Notice = "Signing in..." });
        try
        {
            var challengeBody = GateClient.JsonBody(new ChallengeRequestDto(character.Name, character.WorldName),
                GateJson.Default.ChallengeRequestDto);
            var (challenge, challengeStatus) =
                await client.PostAsync("/auth/challenge", challengeBody, GateJson.Default.ChallengeReplyDto, token)
                    .ConfigureAwait(false);
            if (challenge is null || challengeStatus is < 200 or >= 300)
            {
                Replace(Current with
                {
                    Busy = false, Notice = "Could not reach Pearlgate. Check the network and try again.",
                });
                return;
            }

            var verifyBody = GateClient.JsonBody(new VerifyRequestDto(challenge.ChallengeId),
                GateJson.Default.VerifyRequestDto);
            var (verify, verifyStatus) =
                await client.PostAsync("/auth/verify", verifyBody, GateJson.Default.VerifyReplyDto, token)
                    .ConfigureAwait(false);
            if (verify is { Ok: true, Token: { Length: > 0 } })
            {
                AcceptSession(verify.Token, verify.User, "Signed in.");
                QueueRefresh();
                return;
            }

            var reason = verify?.Reason ?? $"http {verifyStatus}";
            if (string.Equals(reason, "pending", StringComparison.OrdinalIgnoreCase))
            {
                Replace(Current with
                {
                    Busy = false,
                    ChallengeCode = challenge.Code,
                    Notice = challenge.Instructions,
                });
                return;
            }

            Replace(Current with { Busy = false, ChallengeCode = challenge.Code, Notice = SignInFailure(reason) });
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate sign-in failed");
            Replace(Current with { Busy = false, Notice = "Sign-in failed. Try again in a moment." });
        }
    }

    private async Task RunSignOutAsync(CancellationToken token)
    {
        try
        {
            await client.DeleteAsync("/auth/token", token).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate sign-out revoke failed");
        }

        client.SetBearer(string.Empty);
        persistToken(null);
        Replace(new PearlSnapshot { Notice = "Signed out.", Generation = NextGeneration() });
    }

    private async Task RunRefreshAsync(CancellationToken token)
    {
        if (!Current.SignedIn)
        {
            return;
        }

        try
        {
            var (me, meStatus) = await client.GetAsync("/me", GateJson.Default.GateUserDto, token)
                .ConfigureAwait(false);
            if (meStatus == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            if (me is null)
            {
                Replace(Current with { Busy = false, Notice = "Pearlgate is up, but /me did not return a profile." });
                return;
            }

            var (chatsPage, _) = await client.GetAsync("/chats/", GateJson.Default.ConversationPageDto, token)
                .ConfigureAwait(false);
            var (contacts, _) = await client.GetAsync("/contacts/", GateJson.Default.ContactListDto, token)
                .ConfigureAwait(false);
            var (stories, _) = await client.GetAsync("/stories", GateJson.Default.StoryTrayDto, token)
                .ConfigureAwait(false);
            var (announcements, _) = await client.GetAsync("/announcements", GateJson.Default.AnnouncementPageDto, token)
                .ConfigureAwait(false);

            var chats = MapChats(chatsPage?.Items);
            var people = MapPeople(contacts?.Contacts);
            Replace(new PearlSnapshot
            {
                SignedIn = true,
                Busy = false,
                Notice = string.Empty,
                MeName = Display(me.DisplayName, me.Name),
                MeWorld = me.World ?? string.Empty,
                MeHandle = me.Handle ?? string.Empty,
                MeBio = me.Bio ?? string.Empty,
                MyNumber = contacts?.MyNumber ?? string.Empty,
                Followers = me.Followers,
                Following = me.Following,
                Chats = chats,
                People = people,
                Stories = MapStories(stories?.Rings),
                Announcements = MapAnnouncements(announcements?.Items),
                SearchHits = Current.SearchHits,
                UnreadTotal = CountUnread(chats),
                Generation = NextGeneration(),
            });
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate refresh failed");
            Replace(Current with { Busy = false, Notice = "Lost Pearlgate for a moment. Retrying." });
        }
    }

    private async Task RunSearchAsync(CancellationToken token)
    {
        var query = pendingQuery;
        if (query.Length < 2 || !Current.SignedIn)
        {
            return;
        }

        activeQuery = query;
        try
        {
            var path = "/users/search?q=" + Uri.EscapeDataString(query);
            var (page, status) = await client.GetAsync(path, GateJson.Default.UserSearchDto, token)
                .ConfigureAwait(false);
            if (status == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            if (!string.Equals(activeQuery, pendingQuery, StringComparison.Ordinal))
            {
                return;
            }

            Replace(Current with { SearchHits = MapHits(page?.Users), Generation = NextGeneration() });
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate people search failed");
        }
    }

    private void AcceptSession(string token, GateUserDto? user, string notice)
    {
        client.SetBearer(token);
        persistToken(token);
        Replace(new PearlSnapshot
        {
            SignedIn = true,
            Busy = true,
            Notice = notice,
            MeName = user is null ? string.Empty : Display(user.DisplayName, user.Name),
            MeWorld = user?.World ?? string.Empty,
            MeHandle = user?.Handle ?? string.Empty,
            MeBio = user?.Bio ?? string.Empty,
            Followers = user?.Followers ?? 0,
            Following = user?.Following ?? 0,
            Generation = NextGeneration(),
        });
    }

    private void DropSession(string notice)
    {
        client.SetBearer(string.Empty);
        persistToken(null);
        Replace(new PearlSnapshot { Notice = notice, Generation = NextGeneration() });
    }

    private void Replace(PearlSnapshot next)
    {
        lock (gate)
        {
            snapshot = next.Generation == 0 ? next with { Generation = NextGeneration() } : next;
        }
    }

    private int NextGeneration() => Interlocked.Increment(ref generation);

    private static PearlChat[] MapChats(ConversationDto[]? items)
    {
        if (items is null || items.Length == 0)
        {
            return [];
        }

        var mapped = new PearlChat[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var title = item.IsGroup
                ? (string.IsNullOrWhiteSpace(item.Title) ? "Group" : item.Title)
                : (string.IsNullOrWhiteSpace(item.OtherDisplayName) ? item.Title : item.OtherDisplayName);
            mapped[index] = new PearlChat(item.Id, string.IsNullOrWhiteSpace(title) ? "Chat" : title,
                item.LastMessagePreview ?? string.Empty, item.UnreadCount, item.LastMessageAtUnix);
        }

        return mapped;
    }

    private static PearlPerson[] MapPeople(ContactDto[]? items)
    {
        if (items is null || items.Length == 0)
        {
            return [];
        }

        var mapped = new PearlPerson[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var name = string.IsNullOrWhiteSpace(item.Alias) ? item.DisplayName : item.Alias;
            mapped[index] = new PearlPerson(item.UserId, name ?? string.Empty, item.Handle ?? string.Empty,
                item.PhoneNumber ?? string.Empty, item.IsMutual);
        }

        return mapped;
    }

    private static PearlStory[] MapStories(StoryRingDto[]? items)
    {
        if (items is null || items.Length == 0)
        {
            return [];
        }

        var mapped = new PearlStory[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            mapped[index] = new PearlStory(item.AuthorId ?? string.Empty, item.AuthorDisplayName ?? "Someone",
                item.Count, item.HasUnseen);
        }

        return mapped;
    }

    private static PearlAnnouncement[] MapAnnouncements(AnnouncementDto[]? items)
    {
        if (items is null || items.Length == 0)
        {
            return [];
        }

        var mapped = new List<PearlAnnouncement>(items.Length);
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Title))
            {
                continue;
            }

            mapped.Add(new PearlAnnouncement(item.Id, item.Title, item.Body ?? string.Empty, item.CreatedAtUnix));
        }

        return mapped.ToArray();
    }

    private static PearlHit[] MapHits(GateUserDto[]? users)
    {
        if (users is null || users.Length == 0)
        {
            return [];
        }

        var mapped = new PearlHit[users.Length];
        for (var index = 0; index < users.Length; index++)
        {
            var user = users[index];
            mapped[index] = new PearlHit("Player", Display(user.DisplayName, user.Name),
                string.IsNullOrWhiteSpace(user.World) ? user.Handle ?? string.Empty : user.World);
        }

        return mapped;
    }

    private static int CountUnread(PearlChat[] chats)
    {
        var total = 0;
        for (var index = 0; index < chats.Length; index++)
        {
            total += chats[index].UnreadCount;
        }

        return total;
    }

    private static string Display(string? displayName, string? name) =>
        string.IsNullOrWhiteSpace(displayName) ? name ?? string.Empty : displayName;

    private static string SignInFailure(string reason) => reason switch
    {
        "banned" => "This character is suspended on Pearlgate.",
        "code_not_found" => "That sign-in code expired. Try again.",
        "xivauth_unconfigured" => "XIVAuth is not configured on the server yet.",
        _ => "Pearlgate refused sign-in (" + reason + ").",
    };
}
