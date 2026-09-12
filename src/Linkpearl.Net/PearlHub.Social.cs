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
}

internal readonly record struct SocialWrite(
    SocialKind Kind,
    string Id,
    string Body,
    bool Everyone,
    string[] MediaPaths,
    string QuoteOf);

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

    public void PublishPost(string body, bool everyone, IReadOnlyList<string> mediaPaths, string quoteOf)
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

        Enqueue(new SocialWrite(SocialKind.Publish, string.Empty, text, everyone, files, quote));
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

    public void SetAvatar(string mediaPath)
    {
        if (!File.Exists(mediaPath) || !Current.SignedIn)
        {
            return;
        }

        if (BlockMuted())
        {
            return;
        }

        Enqueue(new SocialWrite(SocialKind.Avatar, string.Empty, string.Empty, true, [mediaPath], string.Empty));
    }

    public void PublishStory(string body, string mediaPath)
    {
        if (!Current.SignedIn)
        {
            return;
        }

        if (BlockMuted())
        {
            return;
        }

        var files = File.Exists(mediaPath) ? new[] { mediaPath } : [];
        Enqueue(new SocialWrite(SocialKind.Story, string.Empty, body.Trim(), true, files, string.Empty));
    }

    private bool BlockMuted()
    {
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
        if (key.Length == 0)
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
                .GetAsync("/feed?tab=" + Uri.EscapeDataString(tab), GateJson.Default.PostPageDto, token)
                .ConfigureAwait(false);
            if (status == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            var posts = status is >= 200 and < 300 ? MapPosts(page?.Items) : [];
            RememberPosts(posts);
            Replace(Current with
            {
                Feed = posts,
                FeedTab = tab,
                FeedLive = status is >= 200 and < 300,
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
                ? "/users/me/posts"
                : "/users/" + Uri.EscapeDataString(userId) + "/posts";
            var (page, status) = await client.GetAsync(path, GateJson.Default.PostPageDto, token)
                .ConfigureAwait(false);
            if (status == 401)
            {
                DropSession("Session expired. Sign in again.");
                return;
            }

            var posts = status is >= 200 and < 300 ? MapPosts(page?.Items) : [];
            RememberPosts(posts);
            Replace(Current with
            {
                ProfilePosts = posts,
                WatchedUserId = userId,
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
            if (uploaded is { Length: > 0 })
            {
                ids.Add(uploaded);
            }
        }

        var body = new PostBodyDto(write.Body.Length == 0 ? null : write.Body,
            write.Everyone ? "everyone" : "following", ids.Count == 0 ? null : ids.ToArray(),
            write.QuoteOf.Length == 0 ? null : write.QuoteOf);
        return await client.PostAsync("/posts", GateClient.JsonBody(body, GateJson.Default.PostBodyDto), token)
            .ConfigureAwait(false);
    }

    private async Task<int> RunStoryWriteAsync(SocialWrite write, CancellationToken token)
    {
        string? mediaId = null;
        if (write.MediaPaths.Length > 0)
        {
            mediaId = await UploadMediaAsync(write.MediaPaths[0], token).ConfigureAwait(false);
        }

        var body = new StoryBodyDto(write.Body.Length == 0 ? null : write.Body, mediaId);
        return await client.PostAsync("/stories", GateClient.JsonBody(body, GateJson.Default.StoryBodyDto), token)
            .ConfigureAwait(false);
    }

    private async Task<int> RunAvatarWriteAsync(SocialWrite write, CancellationToken token)
    {
        if (write.MediaPaths.Length == 0)
        {
            return 400;
        }

        var mediaId = await UploadMediaAsync(write.MediaPaths[0], token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return 404;
        }

        return await client.PostAsync("/me/avatar",
                GateClient.JsonBody(new AvatarBodyDto(mediaId), GateJson.Default.AvatarBodyDto), token)
            .ConfigureAwait(false);
    }

    private async Task<string?> UploadMediaAsync(string path, CancellationToken token)
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

        return reply?.Id;
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
            item.QuoteBody ?? string.Empty, MapMedia(item.Media));
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
}
