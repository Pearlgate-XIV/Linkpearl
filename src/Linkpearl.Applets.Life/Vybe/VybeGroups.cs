using System.Globalization;
using System.IO;
using Linkpearl.Modules;
using Linkpearl.Net;

namespace Linkpearl.Applets.Life.Vybe;

internal enum GroupLane : byte
{
    Mine = 0,
    Suggested = 1,
    Nearby = 2,
    All = 3,
    Plus = 4,
}

internal readonly record struct SceneGroup(
    string Id,
    string Name,
    string Tag,
    int Members,
    string File,
    bool Suggested,
    bool Nearby,
    bool PlusOnly = false);

internal static class VybeGroups
{
    public static readonly string[] Lanes = { "My Groups", "Suggested", "Nearby", "All" };

    public static string[] LanesOf(bool night) =>
        night ? ["My Groups", "Suggested", "Nearby", "All", "Plus"] : Lanes;

    public static readonly SceneGroup[] Catalog =
    {
        new("travel", "Travel Buddies", "Travel", 12_400, "city.png", true, true),
        new("fitness", "Fitness & Health", "Fitness", 8_100, "pose.png", true, false),
        new("food", "Food Lovers", "Food", 22_300, "sunset.png", true, true),
        new("music", "Music & Events", "Music", 11_900, "club-a.png", true, true),
        new("creative", "Creative Minds", "Creative", 6_700, "echo.png", true, false),
        new("pets", "Pet Parents", "Pets", 14_200, "luna.png", true, false),
        new("gaming", "Gaming Central", "Gaming", 9_800, "raven.png", true, true),
        new("ward", "Ward Walkers", "Travel", 3_400, "fc.png", false, true),
        new("club", "After Hours", "Music", 18_600, "club-b.png", false, true),
        new("photo", "Lens Club", "Creative", 5_200, "pose.png", false, false),
        new("lounge", "After Dark Lounge", "Plus", 4_800, "club-c.png", true, true, true),
        new("unfiltered", "Unfiltered GPose", "Plus", 2_100, "pose.png", true, false, true),
        new("pluscircle", "Plus Circle", "Plus", 3_600, "city.png", false, true, true),
    };

    public static string Face(HostPaths paths, string file) =>
        paths.Asset(Path.Combine("Icons", "vybe-demo", file));

    public static PearlPost[] Posts(HostPaths paths) => [];

    private static PearlPost[] DemoPosts(HostPaths paths) =>
    [
        Group(paths, "food", "1", "demo:luna", "Luna", "sunset.png",
            "Midnight noodles after the market. Who's grabbing a bowl? #Food", "3h", true, 64, 11, 6),
        Group(paths, "music", "1", "demo:raven", "Raven", "club-a.png",
            "After Hours set starts at 11. Rail spots going fast. #Music", "5h", false, 91, 18, 8),
        Group(paths, "travel", "1", "demo:sol", "Sol", "city.png",
            "Rooftop route tonight if the weather holds. Meet at the aetheryte. #Travel", "8h", false, 37, 7, 3),
        Group(paths, "gaming", "1", "demo:jett", "Jett", "raven.png",
            "Need two more for a late savage reclear. Voice on. #Gaming", "12h", true, 22, 14, 2),
        Group(paths, "creative", "1", "demo:vex", "Vex", "pose.png",
            "GPose dump from the pink neon rooftop. Come steal the lighting. #Creative", "1d", false, 58, 9, 5),
        Group(paths, "club", "1", "demo:novale", "NoVale", "club-b.png",
            "Dance floor is packed and the lights are mean tonight. #Music", "1d", true, 120, 21, 15),
        PlusGroup(paths, "lounge", "1", "demo:velvet", "Velvet", "club-c.png",
            "Plus lounge is open. Keep this off the SFW board. #VYBEPlus", "40m", true, 88, 16, 9),
        PlusGroup(paths, "unfiltered", "1", "demo:hex", "Hex", "pose.png",
            "Unfiltered set. 18+ members only. #NSFW #VYBEPlus", "2h", false, 54, 12, 4),
    ];

    public static bool TryGroup(PearlPost post, out SceneGroup group)
    {
        group = default;
        if (!post.AuthorHandle.StartsWith("group:", StringComparison.Ordinal))
        {
            return false;
        }

        var id = post.AuthorHandle[6..];
        for (var index = 0; index < Catalog.Length; index++)
        {
            if (!string.Equals(Catalog[index].Id, id, StringComparison.Ordinal))
            {
                continue;
            }

            group = Catalog[index];
            return true;
        }

        return false;
    }

    public static bool TryGroup(PearlPost post, VybeState state, out SceneGroup group)
    {
        if (TryGroup(post, out group))
        {
            return true;
        }

        if (!post.AuthorHandle.StartsWith("group:", StringComparison.Ordinal))
        {
            return false;
        }

        var found = VybeClubs.SceneOf(state, post.AuthorHandle[6..]);
        if (found is not { } club)
        {
            return false;
        }

        group = club;
        return true;
    }

    private static PearlPost Group(HostPaths paths, string groupId, string key, string authorId, string author,
        string file, string body, string when, bool liked, int likes, int comments, int reposts)
    {
        var still = Face(paths, file);
        return new PearlPost("demo-group:" + groupId + ":" + key, authorId, author, "group:" + groupId, still, body,
            when, false, liked, likes, comments, reposts, false, string.Empty, string.Empty, string.Empty,
            [new PearlMedia("demo-group-media-" + groupId + "-" + key, still, 960, 540)]);
    }

    private static PearlPost PlusGroup(HostPaths paths, string groupId, string key, string authorId, string author,
        string file, string body, string when, bool liked, int likes, int comments, int reposts)
    {
        var still = Face(paths, file);
        return new PearlPost("plus-group:" + groupId + ":" + key, authorId, author, "group:" + groupId, still, body,
            when, false, liked, likes, comments, reposts, false, string.Empty, string.Empty, string.Empty,
            [new PearlMedia("plus-group-media-" + groupId + "-" + key, still, 960, 540)]);
    }

    public static string Crowd(int members)
    {
        if (members >= 10_000)
        {
            return (members / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + "K members";
        }

        if (members >= 1000)
        {
            return (members / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + "K members";
        }

        return members.ToString(CultureInfo.InvariantCulture) + " members";
    }

    public static IReadOnlyList<SceneGroup> Shown(VybeState state)
    {
        var hits = new List<SceneGroup>();
        var needle = state.Search.Trim().Length > 0 ? state.Search.Trim() : state.GroupQuery.Trim();
        var find = state.PeopleFind;
        for (var index = 0; index < Catalog.Length; index++)
        {
            var group = Catalog[index];
            if (!Fits(state, group, owned: false) || !PeopleFindBook.FitsGroup(group, find, needle))
            {
                continue;
            }

            hits.Add(group);
        }

        for (var index = 0; index < state.Clubs.Count; index++)
        {
            var group = VybeClubs.Scene(state.Clubs[index]);
            if (!Fits(state, group, owned: true) || !PeopleFindBook.FitsGroup(group, find, needle))
            {
                continue;
            }

            hits.Add(group);
        }

        return hits;
    }

    public static bool Joined(VybeState state, string id) => state.JoinedGroups.Contains(id);

    public static void Toggle(VybeState state, string id, HostPaths paths)
    {
        if (!state.JoinedGroups.Add(id))
        {
            state.JoinedGroups.Remove(id);
        }

        state.Save(paths);
    }

    public static void Seed(VybeState state)
    {
        if (state.JoinedGroups.Count > 0)
        {
            return;
        }

        state.JoinedGroups.Add("food");
    }

    private static bool Fits(VybeState state, SceneGroup group, bool owned)
    {
        if (group.PlusOnly)
        {
            return state.Night && (state.GroupLane == GroupLane.Plus ||
                                   (state.GroupLane == GroupLane.Mine && (owned || Joined(state, group.Id))));
        }

        if (state.GroupLane == GroupLane.Plus)
        {
            return false;
        }

        return state.GroupLane switch
        {
            GroupLane.Mine => owned || Joined(state, group.Id),
            GroupLane.Suggested => group.Suggested && !owned,
            GroupLane.Nearby => group.Nearby,
            _ => true,
        };
    }
}
