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
}

internal readonly record struct SceneGroup(
    string Id,
    string Name,
    string Tag,
    int Members,
    string File,
    bool Suggested,
    bool Nearby);

internal static class VybeGroups
{
    public static readonly string[] Lanes = { "My Groups", "Suggested", "Nearby", "All" };

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
    };

    public static string Face(HostPaths paths, string file) =>
        paths.Asset(Path.Combine("Icons", "vybe-demo", file));

    public static PearlPost[] Posts(HostPaths paths) =>
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

    private static PearlPost Group(HostPaths paths, string groupId, string key, string authorId, string author,
        string file, string body, string when, bool liked, int likes, int comments, int reposts)
    {
        var still = Face(paths, file);
        return new PearlPost("demo-group:" + groupId + ":" + key, authorId, author, "group:" + groupId, still, body,
            when, false, liked, likes, comments, reposts, false, string.Empty, string.Empty, string.Empty,
            [new PearlMedia("demo-group-media-" + groupId + "-" + key, still, 960, 540)]);
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
        var needle = state.GroupQuery.Trim();
        for (var index = 0; index < Catalog.Length; index++)
        {
            var group = Catalog[index];
            if (!Fits(state, group))
            {
                continue;
            }

            if (needle.Length > 0 &&
                !group.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) &&
                !group.Tag.Contains(needle, StringComparison.OrdinalIgnoreCase))
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

    private static bool Fits(VybeState state, SceneGroup group) => state.GroupLane switch
    {
        GroupLane.Mine => Joined(state, group.Id),
        GroupLane.Suggested => group.Suggested,
        GroupLane.Nearby => group.Nearby,
        _ => true,
    };
}
