using Linkpearl.Diagnostics;

namespace Linkpearl.Net;

internal enum SocialKind : byte
{
    Like = 0,
    Unlike = 1,
    Follow = 2,
    Unfollow = 3,
    Comment = 4,
    Repost = 5,
    Publish = 6,
    Story = 7,
    Avatar = 8,
    Banner = 9,
}

internal readonly record struct SocialWrite(
    SocialKind Kind,
    string Id,
    string Body,
    bool Everyone,
    string[] MediaPaths,
    string QuoteOf,
    bool Plus = false);

public sealed partial class PearlHub
{
    public void WatchFeed(string tab)
    {
        var next = string.Equals(tab, "following", StringComparison.OrdinalIgnoreCase) ? "following" : "foryou";
        if (!string.Equals(feedTab, next, StringComparison.Ordinal))
        {
            feedTab = next;
            Replace(Current with { FeedTab = next, Generation = NextGeneration() });
        }

        feedQueued = true;
    }

    public void WatchPost(string postId)
    {
        var id = postId.Trim();
        if (id.Length == 0)
        {
            return;
        }

        watchedPost = id;
        postQueued = true;
    }

    public void WatchProfile(string userId)
    {
        var id = userId.Trim();
        if (id.Length == 0)
        {
            return;
        }

        watchedUser = id;
        Replace(Current with { WatchedUserId = id, Generation = NextGeneration() });
        profileQueued = true;
    }

    public void PublishPost(string body, bool everyone, IReadOnlyList<string> mediaPaths, string quoteOf, bool plus = false)
    {
        var text = body.Trim();
        var quote = quoteOf.Trim();
        var files = mediaPaths.Where(File.Exists).Take(4).ToArray();
        if ((text.Length == 0 && files.Length == 0 && quote.Length == 0) || !Current.SignedIn)
        {
            return;
        }

        if (BlockMuted())
        {
            return;
        }

        Enqueue(new SocialWrite(SocialKind.Publish, string.Empty, text, everyone, files, quote, plus));
    }

    public void LikePost(string postId, bool liked)
    {
        var id = postId.Trim();
        if (id.Length == 0 || !Current.SignedIn)
        {
            return;
        }

        if (BlockMuted())
        {
            return;
        }

        Enqueue(new SocialWrite(liked ? SocialKind.Like : SocialKind.Unlike, id, string.Empty, true, [],
            string.Empty));
    }

    public void CommentOn(string postId, string body)
    {
        var id = postId.Trim();
        var text = body.Trim();
        if (id.Length == 0 || text.Length == 0 || !Current.SignedIn)
        {
            return;
        }

        if (BlockMuted())
        {
            return;
        }

        Enqueue(new SocialWrite(SocialKind.Comment, id, text, true, [], string.Empty));
    }

    public void Repost(string postId)
    {
        var id = postId.Trim();
        if (id.Length == 0 || !Current.SignedIn)
        {
            return;
        }

        if (BlockMuted())
        {
            return;
        }

        Enqueue(new SocialWrite(SocialKind.Repost, id, string.Empty, true, [], string.Empty));
    }

    public void Follow(string userId, bool follow)
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

        Enqueue(new SocialWrite(follow ? SocialKind.Follow : SocialKind.Unfollow, id, string.Empty, true, [],
            string.Empty));
    }

    public void SetAvatar(string mediaPath) => QueueProfileStill(SocialKind.Avatar, mediaPath);

    public void SetBanner(string mediaPath) => QueueProfileStill(SocialKind.Banner, mediaPath);

    private void QueueProfileStill(SocialKind kind, string mediaPath)
    {
        if (!Current.SignedIn)
        {
            return;
        }

        if (BlockMuted())
        {
            return;
        }

        var path = mediaPath.Trim();
        if (path.Length > 0 && !File.Exists(path))
        {
            Replace(Current with
            {
                Notice = kind == SocialKind.Banner
                    ? "Could not save that banner."
                    : "Could not save that photo.",
                Generation = NextGeneration(),
            });
            return;
        }

        Enqueue(new SocialWrite(kind, string.Empty, string.Empty, true, path.Length > 0 ? [path] : [], string.Empty));
    }

    public void PublishStory(string body, string mediaPath)
    {
        if (!Current.SignedIn)
        {
            Replace(Current with
            {
                Notice = "Sign in from You to post a story.",
                Generation = NextGeneration(),
            });
            return;
        }

        if (BlockMuted())
        {
            return;
        }

        var text = body.Trim();
        var files = File.Exists(mediaPath) ? new[] { mediaPath } : [];
        if (text.Length == 0 && files.Length == 0)
        {
            return;
        }

        Enqueue(new SocialWrite(SocialKind.Story, string.Empty, text, true, files, string.Empty));
    }

    public void WatchStory(string authorId)
    {
        var id = authorId.Trim();
        if (id.Length == 0 || !Current.SignedIn)
        {
            return;
        }

        watchedStory = id;
        storyFetchQueued = true;
    }

    private bool BlockMuted()
    {
        if (Current.Banned)
        {
            Replace(Current with
            {
                Notice = "This account is suspended.",
                Generation = NextGeneration(),
            });
            return true;
        }

        if (!Current.Muted)
        {
            return false;
        }

        Replace(Current with
        {
            Notice = "Staff muted this handset.",
            Generation = NextGeneration(),
        });
        return true;
    }

    public IReadOnlyList<PearlComment> CommentsFor(string postId)
    {
        lock (gate)
        {
            return postComments.TryGetValue(postId, out var lines) ? lines.ToArray() : [];
        }
    }

    public PearlPost? PostById(string postId)
    {
        lock (gate)
        {
            return postIndex.TryGetValue(postId, out var post) ? post : null;
        }
    }

    public void PrefetchMedia(string url)
    {
        var key = url.Trim();
        if (!GateMedia.IsRemote(key))
        {
            return;
        }

        lock (gate)
        {
            if (mediaPaths.ContainsKey(key) || mediaFailed.Contains(key) || mediaWanted.Contains(key))
            {
                return;
            }

            mediaWanted.Enqueue(key);
        }
    }

    public string? LocalMedia(string url)
    {
        lock (gate)
        {
            return mediaPaths.TryGetValue(url, out var path) ? path : null;
        }
    }

    private void Enqueue(SocialWrite write)
    {
        lock (gate)
        {
            writes.Enqueue(write);
        }
    }

    private async Task RunFeedAsync(string tab, CancellationToken token)
    {
        if (!Current.SignedIn)
        {
            return;
        }

        try
        {
            var (page, status) = await client
                .GetAsync("/vybe/feed?tab=" + Uri.EscapeDataString(tab), GateJson.Default.PostPageDto, token)
                .ConfigureAwait(false);
            if (status is < 200 or >= 300)
            {
                (page, status) = await client
                    .GetAsync("/feed?tab=" + Uri.EscapeDataString(tab), GateJson.Default.PostPageDto, token)
                    .ConfigureAwait(false);
            }
            if (status == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            var sfw = status is >= 200 and < 300 ? MapPosts(page?.Items) : [];
            var (plusLane, plusLive) = await LoadVybePlusFeedAsync(token).ConfigureAwait(false);
            var posts = MergePosts(plusLane, sfw);
            var live = plusLive || status is >= 200 and < 300;
            RememberPosts(posts);
            Replace(Current with
            {
                Feed = live ? posts : Current.Feed,
                FeedTab = tab,
                FeedLive = live,
                Generation = NextGeneration(),
            });
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate feed failed");
        }
    }

    private async Task RunPostAsync(string postId, CancellationToken token)
    {
        if (postId.Length == 0 || !Current.SignedIn)
        {
            return;
        }

        try
        {
            var (page, status) = await client
                .GetAsync("/posts/" + Uri.EscapeDataString(postId), GateJson.Default.CommentPageDto, token)
                .ConfigureAwait(false);
            if (status == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            if (status is < 200 or >= 300)
            {
                return;
            }

            var comments = MapComments(page?.Items);
            lock (gate)
            {
                postComments[postId] = comments;
                if (page?.Post is not null)
                {
                    var mapped = MapPost(page.Post);
                    if (mapped is { Id.Length: > 0 } post)
                    {
                        postIndex[post.Id] = post;
                    }
                }

                generation = NextGeneration();
            }

            Replace(Current with { WatchedPostId = postId, Generation = NextGeneration() });
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate post fetch failed");
        }
    }

    private async Task RunProfilePostsAsync(string userId, CancellationToken token)
    {
        if (userId.Length == 0 || !Current.SignedIn)
        {
            return;
        }

        try
        {
            var path = string.Equals(userId, "me", StringComparison.Ordinal)
                ? "/vybe/users/me/posts"
                : "/vybe/users/" + Uri.EscapeDataString(userId) + "/posts";
            var (page, status) = await client.GetAsync(path, GateJson.Default.PostPageDto, token)
                .ConfigureAwait(false);
            if (status is < 200 or >= 300)
            {
                path = string.Equals(userId, "me", StringComparison.Ordinal)
                    ? "/users/me/posts"
                    : "/users/" + Uri.EscapeDataString(userId) + "/posts";
                (page, status) = await client.GetAsync(path, GateJson.Default.PostPageDto, token)
                    .ConfigureAwait(false);
            }
            if (status == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            var sfw = status is >= 200 and < 300 ? MapPosts(page?.Items) : [];
            var vybeId = string.Equals(userId, "me", StringComparison.Ordinal) ? "me" : userId;
            var (plusLane, _) = await LoadVybePlusProfilePostsAsync(vybeId, token).ConfigureAwait(false);
            var posts = MergePosts(plusLane, sfw);
            RememberPosts(posts);
            var (avatarUrl, bannerUrl) = await LoadWatchedProfileMediaAsync(userId, token).ConfigureAwait(false);
            if (avatarUrl.Length > 0)
            {
                PrefetchMedia(avatarUrl);
            }

            if (bannerUrl.Length > 0)
            {
                PrefetchMedia(bannerUrl);
            }

            Replace(Current with
            {
                ProfilePosts = posts,
                WatchedUserId = userId,
                WatchedUserAvatarUrl = avatarUrl,
                WatchedUserBannerUrl = bannerUrl,
                Generation = NextGeneration(),
            });
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate profile posts failed");
        }
    }

    private async Task RunMediaAsync(string url, CancellationToken token)
    {
        try
        {
            var (bytes, status) = await client.GetBytesAsync(url, token).ConfigureAwait(false);
            if (status is < 200 or >= 300 || bytes is null || bytes.Length == 0)
            {
                lock (gate)
                {
                    mediaFailed.Add(url);
                }

                return;
            }

            var name = MediaFileName(url);
            var path = Path.Combine(mediaCache, name);
            await File.WriteAllBytesAsync(path, bytes, token).ConfigureAwait(false);
            lock (gate)
            {
                mediaPaths[url] = path;
                generation = NextGeneration();
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate media fetch failed");
            lock (gate)
            {
                mediaFailed.Add(url);
            }
        }
    }

    private async Task RunWriteAsync(SocialWrite write, CancellationToken token)
    {
        try
        {
            var status = write.Kind switch
            {
                SocialKind.Like => await client
                    .PostAsync("/posts/" + Uri.EscapeDataString(write.Id) + "/likes", null, token)
                    .ConfigureAwait(false),
                SocialKind.Unlike => await client
                    .DeleteAsync("/posts/" + Uri.EscapeDataString(write.Id) + "/likes", token)
                    .ConfigureAwait(false),
                SocialKind.Follow => await client
                    .PostAsync("/follows/" + Uri.EscapeDataString(write.Id), null, token)
                    .ConfigureAwait(false),
                SocialKind.Unfollow => await client
                    .DeleteAsync("/follows/" + Uri.EscapeDataString(write.Id), token)
                    .ConfigureAwait(false),
                SocialKind.Repost => await client
                    .PostAsync("/posts/" + Uri.EscapeDataString(write.Id) + "/reposts", null, token)
                    .ConfigureAwait(false),
                SocialKind.Comment => await client
                    .PostAsync("/posts/" + Uri.EscapeDataString(write.Id) + "/comments",
                        GateClient.JsonBody(new CommentBodyDto(write.Body), GateJson.Default.CommentBodyDto), token)
                    .ConfigureAwait(false),
                SocialKind.Publish => await RunPublishAsync(write, token).ConfigureAwait(false),
                SocialKind.Story => await RunStoryWriteAsync(write, token).ConfigureAwait(false),
                SocialKind.Avatar => await RunAvatarWriteAsync(write, token).ConfigureAwait(false),
                SocialKind.Banner => await RunBannerWriteAsync(write, token).ConfigureAwait(false),
                _ => 0,
            };

            if (status == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            if (status is >= 200 and < 300)
            {
                if (write.Kind is SocialKind.Comment)
                {
                    postQueued = true;
                }

                if (write.Kind is SocialKind.Publish or SocialKind.Repost or SocialKind.Like or SocialKind.Unlike)
                {
                    feedQueued = true;
                }

                QueueRefresh();
                return;
            }

            var muted = status == 403;
            var missing = status is 404 or 405 or 501;
            Replace(Current with
            {
                Notice = muted
                    ? "Staff muted this handset."
                    : missing
                        ? "Pearlgate does not host this yet (" + write.Kind + ")."
                        : "Pearlgate refused that (" + status + ").",
                Muted = muted || Current.Muted,
                Generation = NextGeneration(),
            });
            log.Write(LogSeverity.Warning, "Pearlgate " + write.Kind + " returned HTTP " + status);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate social write failed");
            Replace(Current with { Notice = "Could not reach Pearlgate for that.", Generation = NextGeneration() });
        }
    }

    private async Task<int> RunPublishAsync(SocialWrite write, CancellationToken token)
    {
        var ids = new List<string>();
        foreach (var path in write.MediaPaths)
        {
            var uploaded = await UploadMediaAsync(path, token).ConfigureAwait(false);
            if (uploaded is UploadedMedia media && media.Id.Length > 0)
            {
                ids.Add(media.Id);
            }
        }

        var body = new PostBodyDto(write.Body.Length == 0 ? null : write.Body,
            write.Everyone ? "everyone" : "following", ids.Count == 0 ? null : ids.ToArray(),
            write.QuoteOf.Length == 0 ? null : write.QuoteOf);
        if (write.Plus)
        {
            var plus = await client.PostAsync("/vybe-plus/posts",
                    GateClient.JsonBody(
                        new CreateVybePlusPostBodyDto(
                            ids.Count > 0 ? ids[0] : null,
                            0,
                            0,
                            write.Body ?? string.Empty,
                            [],
                            ids.Count == 0 ? null : ids.ToArray(),
                            write.Everyone ? 1 : 0,
                            1),
                        GateJson.Default.CreateVybePlusPostBodyDto), token)
                .ConfigureAwait(false);
            if (plus is >= 200 and < 300)
            {
                return plus;
            }

            // Leftover Gate path; product name is Vybe+.
            plus = await client.PostAsync("/afterdark/posts",
                    GateClient.JsonBody(
                        new CreateVybePlusPostBodyDto(
                            ids.Count > 0 ? ids[0] : null,
                            0,
                            0,
                            write.Body ?? string.Empty,
                            [],
                            ids.Count == 0 ? null : ids.ToArray(),
                            write.Everyone ? 1 : 0,
                            1),
                        GateJson.Default.CreateVybePlusPostBodyDto), token)
                .ConfigureAwait(false);
            return plus;
        }

        var vybe = await client.PostAsync("/vybe/posts", GateClient.JsonBody(body, GateJson.Default.PostBodyDto), token)
            .ConfigureAwait(false);
        if (vybe is >= 200 and < 300)
        {
            return vybe;
        }

        return await client.PostAsync("/posts", GateClient.JsonBody(body, GateJson.Default.PostBodyDto), token)
            .ConfigureAwait(false);
    }

    private async Task RunWatchStoryAsync(string authorId, CancellationToken token)
    {
        if (authorId.Length == 0 || !Current.SignedIn)
        {
            return;
        }

        try
        {
            var ring = await LoadStoryRingAsync(authorId, token).ConfigureAwait(false);
            if (ring is null)
            {
                return;
            }

            var mapped = MapRing(ring, Current.Stories);
            KeepStorySlides([mapped]);
            Replace(Current with
            {
                Stories = ReplaceRing(Current.Stories, mapped),
                StoriesLive = true,
                Generation = NextGeneration(),
            });
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.Write(LogSeverity.Warning, failure, "Pearlgate story watch failed");
        }
    }

    private async Task<StoryRingDto?> LoadStoryRingAsync(string authorId, CancellationToken token)
    {
        var escaped = Uri.EscapeDataString(authorId);
        var (ring, status) = await client
            .GetAsync("/stories/" + escaped, GateJson.Default.StoryRingDto, token)
            .ConfigureAwait(false);
        if (status == 401)
        {
            DropSession("Session expired. Sign in again.");
            return null;
        }

        if (status is >= 200 and < 300 && LooksLikeRing(ring))
        {
            return ring;
        }

        var trayPaths = new[]
        {
            "/stories/" + escaped,
            "/users/" + escaped + "/stories",
            "/stories?userId=" + escaped,
        };
        for (var index = 0; index < trayPaths.Length; index++)
        {
            var (tray, trayStatus) = await client
                .GetAsync(trayPaths[index], GateJson.Default.StoryTrayDto, token)
                .ConfigureAwait(false);
            if (trayStatus == 401)
            {
                DropSession("Session expired. Sign in again.");
                return null;
            }

            if (trayStatus is < 200 or >= 300)
            {
                continue;
            }

            var rings = MapStories(tray, Current.Stories);
            for (var ringIndex = 0; ringIndex < rings.Length; ringIndex++)
            {
                if (string.Equals(rings[ringIndex].AuthorId, authorId, StringComparison.Ordinal))
                {
                    return new StoryRingDto(rings[ringIndex].AuthorId, rings[ringIndex].AuthorName,
                        rings[ringIndex].HasUnseen, rings[ringIndex].Count, rings[ringIndex].AvatarUrl,
                        ToItems(rings[ringIndex].Items));
                }
            }

            if (rings.Length == 1)
            {
                return new StoryRingDto(rings[0].AuthorId, rings[0].AuthorName, rings[0].HasUnseen, rings[0].Count,
                    rings[0].AvatarUrl, ToItems(rings[0].Items));
            }
        }

        return null;
    }

    private static StoryItemDto[] ToItems(PearlStorySlide[] slides)
    {
        var items = new StoryItemDto[slides.Length];
        for (var index = 0; index < slides.Length; index++)
        {
            var slide = slides[index];
            items[index] = new StoryItemDto(slide.Id, null, null, slide.Body, null, null, null, slide.MediaUrl,
                null, null, null, slide.CreatedAtUnix);
        }

        return items;
    }

    private static PearlStory[] ReplaceRing(PearlStory[] rings, PearlStory next)
    {
        var found = false;
        var mapped = new PearlStory[rings.Length];
        for (var index = 0; index < rings.Length; index++)
        {
            if (string.Equals(rings[index].AuthorId, next.AuthorId, StringComparison.Ordinal))
            {
                mapped[index] = next;
                found = true;
            }
            else
            {
                mapped[index] = rings[index];
            }
        }

        if (found)
        {
            return mapped;
        }

        var grown = new PearlStory[rings.Length + 1];
        rings.CopyTo(grown, 0);
        grown[^1] = next;
        return grown;
    }

    private static bool LooksLikeRing(StoryRingDto? ring) =>
        ring is not null &&
        (ring.AuthorId is { Length: > 0 } || ring.Count > 0 || ring.Items is { Length: > 0 } ||
         ring.Stories is { Length: > 0 });

    private async Task<int> RunStoryWriteAsync(SocialWrite write, CancellationToken token)
    {
        string? mediaId = null;
        if (write.MediaPaths.Length > 0)
        {
            var uploaded = await UploadMediaAsync(write.MediaPaths[0], token).ConfigureAwait(false);
            mediaId = uploaded?.Id;
        }

        var body = new StoryBodyDto(write.Body.Length == 0 ? null : write.Body, mediaId);
        return await client.PostAsync("/stories", GateClient.JsonBody(body, GateJson.Default.StoryBodyDto), token)
            .ConfigureAwait(false);
    }

    private async Task<int> RunAvatarWriteAsync(SocialWrite write, CancellationToken token) =>
        await RunProfileStillWriteAsync(write, "/me/avatar", avatar: true, token).ConfigureAwait(false);

    private async Task<int> RunBannerWriteAsync(SocialWrite write, CancellationToken token) =>
        await RunProfileStillWriteAsync(write, "/me/banner", avatar: false, token).ConfigureAwait(false);

    private async Task<int> RunProfileStillWriteAsync(SocialWrite write, string slotPath, bool avatar,
        CancellationToken token)
    {
        if (write.MediaPaths.Length == 0)
        {
            return await PatchProfileMediaAsync(avatar, string.Empty, token).ConfigureAwait(false);
        }

        var uploaded = await UploadMediaAsync(write.MediaPaths[0], token).ConfigureAwait(false);
        if (uploaded is not UploadedMedia media)
        {
            return 404;
        }

        var (user, status) = await client.PostAsync(slotPath,
                GateClient.JsonBody(new AvatarBodyDto(media.Id), GateJson.Default.AvatarBodyDto),
                GateJson.Default.GateUserDto, token)
            .ConfigureAwait(false);
        if (status is >= 200 and < 300)
        {
            ApplyMeMedia(user, avatar, media.Url);
            return status;
        }

        if (status is 404 or 405 or 501 && media.Url.Length > 0)
        {
            return await PatchProfileMediaAsync(avatar, media.Url, token).ConfigureAwait(false);
        }

        return status;
    }

    private async Task<int> PatchProfileMediaAsync(bool avatar, string url, CancellationToken token)
    {
        var body = avatar
            ? new ProfilePatchDto(AvatarUrl: url)
            : new ProfilePatchDto(BannerUrl: url);
        var (user, status) = await client.PatchAsync("/me",
                GateClient.JsonBody(body, GateJson.Default.ProfilePatchDto), GateJson.Default.GateUserDto, token)
            .ConfigureAwait(false);
        if (status is >= 200 and < 300)
        {
            ApplyMeMedia(user, avatar, url);
        }

        return status;
    }

    private void ApplyMeMedia(GateUserDto? user, bool avatar, string fallbackUrl)
    {
        var url = avatar
            ? GateMedia.AvatarUrl(user, fallbackUrl)
            : GateMedia.BannerUrl(user, fallbackUrl);
        if (avatar)
        {
            Replace(Current with
            {
                MeAvatarUrl = url,
                WatchedUserAvatarUrl = WatchingMe() ? url : Current.WatchedUserAvatarUrl,
                Generation = NextGeneration(),
            });
            return;
        }

        Replace(Current with
        {
            MeBannerUrl = url,
            WatchedUserBannerUrl = WatchingMe() ? url : Current.WatchedUserBannerUrl,
            Generation = NextGeneration(),
        });
    }

    private bool WatchingMe() =>
        string.Equals(Current.WatchedUserId, "me", StringComparison.Ordinal) ||
        (Current.MeId.Length > 0 && string.Equals(Current.WatchedUserId, Current.MeId, StringComparison.Ordinal));

    private async Task<UploadedMedia?> UploadMediaAsync(string path, CancellationToken token)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }

        var name = Path.GetFileName(path);
        var type = MediaType(name);
        var (reply, status) = await client
            .PostFileAsync("/media", "file", name, bytes, type, GateJson.Default.MediaReplyDto, token)
            .ConfigureAwait(false);
        if (status is < 200 or >= 300)
        {
            return null;
        }

        var id = GateMedia.Id(reply);
        if (id.Length == 0)
        {
            return null;
        }

        return new UploadedMedia(id, GateMedia.PublicUrl(reply));
    }

    private void RememberPosts(PearlPost[] posts)
    {
        lock (gate)
        {
            foreach (var post in posts)
            {
                if (post.Id.Length > 0)
                {
                    postIndex[post.Id] = post;
                }
            }
        }
    }

    private static PearlPost[] MapPosts(PostDto[]? items)
    {
        if (items is null || items.Length == 0)
        {
            return [];
        }

        var mapped = new List<PearlPost>(items.Length);
        foreach (var item in items)
        {
            var post = MapPost(item);
            if (post is { Id.Length: > 0 } ready)
            {
                mapped.Add(ready);
            }
        }

        return mapped.ToArray();
    }

    private async Task<(PearlPost[] Posts, bool Live)> LoadVybePlusFeedAsync(CancellationToken token)
    {
        var (page, status) = await client
            .GetAsync("/vybe-plus/feed?scope=all", GateJson.Default.VybePlusFeedPageDto, token)
            .ConfigureAwait(false);
        if (status is < 200 or >= 300)
        {
            // Leftover Gate path; product name is Vybe+.
            (page, status) = await client
                .GetAsync("/afterdark/feed?scope=all", GateJson.Default.VybePlusFeedPageDto, token)
                .ConfigureAwait(false);
        }

        if (status is < 200 or >= 300)
        {
            return ([], false);
        }

        return (MapVybePlusPosts(page?.Items, Current.MeId), true);
    }

    private async Task<(PearlPost[] Posts, bool Live)> LoadVybePlusProfilePostsAsync(string userId, CancellationToken token)
    {
        var path = string.Equals(userId, "me", StringComparison.Ordinal)
            ? "/vybe-plus/users/me/posts"
            : "/vybe-plus/users/" + Uri.EscapeDataString(userId) + "/posts";
        var (page, status) = await client
            .GetAsync(path, GateJson.Default.VybePlusUserPostsPageDto, token)
            .ConfigureAwait(false);
        if (status is < 200 or >= 300)
        {
            // Leftover Gate path; product name is Vybe+.
            path = string.Equals(userId, "me", StringComparison.Ordinal)
                ? "/afterdark/users/me/posts"
                : "/afterdark/users/" + Uri.EscapeDataString(userId) + "/posts";
            (page, status) = await client
                .GetAsync(path, GateJson.Default.VybePlusUserPostsPageDto, token)
                .ConfigureAwait(false);
        }

        if (status is < 200 or >= 300)
        {
            return ([], false);
        }

        var meId = string.Equals(userId, "me", StringComparison.Ordinal) ? Current.MeId : userId;
        return (MapVybePlusPosts(page?.Items, meId), true);
    }

    private static PearlPost[] MergePosts(PearlPost[] first, PearlPost[] second)
    {
        if (first.Length == 0)
        {
            return second;
        }

        if (second.Length == 0)
        {
            return first;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var merged = new List<PearlPost>(first.Length + second.Length);
        foreach (var post in first)
        {
            if (post.Id.Length == 0 || !seen.Add(post.Id))
            {
                continue;
            }

            merged.Add(post);
        }

        foreach (var post in second)
        {
            if (post.Id.Length == 0 || !seen.Add(post.Id))
            {
                continue;
            }

            merged.Add(post);
        }

        return merged.ToArray();
    }

    private static PearlPost[] MapVybePlusPosts(VybePlusPostDto[]? items, string meId)
    {
        if (items is null || items.Length == 0)
        {
            return [];
        }

        var mapped = new List<PearlPost>(items.Length);
        foreach (var item in items)
        {
            var post = MapVybePlusPost(item, meId);
            if (post is { Id.Length: > 0 } ready)
            {
                mapped.Add(ready);
            }
        }

        return mapped.ToArray();
    }

    private static PearlPost? MapVybePlusPost(VybePlusPostDto item, string meId)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            return null;
        }

        var when = item.CreatedAtUnix > 0
            ? DateTimeOffset.FromUnixTimeSeconds(item.CreatedAtUnix).ToLocalTime().ToString("HH:mm")
            : "now";
        var owner = item.OwnerId ?? string.Empty;
        var urls = item.MediaUrls is { Length: > 0 } many
            ? many
            : string.IsNullOrWhiteSpace(item.MediaUrl) ? [] : new[] { item.MediaUrl };
        var media = new List<PearlMedia>(urls.Length);
        for (var index = 0; index < urls.Length; index++)
        {
            var url = urls[index];
            if (string.IsNullOrWhiteSpace(url) || url.TrimEnd('/').EndsWith("/media", StringComparison.Ordinal))
            {
                continue;
            }

            media.Add(new PearlMedia(item.MediaId ?? url, url, item.MediaWidth, item.MediaHeight));
        }

        var plus = item.Lane == 1;
        return new PearlPost(
            item.Id,
            owner,
            Display(item.OwnerDisplayName, "Someone"),
            item.OwnerHandle ?? string.Empty,
            item.OwnerAvatarUrl ?? string.Empty,
            item.Caption ?? string.Empty,
            when,
            meId.Length > 0 && string.Equals(owner, meId, StringComparison.Ordinal),
            item.MyReaction >= 0,
            item.TotalReactions,
            item.CommentCount,
            0,
            false,
            string.Empty,
            string.Empty,
            string.Empty,
            media.ToArray(),
            plus ? "vybe_plus" : "vybe",
            plus ? "mature" : "sfw",
            null,
            item.Tags);
    }

    private static PearlPost? MapPost(PostDto item)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            return null;
        }

        var when = item.CreatedAtUnix > 0
            ? DateTimeOffset.FromUnixTimeSeconds(item.CreatedAtUnix).ToLocalTime().ToString("HH:mm")
            : "now";
        return new PearlPost(item.Id, item.AuthorId ?? string.Empty,
            Display(item.AuthorDisplayName, "Someone"), item.AuthorHandle ?? string.Empty,
            item.AuthorAvatarUrl ?? string.Empty, item.Body ?? string.Empty, when, item.Mine, item.Liked, item.Likes,
            item.Comments, item.Reposts, item.Reposted, item.QuoteOf ?? string.Empty, item.QuoteAuthor ?? string.Empty,
            item.QuoteBody ?? string.Empty, MapMedia(item.Media), "vybe", "sfw");
    }

    private static PearlMedia[] MapMedia(MediaDto[]? items)
    {
        if (items is null || items.Length == 0)
        {
            return [];
        }

        var mapped = new List<PearlMedia>(items.Length);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Url) && string.IsNullOrWhiteSpace(item.Id))
            {
                continue;
            }

            mapped.Add(new PearlMedia(item.Id ?? string.Empty, item.Url ?? string.Empty, item.Width, item.Height));
        }

        return mapped.ToArray();
    }

    private static List<PearlComment> MapComments(CommentDto[]? items)
    {
        var mapped = new List<PearlComment>();
        if (items is null)
        {
            return mapped;
        }

        foreach (var item in items)
        {
            var body = item.Body ?? string.Empty;
            if (body.Length == 0)
            {
                continue;
            }

            var when = item.CreatedAtUnix > 0
                ? DateTimeOffset.FromUnixTimeSeconds(item.CreatedAtUnix).ToLocalTime().ToString("HH:mm")
                : "now";
            mapped.Add(new PearlComment(item.AuthorDisplayName ?? "Someone", body, when, item.Mine, item.Id ?? string.Empty));
        }

        return mapped;
    }

    private static PearlNote[] MapNotes(NoteDto[]? items)
    {
        if (items is null || items.Length == 0)
        {
            return [];
        }

        var mapped = new List<PearlNote>(items.Length);
        foreach (var item in items)
        {
            var when = item.CreatedAtUnix > 0
                ? DateTimeOffset.FromUnixTimeSeconds(item.CreatedAtUnix).ToLocalTime().ToString("HH:mm")
                : "now";
            mapped.Add(new PearlNote(item.Id ?? string.Empty, item.Kind ?? "note", item.ActorId ?? string.Empty,
                item.ActorName ?? "Someone", item.Line ?? string.Empty, item.PostId ?? string.Empty, when));
        }

        return mapped.ToArray();
    }

    private static string MediaFileName(string url)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in url)
            {
                hash = (hash ^ ch) * 16777619;
            }

            var ext = ".img";
            var cut = url.IndexOf('?', StringComparison.Ordinal);
            var path = cut >= 0 ? url[..cut] : url;
            var dot = path.LastIndexOf('.');
            if (dot >= 0 && path.Length - dot <= 5)
            {
                ext = path[dot..];
            }

            return hash.ToString("x8") + ext;
        }
    }

    private static string MediaType(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        if (ext.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return "image/webp";
        }

        if (ext.Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            return "image/gif";
        }

        return "image/jpeg";
    }

    private async Task<(string AvatarUrl, string BannerUrl)> LoadWatchedProfileMediaAsync(string userId,
        CancellationToken token)
    {
        if (string.Equals(userId, "me", StringComparison.Ordinal) ||
            string.Equals(userId, Current.MeId, StringComparison.Ordinal))
        {
            return (Current.MeAvatarUrl, Current.MeBannerUrl);
        }

        var (user, status) = await client
            .GetAsync("/users/" + Uri.EscapeDataString(userId), GateJson.Default.GateUserDto, token)
            .ConfigureAwait(false);
        if (status is < 200 or >= 300 || user is null)
        {
            return (string.Empty, string.Empty);
        }

        return (GateMedia.AvatarUrl(user), GateMedia.BannerUrl(user));
    }
}

internal readonly record struct UploadedMedia(string Id, string Url);
