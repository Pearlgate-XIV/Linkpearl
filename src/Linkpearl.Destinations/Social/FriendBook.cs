using Linkpearl.Diagnostics;
using Linkpearl.Modules;
using Linkpearl.Persistence;
using Linkpearl.Platform;

namespace Linkpearl.Destinations.Social;

public sealed class FriendBook
{
    private readonly HostPaths paths;
    private readonly ILinkpearlLog log;
    private readonly HashSet<string> stars = new(StringComparer.OrdinalIgnoreCase);
    private JsonCorruptHold corrupt;
    private string path = string.Empty;
    private ulong bound;
    private bool writeThrough;

    public FriendBook(HostPaths paths, ILinkpearlLog log)
    {
        this.paths = paths;
        this.log = log;
    }

    public ulong BoundId => bound;

    public bool OnlineFirst { get; private set; } = true;

    public void SetOnlineFirst(bool value)
    {
        if (OnlineFirst == value)
        {
            return;
        }

        OnlineFirst = value;
        writeThrough = true;
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

        writeThrough = true;
        Save();
    }

    public bool Flush()
    {
        if (path.Length == 0 || !writeThrough)
        {
            return true;
        }

        return Save();
    }

    public bool Bind(ulong contentId)
    {
        if (contentId == bound)
        {
            return true;
        }

        if (!Flush())
        {
            return false;
        }

        stars.Clear();
        OnlineFirst = true;
        corrupt = default;
        writeThrough = false;
        bound = contentId;
        path = CharacterStatePaths.Friends(paths, contentId);
        if (path.Length == 0)
        {
            return true;
        }

        writeThrough = File.Exists(path);
        if (writeThrough)
        {
            Load();
        }

        return true;
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

    private bool Save()
    {
        if (path.Length == 0 || !writeThrough)
        {
            return true;
        }

        var keys = new string[stars.Count];
        stars.CopyTo(keys);
        return AtomicJson.TrySave(path, new BookSave
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
