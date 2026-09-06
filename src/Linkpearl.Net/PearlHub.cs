using System.Diagnostics;
using System.Globalization;
using Linkpearl.Diagnostics;
using Linkpearl.Platform;
using Linkpearl.Time;

namespace Linkpearl.Net;

public sealed partial class PearlHub : IPearlHub, IDisposable
{
    private const float RefreshSeconds = 15f;

    private readonly GateClient client;
    private readonly IGameSession game;
    private readonly IFrameLoop frames;
    private readonly ILinkpearlLog log;
    private readonly Action<string?> persistToken;
    private readonly string mediaCache;
    private readonly object gate = new();
    private PearlSnapshot snapshot = PearlSnapshot.Empty;
    private readonly CancellationTokenSource lifetime = new();
    private int generation;
    private float sinceRefresh = RefreshSeconds;
    private bool refreshQueued;
    private bool signInQueued;
    private bool signOutQueued;
    private bool patronLinkQueued;
    private string pendingQuery = string.Empty;
    private string activeQuery = string.Empty;
    private bool searchQueued;
    private string watchedChat = string.Empty;
    private bool chatFetchQueued;
    private readonly Dictionary<string, List<PearlChatLine>> chatLines = new(StringComparer.Ordinal);
    private readonly Queue<(string ChatId, string Body)> outgoing = new();
    private string feedTab = "foryou";
    private bool feedQueued;
    private string watchedPost = string.Empty;
    private bool postQueued;
    private string watchedUser = string.Empty;
    private bool profileQueued;
    private readonly Dictionary<string, List<PearlComment>> postComments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PearlPost> postIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> mediaPaths = new(StringComparer.Ordinal);
    private readonly Queue<string> mediaWanted = new();
    private readonly HashSet<string> mediaFailed = new(StringComparer.Ordinal);
    private readonly Queue<SocialWrite> writes = new();
    private readonly Queue<(bool Add, uint ItemId, string Label)> marketWrites = new();
    private readonly Queue<(bool Add, string UserId, string Number)> friendWrites = new();

    public PearlHub(string baseUrl, string? savedToken, IGameSession game, IFrameLoop frames, ILinkpearlLog log,
        Action<string?> persistToken, string? mediaCache = null)
    {
        client = new GateClient(string.IsNullOrWhiteSpace(baseUrl) ? GateClient.DefaultBaseUrl : baseUrl);
        this.game = game;
        this.frames = frames;
        this.log = log;
        this.persistToken = persistToken;
        this.mediaCache = string.IsNullOrWhiteSpace(mediaCache)
            ? Path.Combine(Path.GetTempPath(), "linkpearl-media")
            : mediaCache;
        Directory.CreateDirectory(this.mediaCache);
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

    public void BeginPatronLink() => patronLinkQueued = true;

    public void OpenPatronLink()
    {
        var url = Current.PatronLinkUrl;
        if (url.Length == 0)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception failure)
        {
            log.Write(LogSeverity.Warning, failure, "Could not open Patreon link");
        }
    }

    public void AddFriend(string userId, string number)
    {
        var id = userId.Trim();
        var digits = Digits(number);
        if ((id.Length == 0 && digits.Length == 0) || !Current.SignedIn)
        {
            return;
        }

        if (string.Equals(id, Current.MeId, StringComparison.Ordinal))
        {
            return;
        }

        lock (gate)
        {
            friendWrites.Enqueue((true, id, digits));
        }
    }

    public void RemoveFriend(string userId)
    {
        var id = userId.Trim();
        if (id.Length == 0 || !Current.SignedIn)
        {
            return;
        }

        lock (gate)
        {
            friendWrites.Enqueue((false, id, string.Empty));
        }
    }

    public void WatchMarket(uint itemId, string label)
    {
        if (itemId == 0 || !Current.SignedIn)
        {
            return;
        }

        lock (gate)
        {
            marketWrites.Enqueue((true, itemId, label.Trim()));
        }
    }

    public void UnwatchMarket(uint itemId)
    {
        if (itemId == 0 || !Current.SignedIn)
        {
            return;
        }

        lock (gate)
        {
            marketWrites.Enqueue((false, itemId, string.Empty));
        }
    }

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

    public void WatchChat(string chatId)
    {
        var id = chatId.Trim();
        if (id.Length == 0)
        {
            return;
        }

        if (!string.Equals(watchedChat, id, StringComparison.Ordinal))
        {
            watchedChat = id;
            chatFetchQueued = true;
        }
    }

    public void SendChat(string chatId, string body)
    {
        var id = chatId.Trim();
        var text = body.Trim();
        if (id.Length == 0 || text.Length == 0 || !Current.SignedIn)
        {
            return;
        }

        RememberLine(id, new PearlChatLine(true, text, "now", Current.MeName.Length > 0 ? Current.MeName : "You"));
        lock (gate)
        {
            outgoing.Enqueue((id, text));
        }
    }

    public IReadOnlyList<PearlChatLine> LinesFor(string chatId)
    {
        lock (gate)
        {
            return chatLines.TryGetValue(chatId, out var lines) ? lines.ToArray() : [];
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

        if (patronLinkQueued)
        {
            patronLinkQueued = false;
            Start(RunPatronLinkAsync);
        }

        if (searchQueued)
        {
            searchQueued = false;
            Start(RunSearchAsync);
        }

        (string ChatId, string Body)? send = null;
        lock (gate)
        {
            if (outgoing.Count > 0)
            {
                send = outgoing.Dequeue();
            }
        }

        if (send is { } packet)
        {
            var chatId = packet.ChatId;
            var body = packet.Body;
            Start(token => RunSendChatAsync(chatId, body, token));
        }

        if (chatFetchQueued)
        {
            chatFetchQueued = false;
            var id = watchedChat;
            Start(token => RunFetchChatAsync(id, token));
        }

        if (feedQueued)
        {
            feedQueued = false;
            var tab = feedTab;
            Start(token => RunFeedAsync(tab, token));
        }

        if (postQueued)
        {
            postQueued = false;
            var id = watchedPost;
            Start(token => RunPostAsync(id, token));
        }

        if (profileQueued)
        {
            profileQueued = false;
            var id = watchedUser;
            Start(token => RunProfilePostsAsync(id, token));
        }

        SocialWrite? write = null;
        (bool Add, uint ItemId, string Label)? market = null;
        (bool Add, string UserId, string Number)? friend = null;
        string? mediaUrl = null;
        lock (gate)
        {
            if (writes.Count > 0)
            {
                write = writes.Dequeue();
            }

            if (marketWrites.Count > 0)
            {
                market = marketWrites.Dequeue();
            }

            if (friendWrites.Count > 0)
            {
                friend = friendWrites.Dequeue();
            }

            if (mediaWanted.Count > 0)
            {
                mediaUrl = mediaWanted.Dequeue();
            }
        }

        if (write is { } op)
        {
            Start(token => RunWriteAsync(op, token));
        }

        if (market is { } watch)
        {
            var add = watch.Add;
            var itemId = watch.ItemId;
            var label = watch.Label;
            Start(token => RunMarketWriteAsync(add, itemId, label, token));
        }

        if (friend is { } link)
        {
            var add = link.Add;
            var userId = link.UserId;
            var number = link.Number;
            Start(token => RunFriendWriteAsync(add, userId, number, token));
        }

        if (mediaUrl is { Length: > 0 } url)
        {
            Start(token => RunMediaAsync(url, token));
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
        lock (gate)
        {
            ForgetSessionLocked();
        }

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
            var (feedPage, feedStatus) = await client
                .GetAsync("/feed?tab=" + Uri.EscapeDataString(feedTab), GateJson.Default.PostPageDto, token)
                .ConfigureAwait(false);
            var (notesPage, notesStatus) = await client.GetAsync("/notifications", GateJson.Default.NotePageDto, token)
                .ConfigureAwait(false);
            if (game.RetainersReady)
            {
                var report = MapRetainerReport(game.Retainers);
                await client.PutAsync("/me/retainers",
                        GateClient.JsonBody(report, GateJson.Default.RetainerPageDto), token)
                    .ConfigureAwait(false);
            }

            var (retainersPage, retainersStatus) = await client
                .GetAsync("/retainers", GateJson.Default.RetainerPageDto, token)
                .ConfigureAwait(false);
            var (marketPage, marketStatus) = await client
                .GetAsync("/market", GateJson.Default.MarketPageDto, token)
                .ConfigureAwait(false);

            var chats = MapChats(chatsPage?.Items);
            var people = MapPeople(contacts?.Contacts);
            var feed = MapPosts(feedPage?.Items);
            var notes = MapNotes(notesPage?.Items);
            RememberPosts(feed);
            var prior = Current;
            Replace(new PearlSnapshot
            {
                SignedIn = true,
                Busy = false,
                Notice = string.Empty,
                MeId = me.Id ?? string.Empty,
                MeName = Display(me.DisplayName, me.Name),
                MeWorld = me.World ?? string.Empty,
                MeHandle = me.Handle ?? string.Empty,
                MeBio = me.Bio ?? string.Empty,
                MeAvatarUrl = me.AvatarUrl ?? string.Empty,
                MyNumber = LineNumber(me.PhoneNumber, contacts),
                Followers = me.Followers,
                Following = me.Following,
                FounderSeat = SeatOf(me),
                IsPatron = PatronOf(me),
                PatronLinked = PatronOf(me),
                PatronLinkUrl = me.PatreonUrl ?? Current.PatronLinkUrl,
                Chats = chats,
                People = people,
                Stories = MapStories(stories?.Rings),
                Announcements = MapAnnouncements(announcements?.Items),
                SearchHits = prior.SearchHits,
                SearchPosts = prior.SearchPosts,
                Feed = feedStatus is >= 200 and < 300 ? feed : prior.Feed,
                FeedTab = feedTab,
                FeedLive = feedStatus is >= 200 and < 300,
                Notes = notesStatus is >= 200 and < 300 ? notes : prior.Notes,
                NotesLive = notesStatus is >= 200 and < 300,
                ProfilePosts = prior.ProfilePosts,
                Retainers = retainersStatus is >= 200 and < 300 ? MapRetainers(retainersPage?.Items) : prior.Retainers,
                MarketWatches = marketStatus is >= 200 and < 300 ? MapMarket(marketPage?.Items) : prior.MarketWatches,
                WatchedUserId = watchedUser,
                WatchedPostId = watchedPost,
                UnreadTotal = CountUnread(chats),
                Generation = NextGeneration(),
            });
            if (watchedChat.Length > 0)
            {
                chatFetchQueued = true;
            }

            if (watchedPost.Length > 0)
            {
                postQueued = true;
            }

            if (watchedUser.Length > 0)
            {
                profileQueued = true;
            }
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
            var (posts, postStatus) = await client
                .GetAsync("/posts/search?q=" + Uri.EscapeDataString(query), GateJson.Default.PostPageDto, token)
                .ConfigureAwait(false);
            if (status == 401 || postStatus == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            if (!string.Equals(activeQuery, pendingQuery, StringComparison.Ordinal))
            {
                return;
            }

            var mappedPosts = postStatus is >= 200 and < 300 ? MapPosts(posts?.Items) : [];
            RememberPosts(mappedPosts);
            Replace(Current with
            {
                SearchHits = MapHits(page?.Users),
                SearchPosts = mappedPosts,
                Generation = NextGeneration(),
            });
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
            MeId = user?.Id ?? string.Empty,
            MeName = user is null ? string.Empty : Display(user.DisplayName, user.Name),
            MeWorld = user?.World ?? string.Empty,
            MeHandle = user?.Handle ?? string.Empty,
            MeBio = user?.Bio ?? string.Empty,
            MeAvatarUrl = user?.AvatarUrl ?? string.Empty,
            MyNumber = LineNumber(user?.PhoneNumber, null),
            Followers = user?.Followers ?? 0,
            Following = user?.Following ?? 0,
            FounderSeat = SeatOf(user),
            IsPatron = PatronOf(user),
            PatronLinked = PatronOf(user),
            PatronLinkUrl = user?.PatreonUrl ?? string.Empty,
            Generation = NextGeneration(),
        });
    }

    private void DropSession(string notice)
    {
        client.SetBearer(string.Empty);
        persistToken(null);
        lock (gate)
        {
            ForgetSessionLocked();
        }

        Replace(new PearlSnapshot { Notice = notice, Generation = NextGeneration() });
    }

    private void ForgetSessionLocked()
    {
        chatLines.Clear();
        outgoing.Clear();
        watchedChat = string.Empty;
        postComments.Clear();
        postIndex.Clear();
        writes.Clear();
        marketWrites.Clear();
        friendWrites.Clear();
        watchedPost = string.Empty;
        watchedUser = string.Empty;
    }

    private void Replace(PearlSnapshot next)
    {
        lock (gate)
        {
            snapshot = next.Generation == 0 ? next with { Generation = NextGeneration() } : next;
        }
    }

    private int NextGeneration() => Interlocked.Increment(ref generation);

    private async Task RunFetchChatAsync(string chatId, CancellationToken token)
    {
        if (chatId.Length == 0 || !Current.SignedIn)
        {
            return;
        }

        try
        {
            var path = "/chats/" + Uri.EscapeDataString(chatId) + "/messages";
            var (page, status) = await client.GetAsync(path, GateJson.Default.ChatMessagePageDto, token)
                .ConfigureAwait(false);
            if (status == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            if (status is < 200 or >= 300 || page is null)
            {
                return;
            }

            var mapped = MapChatLines(page);
            lock (gate)
            {
                chatLines[chatId] = mapped;
                generation = NextGeneration();
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate chat fetch failed");
        }
    }

    private async Task RunSendChatAsync(string chatId, string body, CancellationToken token)
    {
        try
        {
            var content = GateClient.JsonBody(new SendChatDto(body), GateJson.Default.SendChatDto);
            var path = "/chats/" + Uri.EscapeDataString(chatId) + "/messages";
            var status = await client.PostAsync(path, content, token).ConfigureAwait(false);
            if (status == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            if (status is >= 200 and < 300)
            {
                chatFetchQueued = true;
                QueueRefresh();
                return;
            }

            log.Write(LogSeverity.Warning, "Pearlgate send returned HTTP " + status);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate send failed");
        }
    }

    private void RememberLine(string chatId, PearlChatLine line)
    {
        lock (gate)
        {
            if (!chatLines.TryGetValue(chatId, out var lines))
            {
                lines = new List<PearlChatLine>();
                chatLines[chatId] = lines;
            }

            lines.Add(line);
            generation = NextGeneration();
        }
    }

    private static List<PearlChatLine> MapChatLines(ChatMessagePageDto page)
    {
        var items = page.Items ?? page.Messages;
        var mapped = new List<PearlChatLine>();
        if (items is null)
        {
            return mapped;
        }

        foreach (var item in items)
        {
            var body = item.Body ?? item.Text ?? item.Content ?? string.Empty;
            if (body.Length == 0)
            {
                continue;
            }

            var when = item.CreatedAtUnix > 0
                ? DateTimeOffset.FromUnixTimeSeconds(item.CreatedAtUnix).ToLocalTime().ToString("HH:mm")
                : "now";
            mapped.Add(new PearlChatLine(item.Mine, body, when,
                item.AuthorDisplayName ?? (item.Mine ? "You" : "Them")));
        }

        return mapped;
    }

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
                item.LastMessagePreview ?? string.Empty, item.UnreadCount, item.LastMessageAtUnix, item.IsGroup,
                item.OtherUserId ?? string.Empty);
        }

        return mapped;
    }

    private static string LineNumber(string? fromProfile, ContactListDto? contacts)
    {
        if (!string.IsNullOrWhiteSpace(fromProfile))
        {
            return fromProfile.Trim();
        }

        if (contacts is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(contacts.MyNumber))
        {
            return contacts.MyNumber.Trim();
        }

        if (!string.IsNullOrWhiteSpace(contacts.PhoneNumber))
        {
            return contacts.PhoneNumber.Trim();
        }

        return string.IsNullOrWhiteSpace(contacts.Number) ? string.Empty : contacts.Number.Trim();
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
                item.PhoneNumber ?? string.Empty, item.IsMutual, item.AvatarUrl ?? string.Empty,
                item.Race ?? string.Empty, item.World ?? string.Empty);
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
                string.IsNullOrWhiteSpace(user.World) ? user.Handle ?? string.Empty : user.World,
                user.Id ?? string.Empty);
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

    private async Task RunPatronLinkAsync(CancellationToken token)
    {
        if (!Current.SignedIn)
        {
            Replace(Current with { Notice = "Sign in to Pearlgate first, then connect Patreon." });
            return;
        }

        Replace(Current with { Busy = true, Notice = "Checking Patreon with Pearlgate..." });
        try
        {
            var (status, statusCode) = await client.GetAsync("/patreon", GateJson.Default.PatronDto, token)
                .ConfigureAwait(false);
            if (statusCode == 404)
            {
                var (alt, altCode) = await client.GetAsync("/me/patreon", GateJson.Default.PatronDto, token)
                    .ConfigureAwait(false);
                status = alt;
                statusCode = altCode;
            }

            if (statusCode is >= 200 and < 300 && status is not null)
            {
                var url = status.Url ?? status.LinkUrl ?? string.Empty;
                var patron = status.Patron == true || status.IsPatron == true;
                Replace(Current with
                {
                    Busy = false,
                    IsPatron = patron || Current.IsPatron,
                    PatronLinked = status.Linked == true || patron,
                    PatronLinkUrl = url.Length > 0 ? url : Current.PatronLinkUrl,
                    Notice = patron
                        ? "Patreon is linked. Patron settings and the rose badge are on this account."
                        : url.Length > 0
                            ? "Finish linking Patreon in the page that opened."
                            : "Pearlgate has the Patreon hook. Pledge, then wait for the next refresh.",
                });
                if (url.Length > 0)
                {
                    OpenPatronLink();
                }

                QueueRefresh();
                return;
            }

            var (linked, linkCode) = await client.PostAsync("/patreon/link", null, GateJson.Default.PatronDto, token)
                .ConfigureAwait(false);
            if (linkCode is >= 200 and < 300 && linked is not null)
            {
                var url = linked.Url ?? linked.LinkUrl ?? string.Empty;
                Replace(Current with
                {
                    Busy = false,
                    PatronLinkUrl = url,
                    PatronLinked = linked.Linked == true,
                    IsPatron = linked.Patron == true || linked.IsPatron == true || Current.IsPatron,
                    Notice = url.Length > 0
                        ? "Open the Patreon page to finish linking this Pearlgate account."
                        : "Pearlgate accepted the Patreon link request.",
                });
                if (url.Length > 0)
                {
                    OpenPatronLink();
                }

                QueueRefresh();
                return;
            }

            Replace(Current with
            {
                Busy = false,
                Notice = "Pearlgate will attach Patreon when your pledge is linked. Refresh runs every 15 seconds.",
            });
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Patreon link failed");
            Replace(Current with
            {
                Busy = false,
                Notice = "Could not reach Patreon through Pearlgate. Try again in a moment.",
            });
        }
    }

    private static bool PatronOf(GateUserDto? user) =>
        user is not null && (user.Patron == true || user.IsPatron == true || user.Patreon == true ||
                             user.PatreonActive == true);

    private static int SeatOf(GateUserDto? user)
    {
        if (user is null)
        {
            return 0;
        }

        if (user.FounderSeat is > 0)
        {
            return user.FounderSeat.Value;
        }

        return user.Seat is > 0 ? user.Seat.Value : 0;
    }

    private async Task RunMarketWriteAsync(bool add, uint itemId, string label, CancellationToken token)
    {
        try
        {
            int status;
            if (add)
            {
                var body = new MarketWatchBodyDto((int)itemId, game.Character.WorldName, label.Length == 0 ? null : label);
                status = await client
                    .PostAsync("/market", GateClient.JsonBody(body, GateJson.Default.MarketWatchBodyDto), token)
                    .ConfigureAwait(false);
            }
            else
            {
                status = await client.DeleteAsync("/market/" + itemId.ToString(CultureInfo.InvariantCulture), token)
                    .ConfigureAwait(false);
            }

            if (status == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            if (status is >= 200 and < 300)
            {
                QueueRefresh();
                return;
            }

            log.Write(LogSeverity.Warning, "Pearlgate market write returned HTTP " + status);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate market write failed");
        }
    }

    private async Task RunFriendWriteAsync(bool add, string userId, string number, CancellationToken token)
    {
        try
        {
            int status;
            if (add)
            {
                var body = userId.Length > 0
                    ? new AddContactBodyDto(null, null, userId)
                    : new AddContactBodyDto(number, null, null);
                status = await client
                    .PostAsync("/contacts/", GateClient.JsonBody(body, GateJson.Default.AddContactBodyDto), token)
                    .ConfigureAwait(false);
            }
            else
            {
                status = await client.DeleteAsync("/contacts/" + Uri.EscapeDataString(userId), token)
                    .ConfigureAwait(false);
            }

            if (status == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            if (status is >= 200 and < 300 or 409)
            {
                QueueRefresh();
                return;
            }

            if (status == 404)
            {
                Replace(Current with { Notice = "Nobody on Pearlgate matches that number.", Generation = NextGeneration() });
                return;
            }

            if (status == 400)
            {
                Replace(Current with { Notice = "Can't add that Pearlgate account.", Generation = NextGeneration() });
                return;
            }

            log.Write(LogSeverity.Warning, "Pearlgate friend write returned HTTP " + status);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate friend write failed");
        }
    }

    private static string Digits(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Where(char.IsDigit).ToArray();
        return new string(chars);
    }

    private static RetainerPageDto MapRetainerReport(IReadOnlyList<GameRetainer> rows)
    {
        var items = new RetainerDto[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            items[index] = new RetainerDto(row.Slot, row.Name, row.Gil, row.ItemsOnSale, row.VentureUntilUnix);
        }

        return new RetainerPageDto(items);
    }

    private static PearlRetainer[] MapRetainers(RetainerDto[]? items)
    {
        if (items is null || items.Length == 0)
        {
            return [];
        }

        var mapped = new List<PearlRetainer>(items.Length);
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                continue;
            }

            mapped.Add(new PearlRetainer(item.Slot, item.Name, item.Gil, item.ItemsOnSale, item.VentureUntilUnix));
        }

        return mapped.ToArray();
    }

    private static PearlMarketWatch[] MapMarket(MarketWatchDto[]? items)
    {
        if (items is null || items.Length == 0)
        {
            return [];
        }

        var mapped = new List<PearlMarketWatch>(items.Length);
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item.ItemId <= 0)
            {
                continue;
            }

            mapped.Add(new PearlMarketWatch(item.ItemId, item.Label ?? string.Empty, item.World ?? string.Empty,
                item.NqGil, item.HqGil, item.Listed));
        }

        return mapped.ToArray();
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
