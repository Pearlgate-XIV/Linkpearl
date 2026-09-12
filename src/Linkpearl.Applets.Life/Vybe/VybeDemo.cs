using System;
using System.Collections.Generic;
using System.IO;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Vybe;

internal static class VybeDemo
{
    public const string Folder = "Icons/vybe-demo";

    public static void Seed(VybeState state, HostPaths paths, bool seedHearts, string homeWorld = "")
    {
        _ = paths;
        _ = seedHearts;
        _ = homeWorld;
        for (var index = state.Roster.Count - 1; index >= 0; index--)
        {
            if (!state.Roster[index].GateId.StartsWith("demo:", StringComparison.Ordinal))
            {
                continue;
            }

            var id = state.Roster[index].Id;
            state.Roster.RemoveAt(index);
            state.LiveStories.Remove(id);
            state.LikedPeople.Remove(id);
            state.Incoming.Remove(id);
            state.Connected.Remove(id);
        }
    }

    public static bool IsPlusPost(PearlPost post) => VybePostMark.IsPlus(post);
    public static ScenePerson[] People(HostPaths paths) => [];

    private static ScenePerson[] DemoPeople(HostPaths paths)
    {
        return
        [
            Person(paths, "luna", "Luna", "@luna_v", "Balmung", "Night markets and neon.", true, 0.72f, 0.28f, 0.46f,
                plusMember: true),
            Person(paths, "ace", "Ace", "@ace", "Crystal", "New track dropping soon.", true, 0.22f, 0.42f, 0.78f),
            Person(paths, "kairo", "Kairo", "@kairo", "Balmung", "Ward walks after midnight.", true, 0.38f, 0.22f, 0.72f,
                "ace"),
            Person(paths, "nyx", "Nyx", "@nyx_", "Mateus", "Quiet corners. Loud nights.", false, 0.64f, 0.20f, 0.52f,
                "raven"),
            Person(paths, "raven", "Raven", "@raven_", "Mateus", "Club lights, late hours.", true, 0.42f, 0.18f, 0.58f,
                plusMember: true),
            Person(paths, "vex", "Vex", "@vex_", "Gilgamesh", "GPose first, talk later.", false, 0.18f, 0.62f, 0.48f),
            Person(paths, "echo", "Echo", "@echo_", "Leviathan", "#Crystal #GoodVibes", true, 0.86f, 0.42f, 0.22f),
            Person(paths, "novale", "NoVale", "@novale", "Balmung", "Late nights. Better company.", true, 0.90f, 0.26f,
                0.40f, plusMember: true),
            Person(paths, "sol", "Sol", "@sol", "Jenova", "Sunset rooftops.", true, 0.94f, 0.48f, 0.22f, "luna"),
            Person(paths, "wren", "Wren", "@wren", "Siren", "FC house is open.", true, 0.28f, 0.58f, 0.46f, "echo"),
            Person(paths, "iris", "Iris", "@iris", "Phoenix", "Looking for a pose partner.", false, 0.78f, 0.32f,
                0.54f, "vex"),
            Person(paths, "jett", "Jett", "@jett", "Cerberus", "On the dance floor.", true, 0.20f, 0.34f, 0.82f,
                "novale"),
            Person(paths, "velvet", "Velvet", "@velvet", "Balmung", "18+ VYBE+. DMs open.", true, 0.82f, 0.18f,
                0.42f, "raven", true),
            Person(paths, "hex", "Hex", "@hex_", "Mateus", "Private sets. No screenshots.", true, 0.28f, 0.16f, 0.62f,
                "ace", true),
            Person(paths, "noir", "Noir", "@noir", "Gilgamesh", "Plus lounge regular.", false, 0.12f, 0.10f, 0.16f,
                "novale", true),
            Person(paths, "ember", "Ember", "@ember", "Jenova", "Unfiltered. Ask first.", true, 0.88f, 0.32f, 0.18f,
                "luna", true),
        ];
    }

    public static PearlPost[] Posts(HostPaths paths) => [];

    private static PearlPost[] DemoPosts(HostPaths paths)
    {
        var luna = Face(paths, "luna");
        var ace = Face(paths, "ace");
        var raven = Face(paths, "raven");
        var vex = Face(paths, "vex");
        var echo = Face(paths, "echo");
        var novale = Face(paths, "novale");
        return
        [
            Post("demo-post-novale", "demo:novale", "NoVale", "@novale", novale,
                "Late nights. Good music. Better company. Who's up? 👀", "2h", true, 158, 24, 12,
                Shot(paths, "city", 960, 540)),
            Post("demo-post-raven", "demo:raven", "Raven", "@raven_", raven,
                "That set last night. #Club #AfterHours", "5h", false, 86, 11, 4,
                Shot(paths, "club-a", 640, 640), Shot(paths, "club-b", 640, 640), Shot(paths, "club-c", 640, 640)),
            Post("demo-post-ace", "demo:ace", "Ace", "@ace", ace,
                "New track dropping soon... Stay tuned.", "8h", false, 42, 9, 3),
            Post("demo-post-luna", "demo:luna", "Luna", "@luna_v", luna,
                "Balmung sunset hit different. #Crystal #VYBE", "1d", true, 210, 18, 7,
                Shot(paths, "sunset", 960, 540)),
            Post("demo-post-echo", "demo:echo", "Echo", "@echo_", echo,
                "Free Company night on Leviathan. #FreeCompany #GoodVibes", "1d", false, 64, 6, 2,
                Shot(paths, "fc", 960, 540)),
            Post("demo-post-vex", "demo:vex", "Vex", "@vex_", vex,
                "GPose dump from the ward. #GPose", "2d", false, 33, 4, 1,
                Shot(paths, "pose", 720, 900)),
            Post("plus-post-velvet", "demo:velvet", "Velvet", "@velvet", Face(paths, "raven"),
                "Plus-only set. Lights off, glam on. #VYBEPlus", "38m", true, 94, 19, 8,
                Shot(paths, "club-c", 640, 640), Shot(paths, "pose", 720, 900)),
            Post("plus-post-hex", "demo:hex", "Hex", "@hex_", Face(paths, "ace"),
                "Private lounge is 18+. Don't share these. #NSFW #VYBEPlus", "1h", false, 61, 14, 5,
                Shot(paths, "club-a", 640, 640)),
            Post("plus-post-noir", "demo:noir", "Noir", "@noir", Face(paths, "novale"),
                "Unfiltered GPose dump. You asked for it. #AfterHours #NSFW", "3h", false, 47, 8, 3,
                Shot(paths, "city", 960, 540)),
            Post("plus-post-ember", "demo:ember", "Ember", "@ember", Face(paths, "luna"),
                "Looking for company that can keep up after midnight. Adults only. #VYBEPlus", "6h", false, 73, 21, 6),
        ];
    }

    private static ScenePerson Person(HostPaths paths, string key, string name, string handle, string world, string line,
        bool online, float r, float g, float b, string face = "", bool nightOnly = false, bool plusMember = false)
    {
        var id = VybeState.StableId("demo:" + key);
        plusMember = plusMember || nightOnly;
        return new ScenePerson(id, "demo:" + key, name, handle, world, line, online, nightOnly ? 9 : 4, nightOnly,
            new Vector4(r, g, b, 1f),
            nightOnly ? ["Dating", "ERP"] : ["Friends", "GPose"],
            nightOnly ? ["VYBE+", "18+"] : ["Playful"],
            Face(paths, face.Length > 0 ? face : key), PlusMember: plusMember,
            TimeZoneId: WorldZones.PickFor(key), Race: DemoRace(key));
    }

    public static bool HasPlusAccount(ScenePerson person) => person.PlusMember || person.NightOnly;

    private static string DemoRace(string key) => key switch
    {
        "luna" or "iris" or "velvet" => "Miqo'te",
        "ace" or "vex" or "hex" => "Au Ra",
        "kairo" or "echo" or "ember" => "Elezen",
        "nyx" or "noir" => "Viera",
        "raven" or "novale" => "Au Ra",
        "sol" or "jett" => "Hrothgar",
        "wren" => "Hyur",
        _ => SceneBook.Races[Math.Abs(key.GetHashCode(StringComparison.Ordinal)) % SceneBook.Races.Length],
    };

    private static PearlPost Post(string id, string authorId, string name, string handle, string avatar, string body,
        string when, bool liked, int likes, int comments, int reposts, params PearlMedia[] media) =>
        new(id, authorId, name, handle, avatar, body, when, false, liked, likes, comments, reposts, false,
            string.Empty, string.Empty, string.Empty, media);

    private static PearlMedia Shot(HostPaths paths, string key, int width, int height) =>
        new("demo-media-" + key, FileOf(paths, key + ".png"), width, height);

    public static string StoryStill(HostPaths paths, string gateId)
    {
        var key = gateId.StartsWith("demo:", StringComparison.Ordinal) ? gateId[5..] : gateId;
        var file = key switch
        {
            "luna" => "sunset.png",
            "ace" => "city.png",
            "raven" => "club-a.png",
            "vex" => "pose.png",
            "echo" => "fc.png",
            "novale" => "club-b.png",
            "velvet" => "club-c.png",
            "hex" => "club-a.png",
            "noir" => "city.png",
            "ember" => "pose.png",
            _ => string.Empty,
        };
        return file.Length == 0 ? string.Empty : FileOf(paths, file);
    }

    public static string FaceOf(HostPaths paths, string key) => FileOf(paths, key + ".png");

    private static string Face(HostPaths paths, string key) => FaceOf(paths, key);

    private static string FileOf(HostPaths paths, string file) =>
        paths.Asset(Path.Combine("Icons", "vybe-demo", file));
}
