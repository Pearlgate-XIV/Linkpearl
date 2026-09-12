using System;
using System.Collections.Generic;
using System.Linq;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Vybe;

internal sealed class PeopleFindState
{
    public int Mode { get; set; }

    public bool SearchOpen { get; set; }

    public string Query { get; set; } = string.Empty;

    public bool MoreOpen { get; set; }

    public bool WhyOpen { get; set; }

    public int WhyId { get; set; } = -1;

    public float Scroll { get; set; }

    public float ModeDrag { get; set; }

    public float ChipDrag { get; set; }

    public int PulseId { get; set; } = -1;

    public float Pulse { get; set; }

    public int LastPass { get; set; } = -1;

    public float PassFade { get; set; }

    public List<string> Quick { get; set; } = new();

    public List<string> LookingFor { get; set; } = new();

    public List<string> Connection { get; set; } = new();

    public List<string> DataCenters { get; set; } = new();

    public List<string> Worlds { get; set; } = new();

    public List<string> Nearby { get; set; } = new();

    public List<string> Races { get; set; } = new();

    public List<string> Roles { get; set; } = new();

    public List<string> Jobs { get; set; } = new();

    public List<string> Interests { get; set; } = new();

    public string SharedFloor { get; set; } = string.Empty;

    public List<string> RpInterest { get; set; } = new();

    public List<string> RpTypes { get; set; } = new();

    public string WalkUp { get; set; } = string.Empty;

    public List<string> Activity { get; set; } = new();

    public List<string> PlayTimes { get; set; } = new();

    public bool OverlapPlay { get; set; }

    public List<string> Communication { get; set; } = new();

    public List<string> Social { get; set; } = new();

    public List<string> DatingIntent { get; set; } = new();

    public List<string> Status { get; set; } = new();

    public int[] LaneWant { get; set; } = [];

    public List<int> Passed { get; set; } = new();

    public List<int> Saved { get; set; } = new();

    public List<string> OpenBlocks { get; set; } = new();

    public int Count()
    {
        return LookingFor.Count + Connection.Count + DataCenters.Count + Worlds.Count + Nearby.Count +
               Races.Count + Roles.Count + Jobs.Count + Interests.Count + RpInterest.Count + RpTypes.Count +
               Activity.Count + PlayTimes.Count + Communication.Count + Social.Count + DatingIntent.Count +
               Status.Count + Quick.Count + (SharedFloor.Length > 0 ? 1 : 0) + (WalkUp.Length > 0 ? 1 : 0) +
               (OverlapPlay ? 1 : 0) + (Mode > 0 ? 1 : 0) + VybeLaneMap.ActiveWants(LaneWant);
    }

    public void Clear()
    {
        Mode = 0;
        Query = string.Empty;
        MoreOpen = false;
        Quick.Clear();
        LookingFor.Clear();
        Connection.Clear();
        DataCenters.Clear();
        Worlds.Clear();
        Nearby.Clear();
        Races.Clear();
        Roles.Clear();
        Jobs.Clear();
        Interests.Clear();
        SharedFloor = string.Empty;
        RpInterest.Clear();
        RpTypes.Clear();
        WalkUp = string.Empty;
        Activity.Clear();
        PlayTimes.Clear();
        OverlapPlay = false;
        Communication.Clear();
        Social.Clear();
        DatingIntent.Clear();
        Status.Clear();
        LaneWant = VybeLaneMap.Blank();
    }

    public string[] Summary()
    {
        var rows = Applied();
        return rows.Count > 6 ? rows.GetRange(0, 6).ToArray() : rows.ToArray();
    }

    public List<string> Applied()
    {
        var rows = new List<string>();
        if (Query.Length > 0)
        {
            rows.Add(Query);
        }

        if (Mode > 0 && Mode < PeopleFindBook.Modes.Length)
        {
            rows.Add(PeopleFindBook.Modes[Mode]);
        }

        Pack(rows, Quick);
        Pack(rows, LookingFor);
        Pack(rows, Connection);
        Pack(rows, DataCenters);
        Pack(rows, Worlds);
        Pack(rows, Nearby);
        Pack(rows, Races);
        Pack(rows, Roles);
        Pack(rows, Jobs);
        Pack(rows, Interests);
        Pack(rows, RpInterest);
        Pack(rows, RpTypes);
        Pack(rows, Activity);
        Pack(rows, PlayTimes);
        Pack(rows, Communication);
        Pack(rows, Social);
        Pack(rows, DatingIntent);
        Pack(rows, Status);
        if (SharedFloor.Length > 0)
        {
            PackOne(rows, SharedFloor);
        }

        if (WalkUp.Length > 0)
        {
            PackOne(rows, "Walk-up " + WalkUp);
        }

        if (OverlapPlay)
        {
            PackOne(rows, "Play overlap");
        }

        var want = VybeLaneMap.Fit(LaneWant);
        for (var index = 0; index < want.Length; index++)
        {
            if (want[index] >= 10)
            {
                PackOne(rows, LaneWantLabel(index, want[index]));
            }
        }

        return rows;
    }

    public void Drop(string label)
    {
        if (string.Equals(Query, label, StringComparison.Ordinal))
        {
            Query = string.Empty;
        }

        if (Mode > 0 && Mode < PeopleFindBook.Modes.Length &&
            string.Equals(PeopleFindBook.Modes[Mode], label, StringComparison.Ordinal))
        {
            Mode = 0;
        }

        Quick.Remove(label);
        LookingFor.Remove(label);
        Connection.Remove(label);
        DataCenters.Remove(label);
        Worlds.Remove(label);
        Nearby.Remove(label);
        Races.Remove(label);
        Roles.Remove(label);
        Jobs.Remove(label);
        Interests.Remove(label);
        RpInterest.Remove(label);
        RpTypes.Remove(label);
        Activity.Remove(label);
        PlayTimes.Remove(label);
        Communication.Remove(label);
        Social.Remove(label);
        DatingIntent.Remove(label);
        Status.Remove(label);
        if (string.Equals(SharedFloor, label, StringComparison.Ordinal))
        {
            SharedFloor = string.Empty;
        }

        if (label.StartsWith("Walk-up ", StringComparison.Ordinal))
        {
            WalkUp = string.Empty;
        }

        if (label == "Play overlap")
        {
            OverlapPlay = false;
        }

        var want = VybeLaneMap.Fit(LaneWant);
        for (var index = 0; index < want.Length; index++)
        {
            if (want[index] >= 10 && string.Equals(label, LaneWantLabel(index, want[index]), StringComparison.Ordinal))
            {
                want[index] = 0;
            }
        }

        LaneWant = want;
    }

    private static string LaneWantLabel(int lane, int percent) =>
        VybeLaneMap.Lanes[lane] + " " + percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";

    private static void Pack(List<string> rows, List<string> source)
    {
        for (var index = 0; index < source.Count; index++)
        {
            PackOne(rows, source[index]);
        }
    }

    private static void PackOne(List<string> rows, string value)
    {
        if (value.Length > 0 && !rows.Contains(value))
        {
            rows.Add(value);
        }
    }

    public static void Flip(List<string> bag, string value)
    {
        if (bag.Contains(value))
        {
            bag.Remove(value);
            return;
        }

        bag.Add(value);
    }

    public static void FlipInt(List<int> bag, int value)
    {
        if (bag.Contains(value))
        {
            bag.Remove(value);
            return;
        }

        bag.Add(value);
    }
}

internal readonly record struct PeopleCard(
    int Id,
    string GateId,
    string Name,
    string Handle,
    string World,
    string DataCenter,
    string Job,
    string[] Jobs,
    string Role,
    string Race,
    string[] LookingFor,
    string[] Interests,
    string Bio,
    string RpInterest,
    string[] RpTypes,
    string WalkUp,
    string Activity,
    bool Online,
    string PlayTime,
    string Comm,
    string Social,
    string DatingIntent,
    string Relationship,
    int Age,
    bool DatingOn,
    bool NearbyOn,
    string Avatar,
    string Gender = "",
    string Sexuality = "",
    bool DmsOpen = true,
    bool PlusOnly = false,
    bool PlusMember = false,
    string TimeZoneId = "",
    int[]? Pulse = null);

internal static class PeopleFindBook
{
    public static readonly string[] Modes = { "Suggested", "Nearby", "Dating", "Friends", "RP" };

    public static readonly string[] Quick =
    {
        "Online Now", "Same World", "Same DC", "Nearby", "Dating", "Friends", "RP", "Events",
    };

    public static readonly string[] LookingFor =
    {
        "Friends", "Dating", "Long-term Relationship", "Casual Dating", "Flirting", "RP Partner",
        "Gaming Partner", "Venue Friends", "DJ / Nightlife Connections", "Event Buddies",
        "Dungeon / Raid Friends", "FC Connections", "Creative Collaborators", "Just Chatting",
    };

    public static readonly string[] Connection =
    {
        "Open to New People", "Open to Messages", "Looking for Matches", "Recently Active", "Online Now",
        "Mutual Followers", "Friends of Friends", "Shared Interests", "Shared Groups",
    };

    public static readonly string[] DataCenters =
    {
        "My Data Center", "Aether", "Crystal", "Primal", "Dynamis", "Light", "Chaos",
    };

    public static readonly string[] Worlds =
    {
        "My World", "Balmung", "Mateus", "Gilgamesh", "Jenova", "Siren", "Leviathan", "Cactuar", "Zalera",
        "Adamantoise", "Excalibur",
    };

    public static readonly string[] Nearby = { "Same Zone", "Same City", "At My Venue", "Nearby Players" };

    public static readonly string[] Races = SceneBook.Races;

    public static readonly string[] Roles =
    {
        "Tank", "Healer", "Melee DPS", "Physical Ranged", "Caster", "Crafter", "Gatherer",
    };

    public static readonly string[] Jobs =
    {
        "PLD", "WAR", "WHM", "SCH", "MNK", "DRG", "BRD", "DNC", "BLM", "SMN", "CRP", "BTN",
    };

    public static readonly string[] Play =
    {
        "Casual Content", "Dungeons", "Raiding", "Savage", "Ultimate", "PvP", "Deep Dungeons", "Treasure Maps",
        "Hunts", "Crafting", "Gathering", "Housing", "Glamour", "GPOSE", "Fishing", "Achievement Hunting",
    };

    public static readonly string[] Social =
    {
        "Venues", "Clubs", "Cafes", "Nightlife", "DJ Events", "Concerts", "RP", "Photography", "Fashion",
        "Housing Design", "Community Events",
    };

    public static readonly string[] Creative =
    {
        "Music", "DJing", "Modding", "Screenshots", "Art", "Writing", "Streaming", "Content Creation",
    };

    public static readonly string[] Shared = { "Any", "1+", "2+", "3+", "Highly Compatible" };

    public static readonly string[] RpInterest = { "No RP", "RP Curious", "Casual RP", "Regular RP", "Heavy RP" };

    public static readonly string[] RpTypes =
    {
        "Social", "Adventure", "Story", "Tavern", "Slice of Life", "Romance", "Lore-Focused", "Freeform",
    };

    public static readonly string[] WalkUp = { "Yes", "Ask First", "No" };

    public static readonly string[] Activity = { "Online Now", "Active Today", "Active This Week", "Any" };

    public static readonly string[] PlayTimes = { "Morning", "Afternoon", "Evening", "Late Night", "Variable" };

    public static readonly string[] Comm = { "Text Chat", "PearlChat", "Discord", "Voice Chat", "In-Game Only" };

    public static readonly string[] Style = { "Quiet / Chill", "Talkative", "Highly Social", "One-on-One", "Group Social" };

    public static readonly string[] DatingIntent =
    {
        "Meeting New People", "Casual Dating", "Serious Dating", "Long-term Relationship", "Flirting",
        "Unsure / Exploring",
    };

    public static readonly string[] Relationship =
    {
        "Single", "Dating", "Partnered", "Open Relationship", "Prefer Not To Say",
    };

    public static string DataCenterOf(string world) => world switch
    {
        "Aether" or "Crystal" or "Primal" or "Dynamis" or "Light" or "Chaos" or "Elemental" or "Gaia" or "Mana"
            or "Meteor" or "Materia" => world,
        "Balmung" or "Mateus" or "Zalera" or "Diabolos" or "Coeurl" or "Malboro" => "Crystal",
        "Gilgamesh" or "Jenova" or "Siren" or "Adamantoise" or "Cactuar" or "Faerie" or "Midgardsormr" or "Sargatanas" =>
            "Aether",
        "Leviathan" or "Excalibur" or "Hyperion" or "Behemoth" or "Famfrit" or "Lamia" or "Ultros" => "Primal",
        "Halicarnassus" or "Maduin" or "Marilith" or "Seraph" or "Cuchulainn" or "Golem" or "Kraken" or "Rafflesia" =>
            "Dynamis",
        "Shiva" or "Twintania" or "Odin" or "Lich" or "Zodiark" or "Phoenix" or "Alpha" or "Raiden" or "Sagittarius"
            or "Phantom" => "Light",
        "Cerberus" or "Louisoix" or "Moogle" or "Omega" or "Ragnarok" or "Spriggan" => "Chaos",
        "Aegis" or "Atomos" or "Carbuncle" or "Garuda" or "Gungnir" or "Kujata" or "Tonberry" or "Typhon" => "Elemental",
        "Alexander" or "Bahamut" or "Durandal" or "Fenrir" or "Ifrit" or "Ridill" or "Tiamat" or "Ultima" => "Gaia",
        "Anima" or "Asura" or "Chocobo" or "Hades" or "Ixion" or "Masamune" or "Pandaemonium" or "Titan" => "Mana",
        "Belias" or "Mandragora" or "Ramuh" or "Shinryu" or "Unicorn" or "Valefor" or "Yojimbo" or "Zeromus" => "Meteor",
        "Bismarck" or "Ravana" or "Sephirot" or "Sophia" or "Zurvan" => "Materia",
        _ => "Crystal",
    };

    public static string RegionOf(string world) => DataCenterOf(world) switch
    {
        "Aether" or "Crystal" or "Primal" or "Dynamis" => "NA",
        "Light" or "Chaos" => "EU",
        "Elemental" or "Gaia" or "Mana" or "Meteor" => "JP",
        "Materia" => "OC",
        _ => "NA",
    };

    public static PeopleCard[] Deck(HostPaths paths) => [];

    public static PeopleCard[] LiveDeck(PearlSnapshot snap, IReadOnlyList<ScenePerson> roster, string homeWorld)
    {
        var cards = new List<PeopleCard>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(PeopleCard card)
        {
            if (card.GateId.Length == 0 ||
                string.Equals(card.GateId, snap.MeId, StringComparison.Ordinal) ||
                !seen.Add(card.GateId) ||
                VybeChrome.IsLalafell(card.Race))
            {
                return;
            }

            cards.Add(card);
        }

        foreach (var person in snap.Directory)
        {
            Add(FromPearl(person, homeWorld));
        }

        foreach (var person in snap.People)
        {
            Add(FromPearl(person, homeWorld));
        }

        foreach (var hit in snap.SearchHits)
        {
            if (hit.Id.Length > 0)
            {
                Add(FromHit(hit, homeWorld));
            }
        }

        foreach (var post in snap.Feed)
        {
            Add(FromPost(post, homeWorld));
        }

        foreach (var person in roster)
        {
            Add(FromScene(person, homeWorld));
        }

        return cards.ToArray();
    }

    private static PeopleCard FromPearl(PearlPerson person, string homeWorld)
    {
        var name = person.DisplayName.Length > 0 ? person.DisplayName : "Someone";
        var world = person.World;
        return LiveCard(person.Id, name, person.Handle, world, person.Race, person.AvatarUrl, person.TimeZoneId,
            string.Empty, [], [], true, homeWorld);
    }

    private static PeopleCard FromHit(PearlHit hit, string homeWorld)
    {
        var world = hit.Subtitle.StartsWith('@') ? string.Empty : hit.Subtitle;
        var handle = hit.Subtitle.StartsWith('@') ? hit.Subtitle : string.Empty;
        return LiveCard(hit.Id, hit.Title.Length > 0 ? hit.Title : "Someone", handle, world, string.Empty,
            string.Empty, string.Empty, string.Empty, [], [], true, homeWorld);
    }

    private static PeopleCard FromPost(PearlPost post, string homeWorld) =>
        LiveCard(post.AuthorId, post.AuthorName.Length > 0 ? post.AuthorName : "Someone", post.AuthorHandle,
            string.Empty, string.Empty, post.AuthorAvatarUrl, string.Empty, string.Empty, [], [], true, homeWorld);

    private static PeopleCard FromScene(ScenePerson person, string homeWorld) =>
        LiveCard(person.GateId, person.Name, person.Handle, person.World, person.Race, person.AvatarUrl,
            person.TimeZoneId, person.Line, person.Intents, person.Tags, person.Online, homeWorld, person.Id,
            person.Gender, person.Sexuality, person.Relationship, person.DmsOpen ?? true, person.PlusMember);

    private static PeopleCard LiveCard(string gateId, string name, string handle, string world, string race,
        string avatar, string timeZone, string bio, string[] looking, string[] interests, bool online,
        string homeWorld, int id = 0, string gender = "", string sexuality = "", string relationship = "",
        bool dmsOpen = true, bool plusMember = false)
    {
        var nearby = homeWorld.Length > 0 &&
                     string.Equals(world, homeWorld, StringComparison.OrdinalIgnoreCase);
        return new PeopleCard(id == 0 ? VybeState.StableId(gateId) : id, gateId, name, handle, world,
            DataCenterOf(world), string.Empty, [], string.Empty, race, looking, interests, bio, string.Empty, [],
            string.Empty, online ? "Online" : "Today", online, "Variable", string.Empty, string.Empty, string.Empty,
            relationship, 0, false, nearby, avatar, gender, sexuality, dmsOpen, false, plusMember, timeZone);
    }

    private static PeopleCard[] DemoDeck(HostPaths paths)
    {
        return
        [
            Card(paths, "luna", "Luna Sky", "@luna_v", "Balmung", "Dancer", "Physical Ranged", "Viera",
                ["Friends", "Dating", "Venue Friends"], ["Music", "Venues", "GPOSE", "Nightlife", "Fashion"],
                "Night markets, neon, and a good set.", "Casual RP", ["Social", "Tavern"], "Ask First",
                "Online", true, "Late Night", "PearlChat", "Highly Social", "Meeting New People", "Single", 24,
                true, true),
            Card(paths, "ace", "Ace Vale", "@ace", "Gilgamesh", "Bard", "Physical Ranged", "Hyur",
                ["Friends", "DJ / Nightlife Connections", "Creative Collaborators"],
                ["Music", "DJing", "Concerts", "Streaming", "Venues"],
                "New track dropping soon. Come hang.", "RP Curious", ["Social"], "No",
                "Online", true, "Evening", "Discord", "Talkative", "Unsure / Exploring", "Single", 26,
                false, true, "ace"),
            Card(paths, "kairo", "Kairo West", "@kairo", "Balmung", "Dark Knight", "Tank", "Au Ra",
                ["Friends", "Gaming Partner", "Dungeon / Raid Friends"],
                ["Raiding", "Savage", "Dungeons", "Hunts", "Glamour"],
                "Ward walks after midnight. Static looking for a healer.", "No RP", Array.Empty<string>(), "No",
                "Online", true, "Late Night", "Voice Chat", "One-on-One", "", "Prefer Not To Say", 0,
                false, true, "ace"),
            Card(paths, "nyx", "Nyx Hollow", "@nyx_", "Mateus", "Reaper", "Melee DPS", "Miqo'te",
                ["RP Partner", "Friends", "Just Chatting"],
                ["RP", "Writing", "Lore-Focused", "Story", "Cafes"],
                "Quiet corners. Loud nights. Walk-up if you ask first.", "Regular RP",
                ["Story", "Slice of Life", "Romance"], "Ask First",
                "Today", false, "Evening", "Text Chat", "Quiet / Chill", "Flirting", "Single", 23,
                true, false, "raven"),
            Card(paths, "raven", "Raven Quill", "@raven_", "Mateus", "Black Mage", "Caster", "Elezen",
                ["Venue Friends", "Event Buddies", "Friends"],
                ["Clubs", "Nightlife", "DJ Events", "Venues", "Photography"],
                "Club lights, late hours. I'll be on the floor.", "Casual RP", ["Tavern", "Social"], "Yes",
                "Online", true, "Late Night", "PearlChat", "Highly Social", "", "", 0,
                false, true),
            Card(paths, "vex", "Vex Mori", "@vex_", "Jenova", "Samurai", "Melee DPS", "Au Ra",
                ["Creative Collaborators", "Friends", "Just Chatting"],
                ["GPOSE", "Screenshots", "Art", "Fashion", "Housing"],
                "GPose first, talk later. Commission book is closed.", "RP Curious", ["Freeform"], "No",
                "Week", false, "Afternoon", "In-Game Only", "Quiet / Chill", "", "Prefer Not To Say", 0,
                false, false),
            Card(paths, "echo", "Echo Lin", "@echo_", "Leviathan", "White Mage", "Healer", "Hyur",
                ["FC Connections", "Dungeon / Raid Friends", "Friends"],
                ["Casual Content", "Dungeons", "Community Events", "Housing", "Crafting"],
                "FC house is open. Tea after roulette.", "Casual RP", ["Social", "Slice of Life"], "Yes",
                "Online", true, "Evening", "PearlChat", "Group Social", "", "", 0,
                false, true),
            Card(paths, "novale", "NoVale", "@novale", "Balmung", "Summoner", "Caster", "Viera",
                ["Friends", "Dating", "Just Chatting"],
                ["Music", "Venues", "RP", "Nightlife", "Photography"],
                "Late nights. Better company.", "Regular RP", ["Social", "Romance", "Tavern"], "Ask First",
                "Online", true, "Late Night", "Discord", "Talkative", "Casual Dating", "Single", 25,
                true, true),
            Card(paths, "sol", "Sol Ember", "@sol", "Siren", "Paladin", "Tank", "Roegadyn",
                ["Event Buddies", "Friends", "Gaming Partner"],
                ["Community Events", "Hunts", "Treasure Maps", "Fishing", "Casual Content"],
                "Sunset rooftops and map parties.", "No RP", Array.Empty<string>(), "No",
                "Today", true, "Morning", "In-Game Only", "Group Social", "", "", 0,
                false, true, "luna"),
            Card(paths, "wren", "Wren Vale", "@wren", "Cactuar", "Botanist", "Gatherer", "Hyur",
                ["Creative Collaborators", "FC Connections", "Friends"],
                ["Housing", "Housing Design", "Gathering", "Crafting", "Glamour"],
                "Redecorating the FC again. Bring plants.", "RP Curious", ["Slice of Life"], "Ask First",
                "Week", true, "Variable", "Text Chat", "Quiet / Chill", "", "", 0,
                false, false, "echo"),
            Card(paths, "iris", "Iris Quinn", "@iris", "Phoenix", "Dancer", "Physical Ranged", "Miqo'te",
                ["Dating", "Flirting", "Venue Friends"],
                ["Fashion", "Venues", "Clubs", "GPOSE", "Music"],
                "Looking for a pose partner and a late set.", "Casual RP", ["Social", "Romance"], "Yes",
                "Online", true, "Late Night", "PearlChat", "Talkative", "Flirting", "Dating", 22,
                true, true, "vex"),
            Card(paths, "jett", "Jett Arden", "@jett", "Cerberus", "Ninja", "Melee DPS", "Hrothgar",
                ["Gaming Partner", "Dungeon / Raid Friends", "Friends"],
                ["PvP", "Dungeons", "Raiding", "Ultimate", "Achievement Hunting"],
                "On the dance floor after savage. Frontline later.", "No RP", Array.Empty<string>(), "No",
                "Online", true, "Evening", "Voice Chat", "Highly Social", "", "", 0,
                false, true, "novale"),
            Card(paths, "velvet", "Velvet Rae", "@velvet", "Balmung", "Dancer", "Physical Ranged", "Viera",
                ["Dating", "Flirting", "Casual Dating"],
                ["Nightlife", "Venues", "GPOSE", "Fashion", "Clubs"],
                "18+ after dark. DMs open. Plus-only.", "Regular RP", ["Romance", "Social"], "Ask First",
                "Online", true, "Late Night", "PearlChat", "Talkative", "Casual Dating", "Single", 26,
                true, true, "raven", true),
            Card(paths, "hex", "Hex Vale", "@hex_", "Mateus", "Reaper", "Melee DPS", "Au Ra",
                ["Dating", "RP Partner", "Flirting"],
                ["RP", "Nightlife", "Photography", "Venues", "Writing"],
                "Private sets. No screenshots. Adults only.", "Heavy RP", ["Romance", "Story"], "Ask First",
                "Online", true, "Late Night", "Discord", "One-on-One", "Flirting", "Single", 27,
                true, true, "ace", true),
            Card(paths, "noir", "Noir Quinn", "@noir", "Gilgamesh", "Bard", "Physical Ranged", "Elezen",
                ["Casual Dating", "Venue Friends", "Just Chatting"],
                ["Clubs", "DJ Events", "Nightlife", "Music", "Fashion"],
                "Plus lounge regular. Keep it off the SFW board.", "Casual RP", ["Tavern", "Social"], "No",
                "Today", false, "Late Night", "PearlChat", "Quiet / Chill", "Casual Dating", "It's Complicated", 29,
                true, false, "novale", true),
            Card(paths, "ember", "Ember Sol", "@ember", "Jenova", "Summoner", "Caster", "Miqo'te",
                ["Dating", "Flirting", "Just Chatting"],
                ["GPOSE", "Venues", "Nightlife", "Photography", "Fashion"],
                "Unfiltered. Ask first. VYBE+ only.", "Regular RP", ["Romance", "Freeform"], "Yes",
                "Online", true, "Late Night", "PearlChat", "Highly Social", "Meeting New People", "Single", 24,
                true, true, "luna", true),
        ];
    }

    public static List<PeopleCard> Match(PeopleCard[] deck, PeopleFindState find, VybeState state,
        string homeWorld, IReadOnlyCollection<string> mine)
    {
        var homeDc = DataCenterOf(homeWorld);
        var picks = new List<PeopleCard>();
        for (var index = 0; index < deck.Length; index++)
        {
            var card = deck[index];
            if (find.Passed.Contains(card.Id) || state.Blocked.Contains(card.Id))
            {
                continue;
            }

            if (state.PeoplePlus)
            {
                if (!card.PlusOnly && card.GateId.StartsWith("demo:", StringComparison.Ordinal))
                {
                    continue;
                }
            }
            else if (card.PlusOnly)
            {
                continue;
            }

            if (!Passes(card, find, state, homeWorld, homeDc, mine))
            {
                continue;
            }

            picks.Add(card);
        }

        var lanes = state.LaneDone ? state.LaneMarks : null;
        picks.Sort((left, right) => Score(right, find, homeWorld, homeDc, mine, lanes) -
                                    Score(left, find, homeWorld, homeDc, mine, lanes));
        return picks;
    }

    public static bool Passes(PeopleCard card, PeopleFindState find, VybeState state, string homeWorld,
        string homeDc, IReadOnlyCollection<string> mine, bool skipQuery = false)
    {
        if (VybeChrome.IsLalafell(card.Race))
        {
            return false;
        }

        find.Races.RemoveAll(VybeChrome.IsLalafell);
        if (!skipQuery && find.Query.Length > 0 &&
            card.Name.IndexOf(find.Query, StringComparison.OrdinalIgnoreCase) < 0 &&
            card.Handle.IndexOf(find.Query, StringComparison.OrdinalIgnoreCase) < 0 &&
            card.World.IndexOf(find.Query, StringComparison.OrdinalIgnoreCase) < 0 &&
            card.Job.IndexOf(find.Query, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (find.Mode == 1 && (!state.NearbyDiscovery || !card.NearbyOn))
        {
            return false;
        }

        if (find.Mode == 2 && !HasAny(card.LookingFor, "Dating", "Casual Dating", "Flirting",
                "Long-term Relationship"))
        {
            return false;
        }

        if (find.Mode == 3 && card.LookingFor.Length > 0 &&
            !HasAny(card.LookingFor, "Friends", "Just Chatting", "FC Connections",
                "Gaming Partner", "Venue Friends", "Event Buddies"))
        {
            return false;
        }

        if (find.Mode == 4 && !HasAny(card.LookingFor, "RP Partner") &&
            card.RpInterest is "No RP" or "")
        {
            return false;
        }

        if (Has("Online Now", find) && !card.Online)
        {
            return false;
        }

        if (Has("Same World", find) && homeWorld.Length > 0 &&
            !string.Equals(card.World, homeWorld, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Has("Same DC", find) && homeDc.Length > 0 &&
            !string.Equals(card.DataCenter, homeDc, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Has("Nearby", find) && (!state.NearbyDiscovery || !card.NearbyOn))
        {
            return false;
        }

        if (Has("Dating", find) && !card.DatingOn)
        {
            return false;
        }

        if (Has("Friends", find) && card.LookingFor.Length > 0 &&
            !HasAny(card.LookingFor, "Friends", "Just Chatting"))
        {
            return false;
        }

        if (Has("RP", find) && card.RpInterest is "No RP" or "")
        {
            return false;
        }

        if (Has("Events", find) && !HasAny(card.Interests, "Community Events", "DJ Events", "Concerts", "Venues"))
        {
            return false;
        }

        if (!ContainsAny(find.LookingFor, card.LookingFor))
        {
            return false;
        }

        if (find.Connection.Contains("Online Now") && !card.Online)
        {
            return false;
        }

        if (find.Connection.Contains("Recently Active") && card.Activity is not ("Online" or "Today"))
        {
            return false;
        }

        if (find.Connection.Contains("Looking for Matches") && !card.DatingOn)
        {
            return false;
        }

        if (!InPlace(find, card, homeWorld, homeDc))
        {
            return false;
        }

        if (find.Nearby.Count > 0 && (!state.NearbyDiscovery || !card.NearbyOn))
        {
            return false;
        }

        if (!ContainsAny(find.Races, card.Race))
        {
            return false;
        }

        if (!ContainsAny(find.Roles, card.Role))
        {
            return false;
        }

        if (!ContainsAny(find.Jobs, card.Jobs) && !ContainsAny(find.Jobs, card.Job))
        {
            return false;
        }

        if (!ContainsAny(find.Interests, card.Interests))
        {
            return false;
        }

        if (!MeetsShared(find.SharedFloor, SharedCount(card.Interests, mine)))
        {
            return false;
        }

        if (!ContainsAny(find.RpInterest, card.RpInterest))
        {
            return false;
        }

        if (!ContainsAny(find.RpTypes, card.RpTypes))
        {
            return false;
        }

        if (find.WalkUp.Length > 0 &&
            !string.Equals(find.WalkUp, card.WalkUp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!InActivity(find.Activity, card))
        {
            return false;
        }

        if (!ContainsAny(find.PlayTimes, card.PlayTime))
        {
            return false;
        }

        if (find.OverlapPlay && mine.Count > 0 && !card.PlayTime.Equals("Variable", StringComparison.Ordinal) &&
            !mine.Contains(card.PlayTime))
        {
            return false;
        }

        if (!ContainsAny(find.Communication, card.Comm))
        {
            return false;
        }

        if (!ContainsAny(find.Social, card.Social))
        {
            return false;
        }

        if (state.DatingDiscovery)
        {
            if (!ContainsAny(find.DatingIntent, card.DatingIntent))
            {
                return false;
            }

            if (!ContainsAny(find.Status, card.Relationship))
            {
                return false;
            }
        }

        return true;
    }

    public static bool FitsPost(PearlPost post, PeopleFindState find, VybeState state, PeopleCard[] deck,
        string homeWorld, string search)
    {
        if (!HitsSearch(search, post.Body, post.AuthorName, post.AuthorHandle) &&
            !PostMarksHit(post, search))
        {
            return false;
        }

        foreach (var (tag, pole) in state.Filters)
        {
            var has = PostMarksHit(post, tag);
            if (pole == FilterPole.Include && !has)
            {
                return false;
            }

            if (pole == FilterPole.Exclude && has)
            {
                return false;
            }
        }

        if (TryAuthor(post, deck, state, out var card))
        {
            return Passes(card, find, state, homeWorld, DataCenterOf(homeWorld), Mine(state), skipQuery: true);
        }

        var marks = VybePostTags.Collect(post);
        return OverlapsLoose(find.Interests, marks) && OverlapsLoose(find.LookingFor, marks);
    }

    public static bool FitsHash(string tag, PeopleFindState find, string search)
    {
        var slug = VybePostTags.Normalize(tag);
        if (slug.Length == 0)
        {
            return false;
        }

        var needle = VybePostTags.Normalize(search);
        if (needle.Length > 0 && slug.IndexOf(needle, StringComparison.Ordinal) < 0)
        {
            return false;
        }

        return OverlapsLoose(find.Interests, [tag]) && OverlapsLoose(find.LookingFor, [tag]);
    }

    public static bool FitsGroup(SceneGroup group, PeopleFindState find, string search)
    {
        if (search.Length > 0 &&
            group.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
            group.Tag.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (find.Mode == 1 || find.Nearby.Count > 0 || find.Quick.Contains("Nearby"))
        {
            if (!group.Nearby)
            {
                return false;
            }
        }

        return OverlapsLoose(find.Interests, [group.Tag, group.Name]) &&
               OverlapsLoose(find.LookingFor, [group.Tag, group.Name]);
    }

    public static PeopleCard FromPerson(ScenePerson person) =>
        new(person.Id, person.GateId, person.Name, person.Handle, person.World, DataCenterOf(person.World),
            string.Empty, [], string.Empty, person.Race, person.Intents, person.Tags, person.Line, string.Empty,
            [], string.Empty, person.Online ? "Online" : string.Empty, person.Online, "Variable", string.Empty,
            string.Empty, string.Empty, person.Relationship, 0,
            person.Intents.Contains("Dating", StringComparer.OrdinalIgnoreCase), true, person.AvatarUrl,
            person.Gender, person.Sexuality, person.DmsOpen ?? true, person.NightOnly, person.PlusMember,
            person.TimeZoneId, VybeLaneMap.Seed(person.GateId.Length > 0 ? person.GateId : person.Name));

    private static bool TryAuthor(PearlPost post, PeopleCard[] deck, VybeState state, out PeopleCard card)
    {
        for (var index = 0; index < deck.Length; index++)
        {
            var hit = deck[index];
            if (hit.GateId.Length > 0 &&
                string.Equals(hit.GateId, post.AuthorId, StringComparison.OrdinalIgnoreCase) ||
                post.AuthorName.Length > 0 &&
                string.Equals(hit.Name, post.AuthorName, StringComparison.OrdinalIgnoreCase))
            {
                card = hit;
                return true;
            }
        }

        if (post.AuthorId.Length > 0 && state.TryFindGate(post.AuthorId, out var person) ||
            post.AuthorName.Length > 0 && state.TryFindName(post.AuthorName, out person))
        {
            card = FromPerson(person);
            return true;
        }

        card = default;
        return false;
    }

    private static bool HitsSearch(string search, params string[] fields)
    {
        if (search.Length == 0)
        {
            return true;
        }

        for (var index = 0; index < fields.Length; index++)
        {
            if (fields[index].IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool PostMarksHit(PearlPost post, string needle)
    {
        if (needle.Length == 0)
        {
            return true;
        }

        var marks = VybePostTags.Collect(post);
        return OverlapsLoose([needle.Trim().TrimStart('#')], marks);
    }

    private static bool OverlapsLoose(List<string> need, IReadOnlyList<string> have)
    {
        if (need.Count == 0)
        {
            return true;
        }

        for (var index = 0; index < need.Count; index++)
        {
            var want = VybePostTags.Normalize(need[index]);
            if (want.Length == 0)
            {
                continue;
            }

            for (var inner = 0; inner < have.Count; inner++)
            {
                var got = VybePostTags.Normalize(have[inner]);
                if (got.Length > 0 &&
                    (got.IndexOf(want, StringComparison.Ordinal) >= 0 ||
                     want.IndexOf(got, StringComparison.Ordinal) >= 0))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static int Score(PeopleCard card, PeopleFindState find, string homeWorld, string homeDc,
        IReadOnlyCollection<string> mine, int[]? mineLanes = null)
    {
        var score = 62 + SharedCount(card.Interests, mine) * 6;
        if (string.Equals(card.World, homeWorld, StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }
        else if (string.Equals(card.DataCenter, homeDc, StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        if (card.Online)
        {
            score += 4;
        }

        if (find.Mode == 4 && card.RpInterest is "Regular RP" or "Heavy RP")
        {
            score += 5;
        }

        var lanes = VybeLaneMap.Match(card.Pulse, find.LaneWant, mineLanes);
        if (lanes > 0)
        {
            score += lanes / 6;
        }

        return Math.Clamp(score, 58, 99);
    }

    public static string[] Reasons(PeopleCard card, string homeWorld, string homeDc, IReadOnlyCollection<string> mine,
        PeopleFindState? find = null, int[]? mineLanes = null)
    {
        var rows = new List<string>();
        var shared = SharedCount(card.Interests, mine);
        if (shared > 0)
        {
            rows.Add(shared + " shared interests");
        }

        if (string.Equals(card.World, homeWorld, StringComparison.OrdinalIgnoreCase))
        {
            rows.Add("Same World");
        }
        else if (string.Equals(card.DataCenter, homeDc, StringComparison.OrdinalIgnoreCase))
        {
            rows.Add("Same Data Center");
        }

        if (HasAny(card.LookingFor, "RP Partner") || card.RpInterest is "Casual RP" or "Regular RP" or "Heavy RP")
        {
            rows.Add("Both looking for RP");
        }

        if (card.PlayTime is "Late Night")
        {
            rows.Add("Both active late night");
        }

        if (HasAny(card.Interests, "Venues", "Nightlife", "DJ Events", "Clubs"))
        {
            rows.Add("Both attend venue events");
        }

        if (card.Online)
        {
            rows.Add("Online now");
        }

        var lanes = VybeLaneMap.Match(card.Pulse, find?.LaneWant, mineLanes);
        if (lanes >= 40)
        {
            rows.Add("Role map " + lanes.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%");
        }

        return rows.Count == 0 ? ["Suggested for you"] : rows.ToArray();
    }

    public static int SharedCount(string[] theirs, IReadOnlyCollection<string> mine)
    {
        var count = 0;
        for (var index = 0; index < theirs.Length; index++)
        {
            if (mine.Contains(theirs[index]))
            {
                count++;
            }
        }

        return count;
    }

    public static IReadOnlyCollection<string> Mine(VybeState state) =>
        state.Tags.Count > 0 ? state.Tags : new[] { "Music", "GPOSE", "RP", "Venues", "Friends" };

    public static string HeartWord(PeopleCard card, bool datingOn) =>
        datingOn && card.DatingOn ? "Interested" : "Connect";

    private static PeopleCard Card(HostPaths paths, string key, string name, string handle, string world, string job,
        string role, string race, string[] looking, string[] interests, string bio, string rp, string[] rpTypes,
        string walk, string activity, bool online, string play, string comm, string social, string dating,
        string status, int age, bool datingOn, bool nearby, string face = "", bool plusOnly = false)
    {
        var gate = "demo:" + key;
        var facts = FaceFacts(key);
        return new PeopleCard(VybeState.StableId(gate), gate, name, handle, world, DataCenterOf(world), job,
            [job], role, race, looking, interests, bio, rp, rpTypes, walk, activity, online, play, comm, social,
            dating, status, age, datingOn, nearby, VybeDemo.FaceOf(paths, face.Length > 0 ? face : key),
            facts.Gender, facts.Sexuality, facts.DmsOpen, plusOnly, plusOnly || PlusFaces(key),
            WorldZones.PickFor(key), VybeLaneMap.Seed(key));
    }

    private static bool PlusFaces(string key) =>
        key is "luna" or "raven" or "novale" or "velvet" or "hex" or "noir" or "ember";

    private static (string Gender, string Sexuality, bool DmsOpen) FaceFacts(string key) =>
        key switch
        {
            "luna" => ("Female", "Bi", true),
            "ace" => ("Male", "Gay", true),
            "kairo" => ("Male", "Bi", false),
            "nyx" => ("Nonbinary", "Pan", true),
            "raven" => ("Female", "Lesbian", true),
            "vex" => ("Genderfluid", "Pan", true),
            "echo" => ("Female", "Straight", false),
            "novale" => ("Female+", "Bi", true),
            "sol" => ("Male", "Straight", true),
            "wren" => ("Nonbinary", "Asexual", true),
            "iris" => ("Female", "Demisexual", false),
            "jett" => ("Male+", "Gay", true),
            "velvet" => ("Female", "Bi", true),
            "hex" => ("Male", "Pan", true),
            "noir" => ("Nonbinary", "Bi", true),
            "ember" => ("Female", "Pan", true),
            _ => (string.Empty, string.Empty, true),
        };

    private static bool Has(string chip, PeopleFindState find) => find.Quick.Contains(chip);

    private static bool HasAny(string[] values, params string[] need)
    {
        for (var index = 0; index < need.Length; index++)
        {
            for (var inner = 0; inner < values.Length; inner++)
            {
                if (string.Equals(values[inner], need[index], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsAny(List<string> need, string have) =>
        need.Count == 0 || need.Contains(have);

    private static bool ContainsAny(List<string> need, string[] have)
    {
        if (need.Count == 0)
        {
            return true;
        }

        for (var index = 0; index < have.Length; index++)
        {
            if (need.Contains(have[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool InPlace(PeopleFindState find, PeopleCard card, string homeWorld, string homeDc)
    {
        if (find.DataCenters.Count > 0)
        {
            var ok = false;
            for (var index = 0; index < find.DataCenters.Count; index++)
            {
                var pick = find.DataCenters[index];
                if (pick == "My Data Center" && string.Equals(card.DataCenter, homeDc, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pick, card.DataCenter, StringComparison.OrdinalIgnoreCase))
                {
                    ok = true;
                    break;
                }
            }

            if (!ok)
            {
                return false;
            }
        }

        if (find.Worlds.Count == 0)
        {
            return true;
        }

        for (var index = 0; index < find.Worlds.Count; index++)
        {
            var pick = find.Worlds[index];
            if (pick == "My World" && string.Equals(card.World, homeWorld, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pick, card.World, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool InActivity(List<string> need, PeopleCard card)
    {
        if (need.Count == 0 || need.Contains("Any"))
        {
            return true;
        }

        if (need.Contains("Online Now") && card.Online)
        {
            return true;
        }

        if (need.Contains("Active Today") && card.Activity is "Online" or "Today")
        {
            return true;
        }

        return need.Contains("Active This Week") && card.Activity is "Online" or "Today" or "Week";
    }

    private static bool MeetsShared(string floor, int shared) => floor switch
    {
        "1+" => shared >= 1,
        "2+" => shared >= 2,
        "3+" => shared >= 3,
        "Highly Compatible" => shared >= 4,
        _ => true,
    };
}
