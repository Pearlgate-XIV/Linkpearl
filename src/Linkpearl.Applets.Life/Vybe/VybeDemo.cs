using System;
using System.Collections.Generic;
using System.IO;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Vybe;

internal static class VybeDemo
{
    public const string Folder = "Icons/vybe-demo";

    public static void Seed(VybeState state, HostPaths paths, bool seedHearts, string homeWorld = "")
    {
        var people = People(paths);
        if (homeWorld.Length > 0)
        {
            people[0] = people[0] with { World = homeWorld };
            people[2] = people[2] with { World = homeWorld };
            people[7] = people[7] with { World = homeWorld };
        }

        for (var index = state.Roster.Count - 1; index >= 0; index--)
        {
            if (state.Roster[index].GateId.StartsWith("demo:", StringComparison.Ordinal))
            {
                state.Roster.RemoveAt(index);
            }
        }

        for (var index = 0; index < people.Length; index++)
        {
            var person = people[index];
            state.Roster.Add(person);
            state.LiveStories.Add(person.Id);
        }

        if (seedHearts && state.LikedPeople.Count == 0)
        {
            state.LikedPeople.Add(people[0].Id);
            state.LikedPeople.Add(people[2].Id);
        }

        if (state.Incoming.Count == 0)
        {
            state.Incoming.Add(people[1].Id);
            state.Incoming.Add(people[10].Id);
        }

        state.Incoming.Remove(people[4].Id);

        TryConnect(state, people[0].Id);
        TryConnect(state, people[2].Id);
        TryConnect(state, people[4].Id);
        TryConnect(state, people[5].Id);
        TryConnect(state, people[6].Id);
        TryConnect(state, people[7].Id);
        TryConnect(state, people[8].Id);
        TryConnect(state, people[9].Id);
        TryConnect(state, people[11].Id);
        state.StarredChats.Add(VybeState.LocalTalkKey(people[0].Id));
        state.StarredChats.Add(VybeState.LocalTalkKey(people[7].Id));

        SeedTalk(state, people[0].Id, "Sunset on the ward is unreal. Come through.",
            new ChatLine(false, "You around tonight?", "2:14 PM"),
            new ChatLine(true, "Just logged in.", "2:16 PM"),
            new ChatLine(false, "Sunset on the ward is unreal. Come through.", "2:17 PM"),
            new ChatLine(true, "On my way. Need a minute to swap glam ✨", "2:18 PM"),
            new ChatLine(false, "Take your time. I grabbed a table by the fountain.", "2:19 PM"),
            new ChatLine(true, "Perfect. Want coffee or something stronger?", "2:20 PM"),
            new ChatLine(false, "Dealer's choice. Bring that playlist too 🎶", "2:21 PM"),
            new ChatLine(true, "Already queued. See you in five.", "2:22 PM"));
        SeedTalk(state, people[2].Id, "Always.",
            new ChatLine(false, "Ward walks after midnight. You in?", "Yesterday"),
            new ChatLine(true, "Always.", "Yesterday"),
            new ChatLine(false, "Same route as last week. Meet at the aetheryte.", "Yesterday"),
            new ChatLine(true, "I'll be the one with the lantern.", "Yesterday"),
            new ChatLine(false, "Don't forget the snacks this time 😂", "Yesterday"),
            new ChatLine(true, "I bought extra. Lesson learned.", "Yesterday"),
            new ChatLine(false, "Good. Echo might join if we start late.", "11:40 PM"),
            new ChatLine(true, "Tell them I'll save a seat.", "11:41 PM"));
        SeedTalk(state, people[4].Id, "",
            new ChatLine(false, "That set last night was feral.", "5:02 PM"),
            new ChatLine(true, "I lost my voice yelling the chorus.", "5:04 PM"),
            new ChatLine(false, "Same. Ace dropped that unreleased track at 2am.", "5:05 PM"),
            new ChatLine(true, "I need that file. Did anyone record it?", "5:06 PM"),
            new ChatLine(false, "Nyx said they clipped the drop. Asking now.", "5:08 PM"),
            new ChatLine(true, "If they send it I'll bounce you a copy.", "5:09 PM"),
            new ChatLine(false, "Deal. Club again Friday? 🔥", "5:11 PM"),
            new ChatLine(true, "I'm in. Save me a spot on the rail.", "5:12 PM"),
            new ChatLine(false, "Already did. Don't be late this time.", "5:13 PM"));
        SeedTalk(state, people[5].Id, "",
            new ChatLine(false, "Need a pose partner tomorrow.", "1:20 PM"),
            new ChatLine(true, "Ward 12? I can do late afternoon.", "1:22 PM"),
            new ChatLine(false, "Yes. Rooftop with the pink neon.", "1:23 PM"),
            new ChatLine(true, "I know the one. Bring the black coat.", "1:24 PM"),
            new ChatLine(false, "And the staff. The lighting loves it.", "1:25 PM"),
            new ChatLine(true, "I'll dump the shots in a folder after.", "1:27 PM"));
        SeedTalk(state, people[6].Id, "",
            new ChatLine(false, "FC house is open if you want dinner.", "6:40 PM"),
            new ChatLine(true, "What are we cooking?", "6:41 PM"),
            new ChatLine(false, "Stew and too much cake. Wren already started.", "6:42 PM"),
            new ChatLine(true, "Say less. I'll teleport after this duty.", "6:43 PM"),
            new ChatLine(false, "Bring Sol if you see them online.", "6:44 PM"),
            new ChatLine(true, "They're on a rooftop. I'll ping.", "6:45 PM"),
            new ChatLine(false, "Perfect. We saved you a plate 💜", "6:46 PM"));
        SeedTalk(state, people[7].Id, "",
            new ChatLine(false, "You still awake?", "1:08 AM"),
            new ChatLine(true, "Barely. What's up?", "1:09 AM"),
            new ChatLine(false, "Can't sleep. Music is too good tonight.", "1:10 AM"),
            new ChatLine(true, "Put me on speaker. I'll stay for a song.", "1:11 AM"),
            new ChatLine(false, "This one first. Tell me if it slaps.", "1:12 AM"),
            new ChatLine(true, "It slaps. Play the next one.", "1:14 AM"),
            new ChatLine(false, "Okay but after this we both log.", "1:15 AM"),
            new ChatLine(true, "Lies. See you in an hour 😴", "1:16 AM"),
            new ChatLine(false, "Fair. Night market tomorrow if we survive.", "1:17 AM"),
            new ChatLine(true, "I'm there. Coffee first though.", "1:18 AM"));
        SeedTalk(state, people[8].Id, "",
            new ChatLine(false, "Sunset from the roof is ridiculous tonight.", "7:31 PM"),
            new ChatLine(true, "Send a shot.", "7:32 PM"),
            new ChatLine(false, "Give me two minutes. Wind is wild.", "7:33 PM"),
            new ChatLine(true, "Stay back from the edge please.", "7:33 PM"),
            new ChatLine(false, "I lived. Look at that orange.", "7:36 PM"),
            new ChatLine(true, "Okay that's illegal. We need a group pose.", "7:37 PM"));
        SeedTalk(state, people[9].Id, "",
            new ChatLine(false, "House tour later? I finished the garden.", "4:05 PM"),
            new ChatLine(true, "Yes. I still owe you those indoor plants.", "4:06 PM"),
            new ChatLine(false, "Bring them. Echo said they'll help hang lights.", "4:07 PM"),
            new ChatLine(true, "I'll be there after raid. Don't start without me.", "4:08 PM"),
            new ChatLine(false, "We won't. There's cake if you're fast 🎂", "4:09 PM"));
        SeedTalk(state, people[11].Id, "",
            new ChatLine(false, "Dance floor is packed. Where are you?", "11:02 PM"),
            new ChatLine(true, "By the stairs. Pink jacket.", "11:03 PM"),
            new ChatLine(false, "I see you. Don't move.", "11:03 PM"),
            new ChatLine(true, "Too late I moved 😭", "11:04 PM"),
            new ChatLine(false, "Found you. Next song is ours.", "11:05 PM"),
            new ChatLine(true, "Lead. I follow.", "11:06 PM"));
    }

    private static void TryConnect(VybeState state, int personId)
    {
        if (!state.TalkHidden(VybeState.LocalTalkKey(personId)))
        {
            state.Connected.Add(personId);
        }
    }

    private static void SeedTalk(VybeState state, int personId, string oldTail, params ChatLine[] lines)
    {
        var thread = state.Thread(personId);
        if (thread.Count > 0)
        {
            if (oldTail.Length == 0 || thread.Count >= 4 ||
                !string.Equals(thread[^1].Body, oldTail, StringComparison.Ordinal))
            {
                return;
            }

            thread.Clear();
        }

        thread.AddRange(lines);
    }

    public static ScenePerson[] People(HostPaths paths)
    {
        return
        [
            Person(paths, "luna", "Luna", "@luna_v", "Balmung", "Night markets and neon.", true, 0.72f, 0.28f, 0.46f),
            Person(paths, "ace", "Ace", "@ace", "Crystal", "New track dropping soon.", true, 0.22f, 0.42f, 0.78f),
            Person(paths, "kairo", "Kairo", "@kairo", "Balmung", "Ward walks after midnight.", true, 0.38f, 0.22f, 0.72f,
                "ace"),
            Person(paths, "nyx", "Nyx", "@nyx_", "Mateus", "Quiet corners. Loud nights.", false, 0.64f, 0.20f, 0.52f,
                "raven"),
            Person(paths, "raven", "Raven", "@raven_", "Mateus", "Club lights, late hours.", true, 0.42f, 0.18f, 0.58f),
            Person(paths, "vex", "Vex", "@vex_", "Gilgamesh", "GPose first, talk later.", false, 0.18f, 0.62f, 0.48f),
            Person(paths, "echo", "Echo", "@echo_", "Leviathan", "#Crystal #GoodVibes", true, 0.86f, 0.42f, 0.22f),
            Person(paths, "novale", "NoVale", "@novale", "Balmung", "Late nights. Better company.", true, 0.90f, 0.26f,
                0.40f),
            Person(paths, "sol", "Sol", "@sol", "Jenova", "Sunset rooftops.", true, 0.94f, 0.48f, 0.22f, "luna"),
            Person(paths, "wren", "Wren", "@wren", "Siren", "FC house is open.", true, 0.28f, 0.58f, 0.46f, "echo"),
            Person(paths, "iris", "Iris", "@iris", "Gilgamesh", "Looking for a pose partner.", false, 0.78f, 0.32f,
                0.54f, "vex"),
            Person(paths, "jett", "Jett", "@jett", "Leviathan", "On the dance floor.", true, 0.20f, 0.34f, 0.82f,
                "novale"),
        ];
    }

    public static PearlPost[] Posts(HostPaths paths)
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
                "Balmung sunset hit different. #Crystal #Daylight", "1d", true, 210, 18, 7,
                Shot(paths, "sunset", 960, 540)),
            Post("demo-post-echo", "demo:echo", "Echo", "@echo_", echo,
                "Free Company night on Leviathan. #FreeCompany #GoodVibes", "1d", false, 64, 6, 2,
                Shot(paths, "fc", 960, 540)),
            Post("demo-post-vex", "demo:vex", "Vex", "@vex_", vex,
                "GPose dump from the ward. #GPose", "2d", false, 33, 4, 1,
                Shot(paths, "pose", 720, 900)),
        ];
    }

    private static ScenePerson Person(HostPaths paths, string key, string name, string handle, string world, string line,
        bool online, float r, float g, float b, string face = "")
    {
        var id = VybeState.StableId("demo:" + key);
        return new ScenePerson(id, "demo:" + key, name, handle, world, line, online, 4, false,
            new Vector4(r, g, b, 1f), ["Friends", "GPose"], ["Playful"], Face(paths, face.Length > 0 ? face : key));
    }

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
            _ => string.Empty,
        };
        return file.Length == 0 ? string.Empty : FileOf(paths, file);
    }

    public static string FaceOf(HostPaths paths, string key) => FileOf(paths, key + ".png");

    private static string Face(HostPaths paths, string key) => FaceOf(paths, key);

    private static string FileOf(HostPaths paths, string file) =>
        paths.Asset(Path.Combine("Icons", "vybe-demo", file));
}
