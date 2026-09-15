using System.Diagnostics;
using System.Globalization;
using Linkpearl.Diagnostics;
using Linkpearl.Platform;
using Linkpearl.Time;

namespace Linkpearl.Net;

public sealed partial class PearlHub : IPearlHub, IDisposable
{
    private const float RefreshSeconds = 15f;
    private const float NoticeSeconds = 4f;
    private const float HealthSeconds = 20f;

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
    private float sinceNotices = NoticeSeconds;
    private float sinceHealth = HealthSeconds;
    private bool refreshQueued;
    private bool signInQueued;
    private string xivFlowId = string.Empty;
    private float xivPollWait;
    private float xivPollInterval = 2f;
    private bool xivPollBusy;
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
    private readonly Queue<string> chatCreates = new();
    private readonly HashSet<string> creatingChats = new(StringComparer.Ordinal);
    private readonly Queue<string> staffReads = new();
    private readonly Queue<(string Type, string Id, string Reason, string Detail, PearlReportLine[] Lines)> reports = new();

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
            Replace(new PearlSnapshot { SignedIn = true, Busy = true, GateLive = true, Notice = "Connecting to Pearlgate..." });
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

    public void OpenPatronLink() => OpenBrowser(Current.PatronLinkUrl);

    private void OpenBrowser(string url)
    {
        if (url.Length == 0 ||
            url.StartsWith("https://localhost", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception failure)
        {
            log.Write(LogSeverity.Warning, failure, "Could not open browser");
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

        if (BlockMuted())
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

        if (BlockMuted())
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

    public void MarkStaffNotice(string id)
    {
        var noticeId = id.Trim();
        if (noticeId.StartsWith("staff:", StringComparison.Ordinal))
        {
            noticeId = noticeId["staff:".Length..];
        }

        if (noticeId.Length == 0)
        {
            return;
        }

        lock (gate)
        {
            staffReads.Enqueue(noticeId);
            var notices = snapshot.StaffNotices;
            var next = new PearlStaffNotice[notices.Length];
            var changed = false;
            for (var index = 0; index < notices.Length; index++)
            {
                var row = notices[index];
                if (!changed && string.Equals(row.Id, noticeId, StringComparison.Ordinal) && !row.Read)
                {
                    next[index] = row with { Read = true };
                    changed = true;
                }
                else
                {
                    next[index] = row;
                }
            }

            if (changed)
            {
                snapshot = snapshot with { StaffNotices = next, Generation = NextGeneration() };
            }
        }
    }

    public void Report(string targetType, string targetId, string reason, string detail) =>
        Report(targetType, targetId, reason, detail, []);

    public void Report(string targetType, string targetId, string reason, string detail,
        IReadOnlyList<PearlReportLine> messages)
    {
        var type = targetType.Trim();
        var id = targetId.Trim();
        var why = reason.Trim();
        if (type.Length == 0 || id.Length == 0 || why.Length == 0)
        {
            return;
        }

        if (!Current.SignedIn)
        {
            Replace(Current with
            {
                Notice = "Sign in on You to send a report to staff.",
                Generation = NextGeneration(),
            });
            return;
        }

        var packed = new PearlReportLine[Math.Min(messages.Count, 40)];
        for (var index = 0; index < packed.Length; index++)
        {
            packed[index] = messages[index];
        }

        lock (gate)
        {
            reports.Enqueue((type, id, why, detail.Trim(), packed));
        }

        Replace(Current with
        {
            Notice = "Report sent to Pearlgate staff.",
            Generation = NextGeneration(),
        });
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

    public string ChatFor(string userId)
    {
        var id = userId.Trim();
        if (id.Length == 0)
        {
            return string.Empty;
        }

        var chats = Current.Chats;
        for (var index = 0; index < chats.Length; index++)
        {
            var chat = chats[index];
            if (!chat.IsGroup && string.Equals(chat.OtherUserId, id, StringComparison.Ordinal))
            {
                return chat.Id;
            }
        }

        return string.Empty;
    }

    public void StartChat(string userId)
    {
        var id = userId.Trim();
        if (id.Length == 0 || string.Equals(id, Current.MeId, StringComparison.Ordinal))
        {
            return;
        }

        if (!Current.SignedIn)
        {
            Replace(Current with
            {
                Notice = "Sign in from You to start a Pearlgate chat.",
                Generation = NextGeneration(),
            });
            return;
        }

        if (BlockMuted())
        {
            return;
        }

        var existing = ChatFor(id);
        if (existing.Length > 0)
        {
            WatchChat(existing);
            return;
        }

        lock (gate)
        {
            if (!creatingChats.Add(id))
            {
                return;
            }

            chatCreates.Enqueue(id);
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

        if (BlockMuted())
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
        sinceNotices += deltaSeconds;
        sinceHealth += deltaSeconds;
        if (signInQueued)
        {
            signInQueued = false;
            Start(RunSignInAsync);
        }

        if (xivFlowId.Length > 0 && !Current.SignedIn && !Current.Busy)
        {
            xivPollWait -= deltaSeconds;
            if (!xivPollBusy && xivPollWait <= 0f)
            {
                xivPollBusy = true;
                xivPollWait = xivPollInterval;
                var flowId = xivFlowId;
                Start(token => RunXivAuthPollAsync(flowId, token));
            }
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
        string? createChat = null;
        string? mediaUrl = null;
        string? staffNotice = null;
        (string Type, string Id, string Reason, string Detail, PearlReportLine[] Lines)? report = null;
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

            if (chatCreates.Count > 0)
            {
                createChat = chatCreates.Dequeue();
            }

            if (mediaWanted.Count > 0)
            {
                mediaUrl = mediaWanted.Dequeue();
            }

            if (staffReads.Count > 0)
            {
                staffNotice = staffReads.Dequeue();
            }

            if (reports.Count > 0)
            {
                report = reports.Dequeue();
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

        if (createChat is { Length: > 0 } peerId)
        {
            Start(token => RunStartChatAsync(peerId, token));
        }

        if (mediaUrl is { Length: > 0 } url)
        {
            Start(token => RunMediaAsync(url, token));
        }

        if (staffNotice is { Length: > 0 } noticeId)
        {
            Start(token => RunStaffReadAsync(noticeId, token));
        }

        if (report is { } filed)
        {
            var type = filed.Type;
            var targetId = filed.Id;
            var reason = filed.Reason;
            var detail = filed.Detail;
            var lines = filed.Lines;
            Start(token => RunReportAsync(type, targetId, reason, detail, lines, token));
        }

        if (Current.SignedIn && sinceNotices >= NoticeSeconds)
        {
            sinceNotices = 0f;
            Start(RunStaffNoticesAsync);
        }

        if (refreshQueued || (Current.SignedIn && sinceRefresh >= RefreshSeconds))
        {
            refreshQueued = false;
            sinceRefresh = 0f;
            Start(RunRefreshAsync);
        }

        if (sinceHealth >= HealthSeconds)
        {
            sinceHealth = 0f;
            Start(RunHealthAsync);
        }
    }

    private async Task RunHealthAsync(CancellationToken token)
    {
        try
        {
            var (body, status) = await client.GetAsync("/health", GateJson.Default.HealthDto, token)
                .ConfigureAwait(false);
            var live = status is >= 200 and < 300 && body is { Ok: true };
            lock (gate)
            {
                snapshot = snapshot with { GateLive = live };
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate health failed");
            lock (gate)
            {
                snapshot = snapshot with { GateLive = false };
            }
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

        xivFlowId = string.Empty;
        Replace(Current with { Busy = true, ChallengeCode = string.Empty, SignInUrl = string.Empty, Notice = "Signing in..." });
        try
        {
            var startBody = GateClient.JsonBody(new XivAuthStartRequestDto(character.Name, character.WorldName),
                GateJson.Default.XivAuthStartRequestDto);
            var (start, startStatus) = await client
                .PostAsync("/auth/xivauth/start", startBody, GateJson.Default.XivAuthStartReplyDto, token)
                .ConfigureAwait(false);
            if (start is { Ok: true, FlowId: { Length: > 0 } flowId })
            {
                xivFlowId = flowId;
                xivPollInterval = start.IntervalSeconds > 0 ? start.IntervalSeconds : 2f;
                xivPollWait = 0f;
                var url = start.VerificationUriComplete ?? start.VerificationUri ?? string.Empty;
                OpenBrowser(url);
                var who = character.Name + " on " + character.WorldName;
                Replace(Current with
                {
                    Busy = false,
                    ChallengeCode = start.UserCode ?? string.Empty,
                    SignInUrl = url,
                    Notice = url.Length > 0
                        ? "Confirm " + who + " in the XIVAuth page. If no browser opened, open " + url
                        : "Confirm " + who + " on XIVAuth, then wait here.",
                });
                return;
            }

            var reason = start?.Reason ?? $"http {startStatus}";
            Replace(Current with { Busy = false, ChallengeCode = string.Empty, SignInUrl = string.Empty, Notice = SignInFailure(reason) });
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate sign-in failed");
            Replace(Current with { Busy = false, GateLive = false, Notice = "Sign-in failed. Try again in a moment." });
        }
    }

    private async Task RunXivAuthPollAsync(string flowId, CancellationToken token)
    {
        try
        {
            var body = GateClient.JsonBody(new XivAuthPollRequestDto(flowId), GateJson.Default.XivAuthPollRequestDto);
            var (verify, status) = await client
                .PostAsync("/auth/xivauth/poll", body, GateJson.Default.VerifyReplyDto, token)
                .ConfigureAwait(false);
            if (verify is { Ok: true, Token: { Length: > 0 } })
            {
                xivFlowId = string.Empty;
                AcceptSession(verify.Token, verify.User, "Signed in.");
                QueueRefresh();
                return;
            }

            var reason = verify?.Reason ?? $"http {status}";
            if (string.Equals(reason, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(reason, "slow_down", StringComparison.OrdinalIgnoreCase))
            {
                xivPollInterval = Math.Min(10f, xivPollInterval + 1f);
                return;
            }

            xivFlowId = string.Empty;
            Replace(Current with
            {
                Busy = false,
                ChallengeCode = string.Empty,
                SignInUrl = string.Empty,
                Notice = SignInFailure(reason),
            });
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate XIVAuth poll failed");
        }
        finally
        {
            xivPollBusy = false;
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
        xivFlowId = string.Empty;
        xivPollBusy = false;
        lock (gate)
        {
            ForgetSessionLocked();
        }

        Replace(new PearlSnapshot { Notice = "Signed out.", GateLive = Current.GateLive, Generation = NextGeneration() });
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

            if (meStatus == 403)
            {
                await ApplyStaffStateAsync(null, true, token).ConfigureAwait(false);
                return;
            }

            if (me is null)
            {
                Replace(Current with { Busy = false, Notice = "Pearlgate is up, but /me did not return a profile." });
                return;
            }

            var (noticesPage, noticesStatus) = await client
                .GetAsync("/account/notices", GateJson.Default.AccountNoticePageDto, token)
                .ConfigureAwait(false);
            var meId = me.Id ?? string.Empty;
            var (bans, bansStatus) = meId.Length == 0
                ? (null, 0)
                : await client.GetAsync("/bans/" + Uri.EscapeDataString(meId), GateJson.Default.BanSnapshotDto, token)
                    .ConfigureAwait(false);
            var staffNotices = KeepLocalStaffReads(noticesStatus is >= 200 and < 300
                ? MapStaffNotices(noticesPage?.Items)
                : MapStaffNotices(me.StaffNotices));
            var banned = bansStatus is >= 200 and < 300
                ? bans?.Banned == true
                : me.Banned == true;
            var muted = bansStatus is >= 200 and < 300
                ? bans?.Muted == true
                : me.Muted == true;
            var muteUntil = bansStatus is >= 200 and < 300
                ? bans?.MuteUntilUnix ?? 0
                : me.MuteUntilUnix;

            var (chatsPage, _) = await client.GetAsync("/chats/", GateJson.Default.ConversationPageDto, token)
                .ConfigureAwait(false);
            var (contacts, _) = await client.GetAsync("/contacts/", GateJson.Default.ContactListDto, token)
                .ConfigureAwait(false);
            var (directoryPage, directoryStatus) = await client
                .GetAsync("/users/directory", GateJson.Default.UserSearchDto, token)
                .ConfigureAwait(false);
            var (stories, _) = await client.GetAsync("/stories", GateJson.Default.StoryTrayDto, token)
                .ConfigureAwait(false);
            var (announcements, _) = await client.GetAsync("/announcements", GateJson.Default.AnnouncementPageDto, token)
                .ConfigureAwait(false);
            var (feedPage, feedStatus) = await client
                .GetAsync("/vybe/feed?tab=" + Uri.EscapeDataString(feedTab), GateJson.Default.PostPageDto, token)
                .ConfigureAwait(false);
            if (feedStatus is < 200 or >= 300)
            {
                (feedPage, feedStatus) = await client
                    .GetAsync("/feed?tab=" + Uri.EscapeDataString(feedTab), GateJson.Default.PostPageDto, token)
                    .ConfigureAwait(false);
            }
            var (plusFeed, plusFeedLive) = await LoadVybePlusFeedAsync(token).ConfigureAwait(false);
            var sfwFeed = feedStatus is >= 200 and < 300 ? MapPosts(feedPage?.Items) : [];
            var feed = MergePosts(plusFeed, sfwFeed);
            var feedLive = plusFeedLive || feedStatus is >= 200 and < 300;
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
            var prior = Current;
            var directory = directoryStatus is >= 200 and < 300
                ? MapDirectory(directoryPage?.Users, meId)
                : prior.Directory;
            var notes = MapNotes(notesPage?.Items);
            RememberPosts(feed);
            Replace(new PearlSnapshot
            {
                SignedIn = true,
                Busy = false,
                GateLive = true,
                Notice = banned
                    ? (me.BanReason is { Length: > 0 } reason ? reason : "This account is suspended.")
                    : muted
                        ? "Staff muted this handset."
                        : string.Empty,
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
                Directory = directory,
                Stories = MapStories(stories?.Rings),
                Announcements = MapAnnouncements(announcements?.Items),
                SearchHits = prior.SearchHits,
                SearchPosts = prior.SearchPosts,
                Feed = feedLive ? feed : prior.Feed,
                FeedTab = feedTab,
                FeedLive = feedLive,
                Notes = notesStatus is >= 200 and < 300 ? notes : prior.Notes,
                NotesLive = notesStatus is >= 200 and < 300,
                ProfilePosts = prior.ProfilePosts,
                Retainers = retainersStatus is >= 200 and < 300 ? MapRetainers(retainersPage?.Items) : prior.Retainers,
                MarketWatches = marketStatus is >= 200 and < 300 ? MapMarket(marketPage?.Items) : prior.MarketWatches,
                StaffNotices = staffNotices.Length > 0 || noticesStatus is >= 200 and < 300
                    ? staffNotices
                    : prior.StaffNotices,
                Banned = banned,
                Muted = muted,
                MuteUntilUnix = muteUntil,
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
            Replace(Current with { Busy = false, Notice = "Lost Pearlgate for a moment. Retrying.", GateLive = false });
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
            GateLive = true,
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
        xivFlowId = string.Empty;
        xivPollBusy = false;
        lock (gate)
        {
            ForgetSessionLocked();
        }

        Replace(new PearlSnapshot { Notice = notice, GateLive = Current.GateLive, Generation = NextGeneration() });
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
        chatCreates.Clear();
        creatingChats.Clear();
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

            var mapped = MapChatLines(page, Current.MeId);
            lock (gate)
            {
                chatLines[chatId] = MergePending(chatId, mapped);
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

            if (status == 403)
            {
                Replace(Current with
                {
                    Notice = "Staff muted this handset.",
                    Muted = true,
                    Generation = NextGeneration(),
                });
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

    private async Task RunStartChatAsync(string userId, CancellationToken token)
    {
        try
        {
            var (conversation, status) = await PostCreateChatAsync(userId, token).ConfigureAwait(false);
            if (status == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            if (status == 403)
            {
                Replace(Current with
                {
                    Notice = "Staff muted this handset.",
                    Muted = true,
                    Generation = NextGeneration(),
                });
                return;
            }

            if (status is >= 200 and < 300 || status == 409)
            {
                var mapped = conversation is null ? null : MapChats([conversation]);
                if (mapped is { Length: > 0 } && mapped[0].Id.Length > 0)
                {
                    UpsertChat(mapped[0], userId);
                    WatchChat(mapped[0].Id);
                    QueueRefresh();
                    return;
                }

                var (page, listStatus) = await client.GetAsync("/chats/", GateJson.Default.ConversationPageDto, token)
                    .ConfigureAwait(false);
                if (listStatus is >= 200 and < 300)
                {
                    var chats = MapChats(page?.Items);
                    Replace(Current with { Chats = chats, Generation = NextGeneration() });
                    var found = ChatFor(userId);
                    if (found.Length > 0)
                    {
                        WatchChat(found);
                        return;
                    }
                }

                QueueRefresh();
                if (status == 409 && ChatFor(userId).Length == 0)
                {
                    Replace(Current with
                    {
                        Notice = "Couldn't start that Pearlgate chat.",
                        Generation = NextGeneration(),
                    });
                }

                return;
            }

            var notice = status is 400 or 404
                ? "Can't start a chat with that Pearlgate account."
                : "Couldn't start that Pearlgate chat.";
            Replace(Current with { Notice = notice, Generation = NextGeneration() });
            log.Write(LogSeverity.Warning, "Pearlgate create chat returned HTTP " + status);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate create chat failed");
            Replace(Current with
            {
                Notice = "Couldn't start that Pearlgate chat.",
                Generation = NextGeneration(),
            });
        }
        finally
        {
            lock (gate)
            {
                creatingChats.Remove(userId);
            }
        }
    }

    private async Task<(ConversationDto? Body, int Status)> PostCreateChatAsync(string userId, CancellationToken token)
    {
        var both = GateClient.JsonBody(new CreateChatDto(userId, userId), GateJson.Default.CreateChatDto);
        var first = await client.PostAsync("/chats/", both, GateJson.Default.ConversationDto, token)
            .ConfigureAwait(false);
        if (first.Status != 400)
        {
            return first;
        }

        var byUser = GateClient.JsonBody(new CreateChatDto(userId), GateJson.Default.CreateChatDto);
        var second = await client.PostAsync("/chats/", byUser, GateJson.Default.ConversationDto, token)
            .ConfigureAwait(false);
        if (second.Status != 400)
        {
            return second;
        }

        var byOther = GateClient.JsonBody(new CreateChatDto(null, userId), GateJson.Default.CreateChatDto);
        return await client.PostAsync("/chats/", byOther, GateJson.Default.ConversationDto, token)
            .ConfigureAwait(false);
    }

    private void UpsertChat(PearlChat chat, string userId)
    {
        var current = Current.Chats;
        var next = new List<PearlChat>(current.Length + 1);
        var replaced = false;
        for (var index = 0; index < current.Length; index++)
        {
            var row = current[index];
            if (string.Equals(row.Id, chat.Id, StringComparison.Ordinal) ||
                (!row.IsGroup && string.Equals(row.OtherUserId, userId, StringComparison.Ordinal)))
            {
                if (!replaced)
                {
                    next.Add(FillChat(chat, userId));
                    replaced = true;
                }

                continue;
            }

            next.Add(row);
        }

        if (!replaced)
        {
            next.Add(FillChat(chat, userId));
        }

        Replace(Current with { Chats = next.ToArray(), Generation = NextGeneration() });
    }

    private PearlChat FillChat(PearlChat chat, string userId)
    {
        var title = chat.Title;
        if (string.IsNullOrWhiteSpace(title) || title == "Chat")
        {
            var named = TitleForUser(userId);
            if (named.Length > 0)
            {
                title = named;
            }
        }

        var other = chat.OtherUserId.Length > 0 ? chat.OtherUserId : userId;
        return chat with { Title = title, OtherUserId = other };
    }

    private string TitleForUser(string userId)
    {
        var snapshot = Current;
        for (var index = 0; index < snapshot.People.Length; index++)
        {
            if (string.Equals(snapshot.People[index].Id, userId, StringComparison.Ordinal))
            {
                return snapshot.People[index].DisplayName;
            }
        }

        for (var index = 0; index < snapshot.Directory.Length; index++)
        {
            if (string.Equals(snapshot.Directory[index].Id, userId, StringComparison.Ordinal))
            {
                return snapshot.Directory[index].DisplayName;
            }
        }

        for (var index = 0; index < snapshot.SearchHits.Length; index++)
        {
            if (string.Equals(snapshot.SearchHits[index].Id, userId, StringComparison.Ordinal))
            {
                return snapshot.SearchHits[index].Title;
            }
        }

        return string.Empty;
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

            for (var index = 0; index < lines.Count; index++)
            {
                var existing = lines[index];
                if (existing.Mine && string.Equals(existing.Body, line.Body, StringComparison.Ordinal))
                {
                    return;
                }
            }

            lines.Add(line);
            generation = NextGeneration();
        }
    }

    private static List<PearlChatLine> MapChatLines(ChatMessagePageDto page, string meId)
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

            var mine = item.Mine ||
                       (meId.Length > 0 && string.Equals(item.SenderId, meId, StringComparison.Ordinal));
            var author = item.AuthorDisplayName ?? item.SenderDisplayName ??
                         (mine ? "You" : "Them");
            var when = item.CreatedAtUnix > 0
                ? DateTimeOffset.FromUnixTimeSeconds(item.CreatedAtUnix).ToLocalTime().ToString("HH:mm")
                : "now";
            mapped.Add(new PearlChatLine(mine, body, when, author, item.Id ?? string.Empty));
        }

        return mapped;
    }

    private List<PearlChatLine> MergePending(string chatId, List<PearlChatLine> fetched)
    {
        if (!chatLines.TryGetValue(chatId, out var prior) || prior.Count == 0)
        {
            return fetched;
        }

        for (var index = 0; index < prior.Count; index++)
        {
            var pending = prior[index];
            if (pending.Id.Length > 0)
            {
                continue;
            }

            var echoed = false;
            for (var inner = 0; inner < fetched.Count; inner++)
            {
                var row = fetched[inner];
                if (pending.Mine && string.Equals(row.Body, pending.Body, StringComparison.Ordinal))
                {
                    echoed = true;
                    break;
                }
            }

            if (!echoed)
            {
                fetched.Add(pending);
            }
        }

        return fetched;
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
                item.Race ?? string.Empty, item.World ?? string.Empty, item.TimeZoneId ?? string.Empty);
        }

        return mapped;
    }

    private static PearlPerson[] MapDirectory(GateUserDto[]? users, string meId)
    {
        if (users is null || users.Length == 0)
        {
            return [];
        }

        var mapped = new List<PearlPerson>(users.Length);
        for (var index = 0; index < users.Length; index++)
        {
            var user = users[index];
            var id = user.Id ?? string.Empty;
            if (id.Length == 0 || string.Equals(id, meId, StringComparison.Ordinal))
            {
                continue;
            }

            mapped.Add(new PearlPerson(id, Display(user.DisplayName, user.Name), user.Handle ?? string.Empty,
                string.Empty, false, user.AvatarUrl ?? string.Empty, string.Empty, user.World ?? string.Empty,
                user.TimeZoneId ?? string.Empty));
        }

        return mapped.ToArray();
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

    private static PearlStaffNotice[] MapStaffNotices(AccountNoticeDto[]? items)
    {
        if (items is null || items.Length == 0)
        {
            return [];
        }

        var mapped = new List<PearlStaffNotice>(items.Length);
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                continue;
            }

            mapped.Add(new PearlStaffNotice(
                item.Id,
                item.Kind ?? "",
                item.Title ?? "Notice",
                item.Body ?? "",
                item.CreatedAtUnix,
                item.Read));
        }

        return mapped.ToArray();
    }

    private async Task RunStaffNoticesAsync(CancellationToken token)
    {
        if (!Current.SignedIn)
        {
            return;
        }

        try
        {
            await ApplyStaffStateAsync(null, Current.Banned, token).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Staff notice poll failed");
        }
    }

    private async Task ApplyStaffStateAsync(GateUserDto? me, bool treatForbiddenAsBan, CancellationToken token)
    {
        var (noticesPage, noticesStatus) = await client
            .GetAsync("/account/notices", GateJson.Default.AccountNoticePageDto, token)
            .ConfigureAwait(false);
        var meId = me?.Id ?? Current.MeId;
        var (bans, bansStatus) = meId.Length == 0
            ? (null, 0)
            : await client.GetAsync("/bans/" + Uri.EscapeDataString(meId), GateJson.Default.BanSnapshotDto, token)
                .ConfigureAwait(false);
        var fromMe = MapStaffNotices(me?.StaffNotices);
        var fromPage = noticesStatus is >= 200 and < 300 ? MapStaffNotices(noticesPage?.Items) : [];
        var notices = KeepLocalStaffReads(fromPage.Length > 0 || noticesStatus is >= 200 and < 300
            ? fromPage
            : fromMe.Length > 0
                ? fromMe
                : Current.StaffNotices);
        var banned = bansStatus is >= 200 and < 300
            ? bans?.Banned == true
            : treatForbiddenAsBan || me?.Banned == true || Current.Banned;
        var muted = bansStatus is >= 200 and < 300 ? bans?.Muted == true : me?.Muted == true || Current.Muted;
        var muteUntil = bansStatus is >= 200 and < 300
            ? bans?.MuteUntilUnix ?? 0
            : me?.MuteUntilUnix ?? Current.MuteUntilUnix;
        var notice = banned
            ? (me?.BanReason is { Length: > 0 } reason ? reason : (bans?.BanReason ?? "This account is suspended."))
            : muted
                ? "Staff muted this handset."
                : Current.Notice;
        Replace(Current with
        {
            SignedIn = true,
            Busy = false,
            GateLive = true,
            Notice = notice,
            StaffNotices = notices,
            Banned = banned,
            Muted = muted,
            MuteUntilUnix = muteUntil,
            Generation = NextGeneration(),
        });
    }

    private PearlStaffNotice[] KeepLocalStaffReads(PearlStaffNotice[] incoming)
    {
        HashSet<string> held;
        lock (gate)
        {
            held = new HashSet<string>(StringComparer.Ordinal);
            var prior = snapshot.StaffNotices;
            for (var index = 0; index < prior.Length; index++)
            {
                if (prior[index].Read && prior[index].Id.Length > 0)
                {
                    held.Add(prior[index].Id);
                }
            }

            foreach (var id in staffReads)
            {
                if (id.Length > 0)
                {
                    held.Add(id);
                }
            }
        }

        if (held.Count == 0 || incoming.Length == 0)
        {
            return incoming;
        }

        var next = new PearlStaffNotice[incoming.Length];
        for (var index = 0; index < incoming.Length; index++)
        {
            var row = incoming[index];
            next[index] = !row.Read && held.Contains(row.Id) ? row with { Read = true } : row;
        }

        return next;
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

    private async Task RunStaffReadAsync(string noticeId, CancellationToken token)
    {
        try
        {
            var status = await client
                .PostAsync("/account/notices/" + Uri.EscapeDataString(noticeId) + "/read", null, token)
                .ConfigureAwait(false);
            if (status is >= 200 and < 300 || status == 404)
            {
                return;
            }

            log.Write(LogSeverity.Warning, "Pearlgate staff notice read returned HTTP " + status);
            RetryStaffRead(noticeId);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate staff notice read failed");
            RetryStaffRead(noticeId);
        }
    }

    private void RetryStaffRead(string noticeId)
    {
        lock (gate)
        {
            foreach (var queued in staffReads)
            {
                if (string.Equals(queued, noticeId, StringComparison.Ordinal))
                {
                    return;
                }
            }

            staffReads.Enqueue(noticeId);
        }
    }

    private async Task RunReportAsync(string targetType, string targetId, string reason, string detail,
        PearlReportLine[] lines, CancellationToken token)
    {
        try
        {
            RevealedChatLineDto[]? revealed = null;
            if (lines.Length > 0)
            {
                revealed = new RevealedChatLineDto[lines.Length];
                for (var index = 0; index < lines.Length; index++)
                {
                    var line = lines[index];
                    revealed[index] = new RevealedChatLineDto(
                        line.Id.Length > 0 ? line.Id : (index + 1).ToString(CultureInfo.InvariantCulture),
                        line.Body);
                }
            }

            var body = new ReportBodyDto(targetType, targetId, reason, detail.Length == 0 ? null : detail, revealed);
            var status = await client
                .PostAsync("/reports", GateClient.JsonBody(body, GateJson.Default.ReportBodyDto), token)
                .ConfigureAwait(false);
            if (status == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            if (status is >= 200 and < 300)
            {
                return;
            }

            Replace(Current with
            {
                Notice = "Could not send that report (" + status + ").",
                Generation = NextGeneration(),
            });
            log.Write(LogSeverity.Warning, "Pearlgate report returned HTTP " + status);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate report failed");
            Replace(Current with
            {
                Notice = "Could not reach Pearlgate to send that report.",
                Generation = NextGeneration(),
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
        "denied" => "XIVAuth was cancelled. Try sign-in again.",
        "expired" => "That XIVAuth sign-in expired. Try again.",
        "character_mismatch" => "XIVAuth confirmed a different character than the one logged in. Pick the character you are playing.",
        "code_not_found" => "That sign-in code expired. Try again.",
        "xivauth_unconfigured" => "XIVAuth is not configured on the server yet.",
        _ when reason.Contains(' ', StringComparison.Ordinal) => reason,
        _ => "Pearlgate refused sign-in (" + reason + ").",
    };
}
