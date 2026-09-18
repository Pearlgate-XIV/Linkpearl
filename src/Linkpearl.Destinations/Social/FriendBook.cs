using Linkpearl.Diagnostics;
using Linkpearl.Modules;
using Linkpearl.Persistence;
using Linkpearl.Platform;

namespace Linkpearl.Destinations.Social;

internal sealed class FriendBook
{
    private readonly string path;
    private readonly ILinkpearlLog log;
    private readonly HashSet<string> stars = new(StringComparer.OrdinalIgnoreCase);
    private JsonCorruptHold corrupt;

    public FriendBook(HostPaths paths, ILinkpearlLog log)
    {
        this.log = log;
        path = paths.State("friends.json");
        Load();
    }

    public bool OnlineFirst { get; private set; } = true;

    public void SetOnlineFirst(bool value)
    {
        if (OnlineFirst == value)
        {
            return;
        }

        OnlineFirst = value;
        Save();
    }

    public bool IsStarred(GameFriend friend) => stars.Contains(Key(friend));

    public void ToggleStar(GameFriend friend)
    {
        var key = Key(friend);
        if (!stars.Add(key))
        {
            stars.Remove(key);
        }

        Save();
    }

    private void Load()
    {
        if (!AtomicJson.TryRead(path, null, out BookSave? dto, ref corrupt, log) || dto is null)
        {
            return;
        }

        OnlineFirst = dto.OnlineFirst ?? true;
        if (dto.Stars is null)
        {
            return;
        }

        for (var index = 0; index < dto.Stars.Length; index++)
        {
            var key = dto.Stars[index];
            if (!string.IsNullOrWhiteSpace(key))
            {
                stars.Add(key.Trim());
            }
        }
    }

    private void Save()
    {
        var keys = new string[stars.Count];
        stars.CopyTo(keys);
        AtomicJson.TrySave(path, new BookSave
        {
            OnlineFirst = OnlineFirst,
            Stars = keys,
        }, null, ref corrupt, log);
    }

    private static string Key(GameFriend friend)
    {
        var name = friend.Name.Trim();
        var world = friend.World.Trim();
        return world.Length > 0 ? name + "@" + world : name;
    }

    private sealed class BookSave
    {
        public bool? OnlineFirst { get; set; }

        public string[]? Stars { get; set; }
    }
}
