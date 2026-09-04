namespace Linkpearl.Badges;

public enum BadgeKind : byte
{
    Earned = 0,
    Cosmetic = 1,
}

public enum BadgeCategory : byte
{
    Linkpearl = 0,
    Social = 1,
    Music = 2,
    Events = 3,
    Community = 4,
    Achievements = 5,
    Patron = 6,
    Development = 7,
    Founder = 8,
    Historical = 9,
    Special = 10,
    Personality = 11,
}

public enum BadgeMark : byte
{
    Pearl = 0,
    Founder = 1,
    Moon = 2,
    Moth = 3,
    Skull = 4,
    Camera = 5,
    Forge = 6,
    Note = 7,
    Ticket = 8,
    People = 9,
    Heart = 10,
    Hourglass = 11,
    Spark = 12,
}

public readonly struct BadgeSpec
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Source { get; init; }

    public required BadgeCategory Category { get; init; }

    public required BadgeKind Kind { get; init; }

    public required BadgeMark Mark { get; init; }

    public string IconAsset { get; init; }

    public int Price { get; init; }

    public bool Purchasable { get; init; }

    public DateTimeOffset? HistoricalUntil { get; init; }

    public bool IsHistorical => HistoricalUntil is not null;

    public bool ObtainableAt(DateTimeOffset now)
    {
        if (HistoricalUntil is { } until && now > until)
        {
            return false;
        }

        return true;
    }
}

public readonly struct BadgeOwn
{
    public required string Id { get; init; }

    public required long EarnedAtUnix { get; init; }

    public required string Source { get; init; }
}

public static class BadgeCatalog
{
    public const int SlotCount = 5;

    public static readonly BadgeSpec[] All =
    {
        Spec("linkpearl", "Linkpearl", "Carried a pearl from the first lighting.",
            "Issued with the communicator.", BadgeCategory.Linkpearl, BadgeKind.Earned, BadgeMark.Pearl, false, null),
        Spec("founder", "Founder", "One of the first 500 to sign in to Pearlgate.",
            "First 500 Pearlgate accounts.", BadgeCategory.Founder, BadgeKind.Earned, BadgeMark.Founder, false, null,
            "Badges/founder.png"),
        Spec("night-watch", "Night Watch", "Kept the glass lit after dark.",
            "Personality.", BadgeCategory.Personality, BadgeKind.Earned, BadgeMark.Moon, false, null),
        Spec("moth", "Mothlight", "Drawn to other pearls in the dark.",
            "Community.", BadgeCategory.Community, BadgeKind.Earned, BadgeMark.Moth, false, null),
        Spec("reaper", "Reaper", "Walked the void as a Reaper.",
            "Job record.", BadgeCategory.Achievements, BadgeKind.Earned, BadgeMark.Skull, false, null),
        Spec("still", "Still", "Took a still and kept it.",
            "Participation.", BadgeCategory.Special, BadgeKind.Earned, BadgeMark.Camera, false, null),
        Spec("forge", "Forge", "Helped shape the communicator.",
            "Development.", BadgeCategory.Development, BadgeKind.Earned, BadgeMark.Forge, false, null),
        Spec("chorus", "Chorus", "Sat with a live set.",
            "Music.", BadgeCategory.Music, BadgeKind.Earned, BadgeMark.Note, false, null),
        Spec("muster", "Muster", "Showed for a gathering.",
            "Events.", BadgeCategory.Events, BadgeKind.Earned, BadgeMark.Ticket, false, null),
        Spec("circle", "Circle", "Kept a thread with friends.",
            "Social.", BadgeCategory.Social, BadgeKind.Earned, BadgeMark.People, false, null),
        Spec("patron", "Patron", "A rose for those who support the work.",
            "Patreon supporters. Never for sale.", BadgeCategory.Patron, BadgeKind.Cosmetic, BadgeMark.Heart, false,
            null, "Badges/patron.png"),
        Spec("relic", "First Light", "Present for the first lighting window.",
            "Historical. Unobtainable after the window closed.", BadgeCategory.Historical, BadgeKind.Earned,
            BadgeMark.Hourglass, false, new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        Spec("spark", "Spark", "A limited mark from a short season.",
            "Limited. Not for sale.", BadgeCategory.Special, BadgeKind.Earned, BadgeMark.Spark, false,
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)),
        Shop("viewfinder", "Viewfinder", "Badges/shop_viewfinder.png",
            BadgeCategory.Personality, 250, "Framed. Focused. Watching."),
        Shop("night-moon", "Night Moon", "Badges/shop_night-moon.png",
            BadgeCategory.Personality, 250, "A small moon for the late glass."),
        Shop("pearl-moth", "Pearl Moth", "Badges/shop_pearl-moth.png",
            BadgeCategory.Social, 250, "Drawn to other pearls in the dark."),
        Shop("afk-cat", "AFK Cat", "Badges/shop_afk-cat.png",
            BadgeCategory.Personality, 250, "Present. Headphones on. Mostly."),
        Shop("chaos-horn", "Chaos Horn", "Badges/shop_chaos-horn.png",
            BadgeCategory.Personality, 250, "A little trouble, worn proudly."),
        Shop("sun-bird", "Sun Bird", "Badges/shop_sun-bird.png",
            BadgeCategory.Personality, 250, "First light, first song."),
        Shop("saturn-heart", "Saturn Heart", "Badges/shop_saturn-heart.png",
            BadgeCategory.Social, 250, "Feelings, with a ring around them."),
        Shop("megaphone", "Megaphone", "Badges/shop_megaphone.png",
            BadgeCategory.Social, 250, "Words were had. Many of them."),
        Shop("cloud-moon", "Cloud Moon", "Badges/shop_cloud-moon.png",
            BadgeCategory.Community, 500, "The late bells belong to you."),
        Shop("day-sun", "Day Sun", "Badges/shop_day-sun.png",
            BadgeCategory.Community, 500, "Always walking toward the next dawn."),
        Shop("reaper-hood", "Reaper Hood", "Badges/shop_reaper-hood.png",
            BadgeCategory.Community, 500, "The void, kept as a calling card."),
        Shop("paw-glass", "Paw Glass", "Badges/shop_paw-glass.png",
            BadgeCategory.Community, 500, "Someone is being looked for."),
        Shop("cape-crown", "Cape Crown", "Badges/shop_cape-crown.png",
            BadgeCategory.Community, 500, "A little ceremony, for no reason."),
        Shop("coin-pouch", "Coin Pouch", "Badges/shop_coin-pouch.png",
            BadgeCategory.Community, 500, "If it sparkles, it was already yours."),
        Shop("heart-camera", "Heart Camera", "Badges/shop_heart-camera.png",
            BadgeCategory.Community, 500, "The stills folder never sleeps."),
        Shop("glamour-bow", "Glamour Bow", "Badges/shop_glamour-bow.png",
            BadgeCategory.Community, 500, "Glamour first. Consequences later."),
        Shop("gear-wrench", "Gear Wrench", "Badges/shop_gear-wrench.png",
            BadgeCategory.Special, 900, "The pattern was the point."),
        Shop("bolt-ring", "Bolt Ring", "Badges/shop_bolt-ring.png",
            BadgeCategory.Special, 900, "Saved it. Spent it. Worth it."),
        Shop("healer-heart", "Healer Heart", "Badges/shop_healer-heart.png",
            BadgeCategory.Special, 900, "They stood in the fire. You stood them back up."),
        Shop("tank-shield", "Tank Shield", "Badges/shop_tank-shield.png",
            BadgeCategory.Special, 900, "The wall, and the courtesy that comes with it."),
        Shop("dps-mind", "DPS Mind", "Badges/shop_dps-mind.png",
            BadgeCategory.Special, 900, "If it can be hit, it will be."),
        Shop("forge-anvil", "Forge Anvil", "Badges/shop_forge-anvil.png",
            BadgeCategory.Special, 900, "Made, not found."),
        Shop("gather-satchel", "Gather Satchel", "Badges/shop_gather-satchel.png",
            BadgeCategory.Special, 900, "One more node. Then we go. Then one more."),
        Shop("map-compass", "Map Compass", "Badges/shop_map-compass.png",
            BadgeCategory.Special, 900, "The atlas is a lifestyle."),
        Shop("gold-moon", "Gold Moon", "Badges/shop_gold-moon.png",
            BadgeCategory.Music, 1400, "When the set runs past the last bell."),
        Shop("star-crown", "Star Crown", "Badges/shop_star-crown.png",
            BadgeCategory.Music, 1400, "Center lights. No apology."),
        Shop("bass-speaker", "Bass Speaker", "Badges/shop_bass-speaker.png",
            BadgeCategory.Music, 1400, "Felt it in the floor."),
        Shop("gold-ticket", "Gold Ticket", "Badges/shop_gold-ticket.png",
            BadgeCategory.Music, 1400, "Close enough to see the hands."),
        Shop("thorn-rose", "Thorn Rose", "Badges/shop_thorn-rose.png",
            BadgeCategory.Music, 1400, "Pretty. Pointed."),
        Shop("orbit-star", "Orbit Star", "Badges/shop_orbit-star.png",
            BadgeCategory.Music, 1400, "A little night, worn as jewelry."),
        Shop("gem-heart", "Gem Heart", "Badges/shop_gem-heart.png",
            BadgeCategory.Music, 1400, "Aether, dressed for the evening."),
        Shop("aether-flame", "Aether Flame", "Badges/shop_aether-flame.png",
            BadgeCategory.Music, 1400, "Bright enough to keep the dark honest."),
        Shop("orbit-crown", "Orbit Crown", "Badges/shop_orbit-crown.png",
            BadgeCategory.Special, 2200, "Light, worn like a title."),
        Shop("void-hood", "Void Hood", "Badges/shop_void-hood.png",
            BadgeCategory.Special, 2200, "The long nights, kept."),
        Shop("ice-moon", "Ice Moon", "Badges/shop_ice-moon.png",
            BadgeCategory.Special, 2200, "The late court is still a court."),
        Shop("astral-crystal", "Astral Crystal", "Badges/shop_astral-crystal.png",
            BadgeCategory.Special, 2200, "Between stars, on purpose."),
        Shop("solar-pearl", "Solar Pearl", "Badges/shop_solar-pearl.png",
            BadgeCategory.Special, 2200, "That one stretch of sky, kept."),
        Shop("star-world", "Star World", "Badges/shop_star-world.png",
            BadgeCategory.Special, 2200, "Downed. Stood. Repeated."),
        Shop("winged-crest", "Winged Crest", "Badges/shop_winged-crest.png",
            BadgeCategory.Special, 2200, "The story is still being told."),
        Shop("sovereign-pearl", "Sovereign Pearl", "Badges/shop_sovereign-pearl.png",
            BadgeCategory.Special, 2200, "The glass, and the one who keeps it."),
    };

    public static readonly string[] CategoryLabels =
    {
        "Linkpearl", "Social", "Music", "Events", "Community", "Achievements", "Patreon / Supporter", "Development",
        "Founder", "Historical", "Special / Limited", "Personality",
    };

    public static BadgeSpec? Find(string id)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index].Id, id, StringComparison.Ordinal))
            {
                return All[index];
            }
        }

        return null;
    }

    public static string CategoryName(BadgeCategory category) =>
        (int)category < CategoryLabels.Length ? CategoryLabels[(int)category] : category.ToString();

    public static IEnumerable<BadgeSpec> ShopItems()
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (All[index].Purchasable && All[index].Kind == BadgeKind.Cosmetic &&
                All[index].IconAsset.Length > 0)
            {
                yield return All[index];
            }
        }
    }

    public static IEnumerable<BadgeSpec> ExclusiveItems()
    {
        if (Find("founder") is { } founder)
        {
            yield return founder;
        }

        if (Find("patron") is { } patron)
        {
            yield return patron;
        }
    }

    public static IEnumerable<BadgeSpec> InCategory(BadgeCategory category)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (All[index].Category == category)
            {
                yield return All[index];
            }
        }
    }

    private static BadgeSpec Spec(string id, string name, string description, string source, BadgeCategory category,
        BadgeKind kind, BadgeMark mark, bool purchasable, DateTimeOffset? until, string icon = "", int price = 0) =>
        new()
        {
            Id = id,
            Name = name,
            Description = description,
            Source = source,
            Category = category,
            Kind = kind,
            Mark = mark,
            IconAsset = icon,
            Price = price,
            Purchasable = purchasable,
            HistoricalUntil = until,
        };

    private static BadgeSpec Shop(string id, string name, string icon, BadgeCategory category, int price,
        string description) =>
        Spec(id, name, description, "Purchased with Pearls.", category, BadgeKind.Cosmetic, BadgeMark.Pearl, true, null,
            icon, price);
}
