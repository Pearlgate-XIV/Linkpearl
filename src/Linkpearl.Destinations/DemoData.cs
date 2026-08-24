using System.Linq;

namespace Linkpearl.Destinations;

// Realistic placeholder content standing in for real backend data, matching the shape the
// design reference calls for. Every field here is fabricated for demonstration; nothing in
// this file talks to a network. See docs/STATUS.md for exactly which systems are placeholder.
internal static class DemoData
{
    internal readonly record struct UpNextEvent(string Title, string Venue, string World, string Time,
        string StartsIn, int InterestedFriends);

    internal readonly record struct MessagePreview(string Sender, string Body, string When, bool Online);

    internal readonly record struct PartyStatus(string Title, int Filled, int Capacity, string Need);

    internal readonly record struct RetainerStatus(string Task, string Detail);

    internal readonly record struct MarketWatch(string Item, float PercentChange, long Price);

    internal readonly record struct UpcomingEvent(string Title, string StartsIn);

    internal readonly record struct QuickAction(string Glyph, string Label, int Badge);

    internal readonly record struct SocialPost(string Author, string Body, string When, int Likes, int Comments,
        bool IsEventShare);

    internal readonly record struct ExploreCard(string Kind, string Title, string Detail, string Meta);

    internal readonly record struct ProfileStat(string Label, string Value);

    internal readonly record struct SearchEntry(string Kind, string Title, string Subtitle);

    internal static string CharacterName => "Cas";

    internal static string CharacterTitle => "Warrior of Light";

    internal static string World => "Mateus";

    internal static string Weather => "Clear Skies";

    internal static string Location => "The Lavender Beds";

    internal static UpNextEvent UpNext => new("Moonlight Masquerade", "The Velvet Rose", "Mateus", "10:00 PM",
        "in 2h 14m", 4);

    internal static MessagePreview LatestMessage => new("Y'shtola", "Are you still coming tonight?", "Now", true);

    internal static PartyStatus Party => new("M4S Learning Party", 6, 8, "2 DPS needed");

    internal static RetainerStatus Retainer => new("Venture Complete", "Quick Exploration");

    internal static int FriendsOnline => 12;

    internal static MarketWatch Market => new("Dark Matter", 0.23f, 85_999);

    internal static UpcomingEvent Event => new("Community Glamour Contest", "in 37m");

    internal static IReadOnlyList<QuickAction> QuickActions { get; } = new[]
    {
        new QuickAction("⚔", "Duty Ready", 0),
        new QuickAction("💬", "Tell from Y'shtola", 0),
        new QuickAction("🗡", "Party Invite", 1),
        new QuickAction("👤", "Friend Request", 0),
    };

    internal static IReadOnlyList<SocialPost> Feed { get; } = new[]
    {
        new SocialPost("Sora Morningstar", "Finally finished the glam I've been working on! ✨", "2m", 24, 8, false),
        new SocialPost("The Velvet Rose", "Moonlight Masquerade — tonight, 10:00 PM. 4 friends interested.", "12m",
            31, 6, true),
        new SocialPost("Raive'h Tia", "Cleared P9S with the static! What a run o/", "30m", 31, 6, false),
    };

    internal static IReadOnlyList<ExploreCard> ExploreFeed { get; } = new[]
    {
        new ExploreCard("VENUE", "The Velvet Rose", "Nightclub · RP · Mateus, Lavender Beds W18 P32", "OPEN NOW"),
        new ExploreCard("ACTIVITY", "M4S Learning Party", "6 / 8 · 2 DPS needed", string.Empty),
        new ExploreCard("EVENT", "Community Glamour Contest", "Limsa Lominsa · Lower Decks", "Starts in 45 minutes"),
        new ExploreCard("ACTIVITY", "Treasure Maps", "5 / 8", "Starting in 20 minutes"),
    };

    internal static IReadOnlyList<ProfileStat> ProfileStats { get; } = new[]
    {
        new ProfileStat("Achievements", "403 / 600"),
        new ProfileStat("Housing", "Shirogane · Ward 3 Plot 30"),
    };

    internal static IReadOnlyList<SearchEntry> RecentSearches { get; } = new[]
    {
        new SearchEntry("Player", "Y'shtola", string.Empty),
        new SearchEntry("Venue", "The Velvet Rose", string.Empty),
        new SearchEntry("Activity", "M4S", string.Empty),
        new SearchEntry("Item", "Dark Matter", string.Empty),
    };

    internal static IReadOnlyList<SearchEntry> SearchIndex { get; } = new[]
    {
        new SearchEntry("Player", "Y'shtola", "Player"),
        new SearchEntry("Venue", "The Velvet Rose", "Venue · Nightclub"),
        new SearchEntry("Activity", "M4S Learning Party", "Activity"),
        new SearchEntry("Item", "Dark Matter", "Market Item"),
        new SearchEntry("Activity", "Raid Roulettes", "Activity"),
        new SearchEntry("Duty", "Eden's Promise: Eternity (Savage)", "Duty"),
    };

    internal static IReadOnlyList<SearchEntry> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<SearchEntry>();
        }

        return SearchIndex.Where(entry => entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
