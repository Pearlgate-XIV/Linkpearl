using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Vybe;

internal enum SocialMode : byte
{
    Daylight = 0,
    AfterDark = 1,
}

internal enum NightTab : byte
{
    Home = 0,
    Discover = 1,
    Gallery = 2,
    Messages = 3,
    Profile = 4,
}

internal enum FeedPick : byte
{
    ForYou = 0,
    Following = 1,
    Groups = 2,
    Plus = 3,
}

internal enum NightPage : byte
{
    Tabs = 0,
    PickMode = 1,
    Gate = 2,
    Rules = 3,
    OnboardIdentity = 4,
    OnboardIntent = 5,
    OnboardAbout = 6,
    OnboardReady = 7,
    Person = 8,
    Post = 9,
    Compose = 10,
    Filters = 11,
    Chat = 12,
    Requests = 13,
    Settings = 14,
    Gallery = 15,
    Likes = 16,
    Blocked = 17,
    FeedWall = 18,
    Search = 19,
    Story = 20,
    StoryCompose = 21,
    Following = 22,
    Followers = 23,
    PhotoPick = 24,
    PlacePhoto = 30,
    PhotoView = 25,
    ShareSend = 26,
    Inbox = 27,
    Alerts = 28,
    Saves = 29,
    Auth = 31,
    AuthLogin = 32,
    AuthCreate = 33,
    StoryComments = 34,
    Club = 35,
    ClubCreate = 36,
    ClubEvent = 37,
    LaneSurvey = 38,
    PostComments = 39,
}

internal enum FilterPole : byte
{
    Neutral = 0,
    Include = 1,
    Exclude = 2,
}

internal sealed class VybeState
{
    public SocialMode Mode { get; set; } = SocialMode.Daylight;

    public bool PickedMode { get; set; }

    public bool Consented { get; set; }

    public bool PlusAgreed { get; set; }

    public bool PlusBlocked { get; set; }

    public bool Onboarded { get; set; }

    public string AccountId { get; set; } = string.Empty;

    public bool HasAccount => AccountId.Length > 0;

    public bool OnAuthSheet =>
        Page is NightPage.Auth or NightPage.AuthLogin or NightPage.AuthCreate;

    [JsonIgnore]
    public VybeBook? Ledger { get; set; }

    [JsonIgnore]
    public string EnterHandle { get; set; } = string.Empty;

    [JsonIgnore]
    public string EnterSecret { get; set; } = string.Empty;

    [JsonIgnore]
    public string JoinName { get; set; } = string.Empty;

    [JsonIgnore]
    public string JoinTitle { get; set; } = string.Empty;

    [JsonIgnore]
    public string JoinHandle { get; set; } = string.Empty;

    [JsonIgnore]
    public string JoinSecret { get; set; } = string.Empty;

    [JsonIgnore]
    public string JoinAgain { get; set; } = string.Empty;

    [JsonIgnore]
    public string AuthNote { get; set; } = string.Empty;

    [JsonIgnore]
    public bool DropConfirm { get; set; }

    [JsonIgnore]
    public string DropSeatId { get; set; } = string.Empty;

    public bool Discoverable { get; set; } = true;

    public string DisplayName { get; set; } = string.Empty;

    public string Honorific { get; set; } = string.Empty;

    public bool UsesHandsetProfile { get; set; } = true;

    public bool UsesHandsetIdentity { get; set; } = true;

    public string ProfileFacePath { get; set; } = string.Empty;

    public string ProfileBannerPath { get; set; } = string.Empty;

    public float FaceZoom { get; set; } = 1f;

    public float FaceFocusX { get; set; } = 0.5f;

    public float FaceFocusY { get; set; } = 0.5f;

    public float BannerZoom { get; set; } = 1f;

    public float BannerFocusX { get; set; } = 0.5f;

    public float BannerFocusY { get; set; } = 0.5f;

    public bool PickingBanner { get; set; }

    public string Handle { get; set; } = string.Empty;

    public string Pronouns { get; set; } = string.Empty;

    public string Race { get; set; } = string.Empty;

    public string About { get; set; } = string.Empty;

    public string Caption { get; set; } = string.Empty;

    public string Draft { get; set; } = string.Empty;

    public string CommentDraft { get; set; } = string.Empty;

    public string Search { get; set; } = string.Empty;

    public string Hashtag { get; set; } = string.Empty;

    [JsonIgnore]
    public string GalleryQuery { get; set; } = string.Empty;

    public string OwnStory { get; set; } = string.Empty;

    public bool OwnStoryPermanent { get; set; }

    public long OwnStoryAtUnix { get; set; }

    public string OwnStoryPlace { get; set; } = string.Empty;

    [JsonIgnore]
    public bool DraftStoryPermanent { get; set; }

    [JsonIgnore]
    public bool StoryMoreOpen { get; set; }

    public bool AudienceEveryone { get; set; } = true;

    public bool DraftPlus { get; set; }

    [JsonIgnore]
    public bool DraftStory { get; set; }

    [JsonIgnore]
    public ContentRating DraftRating { get; set; } = ContentRating.Sfw;

    [JsonIgnore]
    public List<string> DraftDescriptors { get; } = new();

    [JsonIgnore]
    public bool DraftDescOpen { get; set; }

    [JsonIgnore]
    public ComposeSheet DraftSheet { get; set; }

    [JsonIgnore]
    public bool DraftSfwOk { get; set; }

    [JsonIgnore]
    public List<string> DraftTags { get; } = new();

    [JsonIgnore]
    public string DraftTagDraft { get; set; } = string.Empty;

    public List<PearlPost> Posted { get; } = new();

    public FeedPick FeedPick { get; set; } = FeedPick.ForYou;

    public bool FeedEveryone
    {
        get => FeedPick != FeedPick.Following;
        set => FeedPick = value ? FeedPick.ForYou : FeedPick.Following;
    }

    public NightTab Tab { get; set; }

    public NightPage Page { get; set; } = NightPage.Tabs;

    public NightPage ReturnTo { get; set; }

    public int PersonIndex { get; set; }

    public int PostIndex { get; set; }

    public string PostKey { get; set; } = string.Empty;

    public string PersonKey { get; set; } = string.Empty;

    public string QuoteOf { get; set; } = string.Empty;

    public string SharePostId { get; set; } = string.Empty;

    [JsonIgnore]
    public string DropPostId { get; set; } = string.Empty;

    public string ViewMedia { get; set; } = string.Empty;

    public string StoryMedia { get; set; } = string.Empty;

    public bool PickingAvatar { get; set; }

    public List<string> DraftMedia { get; } = new();

    public int ChatIndex { get; set; }

    public string ChatKey { get; set; } = string.Empty;

    public int StoryIndex { get; set; } = -1;

    public int DiscoverPane { get; set; }

    [JsonIgnore]
    public int MessagePane { get; set; }

    [JsonIgnore]
    public string ReportKind { get; set; } = "user";

    [JsonIgnore]
    public bool ReportOpen { get; set; }

    [JsonIgnore]
    public bool ReportFresh { get; set; }

    [JsonIgnore]
    public int ReportReason { get; set; }

    [JsonIgnore]
    public string ReportDetail { get; set; } = string.Empty;

    [JsonIgnore]
    public string ReportTarget { get; set; } = string.Empty;

    [JsonIgnore]
    public string ReportTitle { get; set; } = string.Empty;

    [JsonIgnore]
    public bool DiscoverSearchOpen { get; set; }

    [JsonIgnore]
    public bool GroupHuntOpen { get; set; } = true;

    [JsonIgnore]
    public string GroupQuery { get; set; } = string.Empty;

    [JsonIgnore]
    public GroupLane GroupLane { get; set; } = GroupLane.Suggested;

    [JsonIgnore]
    public bool PeoplePlus { get; set; }

    [JsonIgnore]
    public bool GalleryPlus { get; set; }

    [JsonIgnore]
    public bool DiscoverPostPlus { get; set; }

    [JsonIgnore]
    public bool DiscoverHashPlus { get; set; }

    [JsonIgnore]
    public string HashQuery { get; set; } = string.Empty;

    public void HidePlusLanes()
    {
        if (FeedPick == FeedPick.Plus)
        {
            FeedPick = FeedPick.ForYou;
        }

        PeoplePlus = false;
        GalleryPlus = false;
        DiscoverPostPlus = false;
        DiscoverHashPlus = false;
        if (GroupLane == GroupLane.Plus)
        {
            GroupLane = GroupLane.Suggested;
        }

        if (ActingAsClubId.Length > 0 && VybeClubs.TryFind(this, ActingAsClubId, out var club) && club.PlusOnly)
        {
            ActingAsClubId = string.Empty;
        }
    }

    public HashSet<string> JoinedGroups { get; } = new(StringComparer.Ordinal);

    public List<VybeClub> Clubs { get; } = new();

    public string ActingAsClubId { get; set; } = string.Empty;

    [JsonIgnore]
    public string ClubKey { get; set; } = string.Empty;

    [JsonIgnore]
    public int ClubPane { get; set; }

    [JsonIgnore]
    public string DraftClubName { get; set; } = string.Empty;

    [JsonIgnore]
    public string DraftClubAbout { get; set; } = string.Empty;

    [JsonIgnore]
    public string DraftClubFace { get; set; } = string.Empty;

    [JsonIgnore]
    public bool DraftClubPlus { get; set; }

    [JsonIgnore]
    public bool PickingClubFace { get; set; }

    [JsonIgnore]
    public string DraftEventTitle { get; set; } = string.Empty;

    [JsonIgnore]
    public int DraftEventWhen { get; set; }

    [JsonIgnore]
    public int HottIndex { get; set; }

    [JsonIgnore]
    public float HottDrag { get; set; }

    [JsonIgnore]
    public float HottPlusDrag { get; set; }

    [JsonIgnore]
    public float StoryDrag { get; set; }

    public int ProfilePane { get; set; }

    public int Vibe { get; set; }

    public float Scroll { get; set; }

    public float HoldMark { get; set; }

    public float Wash { get; set; }

    public bool WashToNight { get; set; }

    public int NextPostId { get; set; } = 100;

    public int FollowersSeed { get; set; }

    public int GateFollowers { get; set; }

    public int GateFollowing { get; set; }

    public bool SignedIn { get; set; }

    public List<ScenePerson> Roster { get; } = new();

    public HashSet<int> LiveStories { get; } = new();

    public HashSet<int> HiddenPosts { get; } = new();

    public List<string> Genders { get; } = new();

    public List<string> Sexualities { get; } = new();

    public List<string> Intents { get; } = new();

    public List<string> Tags { get; } = new();

    public string Relationship { get; set; } = "Rather not say";

    public bool DmsOpen { get; set; } = true;

    public HashSet<int> Connected { get; } = new();

    public HashSet<int> Requested { get; } = new();

    public HashSet<int> Incoming { get; } = new();

    public HashSet<int> Blocked { get; } = new();

    public Dictionary<int, string> BlockedLabels { get; } = new();

    public HashSet<int> LikedPosts { get; } = new();

    public HashSet<string> Hearts { get; } = new(StringComparer.Ordinal);

    public HashSet<int> LikedPeople { get; } = new();

    public HashSet<int> SavedPosts { get; } = new();

    public HashSet<string> KeptPosts { get; } = new(StringComparer.Ordinal);

    public HashSet<string> HiddenPearls { get; } = new(StringComparer.Ordinal);

    public HashSet<string> KeptShots { get; } = new(StringComparer.Ordinal);

    public int SavePane { get; set; }

    public bool ToggleKeptPost(string id, IEnumerable<string> shots)
    {
        if (id.Length == 0)
        {
            return KeptPosts.Contains(id);
        }

        if (!KeptPosts.Add(id))
        {
            KeptPosts.Remove(id);
            foreach (var shot in shots)
            {
                if (shot.Length > 0)
                {
                    KeptShots.Remove(shot);
                }
            }

            return false;
        }

        foreach (var shot in shots)
        {
            if (shot.Length > 0)
            {
                KeptShots.Add(shot);
            }
        }

        return true;
    }

    public bool ToggleKeptShot(string url)
    {
        if (url.Length == 0)
        {
            return false;
        }

        if (!KeptShots.Add(url))
        {
            KeptShots.Remove(url);
            return false;
        }

        return true;
    }

    public HashSet<int> Reposted { get; } = new();

    public HashSet<int> ViewedStories { get; } = new();

    [JsonIgnore]
    public HashSet<int> StoryHearts { get; } = new();

    public List<PearlComment> OwnStoryComments { get; } = new();

    [JsonIgnore]
    public Dictionary<string, List<PearlComment>> PostThreads { get; } = new(StringComparer.Ordinal);

    public List<StoryThread> GuestStoryThreads { get; } = new();

    public List<PearlPost> StoryReposts { get; } = new();

    public List<PearlPost> StorySaves { get; } = new();

    public bool HasOwnStory =>
        (OwnStory.Length > 0 || StoryMedia.Length > 0) && !OwnStoryExpired;

    public bool OwnStoryExpired =>
        !OwnStoryPermanent && OwnStoryAtUnix > 0 &&
        DateTimeOffset.UtcNow.ToUnixTimeSeconds() - OwnStoryAtUnix >= 24 * 60 * 60;

    public List<PearlComment> StoryCommentsFor(int personId)
    {
        if (personId < 0)
        {
            return OwnStoryComments;
        }

        for (var index = 0; index < GuestStoryThreads.Count; index++)
        {
            if (GuestStoryThreads[index].PersonId == personId)
            {
                return GuestStoryThreads[index].Lines ??= new List<PearlComment>();
            }
        }

        return new List<PearlComment>();
    }

    public List<PearlComment> PostCommentsFor(string postId)
    {
        var key = (postId ?? string.Empty).Trim();
        if (key.Length == 0)
        {
            return [];
        }

        if (!PostThreads.TryGetValue(key, out var lines))
        {
            lines = new List<PearlComment>();
            PostThreads[key] = lines;
        }

        return lines;
    }

    public void AddPostComment(string postId, string author, string body)
    {
        var key = (postId ?? string.Empty).Trim();
        var text = (body ?? string.Empty).Trim();
        if (key.Length == 0 || text.Length == 0)
        {
            return;
        }

        var bag = PostCommentsFor(key);
        for (var index = 0; index < bag.Count; index++)
        {
            if (string.Equals(bag[index].Author, author, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((bag[index].Body ?? string.Empty).Trim(), text, StringComparison.Ordinal))
            {
                return;
            }
        }

        bag.Add(new PearlComment(author, text, "now", true));
    }

    public void AddStoryComment(int personId, string author, string body)
    {
        var line = new PearlComment(author, body, "now", true);
        if (personId < 0)
        {
            OwnStoryComments.Add(line);
            return;
        }

        StoryThread? thread = null;
        for (var index = 0; index < GuestStoryThreads.Count; index++)
        {
            if (GuestStoryThreads[index].PersonId == personId)
            {
                thread = GuestStoryThreads[index];
                break;
            }
        }

        if (thread is null)
        {
            thread = new StoryThread
            {
                PersonId = personId,
                AtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            GuestStoryThreads.Add(thread);
        }
        else if (thread.AtUnix == 0)
        {
            thread.AtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        thread.Lines ??= new List<PearlComment>();
        thread.Lines.Add(line);
    }

    public void DropExpiredStory()
    {
        DropExpiredGuestThreads();
        if (!OwnStoryExpired)
        {
            return;
        }

        ClearOwnStory();
    }

    public void DropExpiredGuestThreads()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        GuestStoryThreads.RemoveAll(thread =>
            thread.AtUnix > 0 && now - thread.AtUnix >= 24 * 60 * 60);
    }

    public void ClearOwnStory()
    {
        OwnStory = string.Empty;
        StoryMedia = string.Empty;
        OwnStoryPermanent = false;
        OwnStoryAtUnix = 0;
        OwnStoryPlace = string.Empty;
        OwnStoryComments.Clear();
        StoryHearts.Remove(-1);
        if (StoryIndex < 0)
        {
            StoryIndex = -1;
        }
    }

    public List<ScenePost> Mine { get; } = new();

    public List<SceneNote> Notes { get; } = new();

    public Dictionary<int, List<ChatLine>> Threads { get; } = new();

    public HashSet<string> StarredChats { get; } = new(StringComparer.Ordinal);

    public HashSet<string> HiddenChats { get; } = new(StringComparer.Ordinal);

    public Dictionary<int, List<SceneComment>> Comments { get; } = new();

    public Dictionary<string, FilterPole> Filters { get; } = new(StringComparer.Ordinal);

    public bool NearbyDiscovery { get; set; }

    public bool DatingDiscovery { get; set; }

    public bool LaneDone { get; set; }

    public int[] LaneMarks { get; set; } = [];

    [JsonIgnore]
    public int LaneAsk { get; set; }

    [JsonIgnore]
    public int[] LanePicks { get; set; } = [];

    [JsonIgnore]
    public bool LaneMapOpen { get; set; }

    [JsonIgnore]
    public string LaneMapKey { get; set; } = string.Empty;

    public PeopleFindState PeopleFindVybe { get; set; } = new();

    public PeopleFindState PeopleFindPlus { get; set; } = new();

    public PeopleFindState PeopleFind => Night ? PeopleFindPlus : PeopleFindVybe;

    public bool Night => Mode == SocialMode.AfterDark;

    public bool Washing => Wash > 0f;

    public int FollowerCount => SignedIn ? GateFollowers : FollowersSeed;

    public int FollowingCount => SignedIn ? Math.Max(GateFollowing, Connected.Count) : Connected.Count;

    public static VybeState Load(HostPaths paths, string fallbackName)
    {
        var state = new VybeState();
        var path = paths.State("vybe.json");
        if (!File.Exists(path))
        {
            path = paths.State("afterdark.json");
        }

        var fromDisk = false;
        if (File.Exists(path))
        {
            try
            {
                var dto = JsonSerializer.Deserialize<NightSave>(File.ReadAllText(path));
                if (dto is not null)
                {
                    fromDisk = true;
                    state.PlusAgreed = dto.PlusAgreed;
                    state.Consented = state.PlusAgreed;
                    state.Mode = state.PlusAgreed && dto.Night ? SocialMode.AfterDark : SocialMode.Daylight;
                    state.PickedMode = dto.PickedMode;
                    state.Onboarded = dto.Onboarded;
                    state.AccountId = dto.AccountId ?? string.Empty;
                    state.Discoverable = dto.Discoverable;
                    state.DisplayName = dto.DisplayName ?? string.Empty;
                    state.Honorific = dto.Honorific ?? string.Empty;
                    state.UsesHandsetProfile = dto.UsesHandsetProfile ?? true;
                    state.UsesHandsetIdentity = dto.UsesHandsetIdentity ?? state.UsesHandsetProfile;
                    state.ProfileFacePath = dto.ProfileFacePath ?? string.Empty;
                    state.ProfileBannerPath = dto.ProfileBannerPath ?? string.Empty;
                    state.FaceZoom = dto.FaceZoom > 0f ? dto.FaceZoom : 1f;
                    state.FaceFocusX = dto.FaceFocusX == 0f && dto.FaceFocusY == 0f ? 0.5f : dto.FaceFocusX;
                    state.FaceFocusY = dto.FaceFocusX == 0f && dto.FaceFocusY == 0f ? 0.5f : dto.FaceFocusY;
                    state.BannerZoom = dto.BannerZoom > 0f ? dto.BannerZoom : 1f;
                    state.BannerFocusX = dto.BannerFocusX == 0f && dto.BannerFocusY == 0f ? 0.5f : dto.BannerFocusX;
                    state.BannerFocusY = dto.BannerFocusX == 0f && dto.BannerFocusY == 0f ? 0.5f : dto.BannerFocusY;
                    state.Handle = dto.Handle ?? string.Empty;
                    state.Pronouns = dto.Pronouns ?? string.Empty;
                    state.Race = SceneBook.NamedRace(0, dto.Race ?? string.Empty);
                    state.About = dto.About ?? string.Empty;
                    state.OwnStory = dto.OwnStory ?? string.Empty;
                    state.StoryMedia = dto.StoryMedia ?? state.StoryMedia;
                    state.OwnStoryPermanent = dto.OwnStoryPermanent;
                    state.OwnStoryAtUnix = dto.OwnStoryAtUnix;
                    state.OwnStoryPlace = dto.OwnStoryPlace ?? string.Empty;
                    if (dto.OwnStoryComments is { Length: > 0 })
                    {
                        state.OwnStoryComments.Clear();
                        state.OwnStoryComments.AddRange(dto.OwnStoryComments);
                    }

                    if (dto.StoryReposts is { Length: > 0 })
                    {
                        state.StoryReposts.Clear();
                        state.StoryReposts.AddRange(dto.StoryReposts);
                    }

                    if (dto.GuestStoryThreads is { Length: > 0 })
                    {
                        state.GuestStoryThreads.Clear();
                        state.GuestStoryThreads.AddRange(dto.GuestStoryThreads);
                    }

                    if (dto.StorySaves is { Length: > 0 })
                    {
                        state.StorySaves.Clear();
                        state.StorySaves.AddRange(dto.StorySaves);
                    }

                    AbsorbSet(state.StoryHearts, dto.StoryHearts);
                    state.DropExpiredStory();
                    state.Relationship = dto.Relationship ?? "Rather not say";
                    state.DmsOpen = dto.DmsOpen ?? true;
                    Absorb(state.Genders, dto.Genders);
                    Absorb(state.Sexualities, dto.Sexualities);
                    AbsorbJoined(state.Sexualities, dto.Sexuality);
                    Absorb(state.Intents, dto.Intents);
                    state.Intents.RemoveAll(tag => string.Equals(tag, "Sharing", StringComparison.Ordinal));
                    Absorb(state.Tags, dto.Tags);
                    AbsorbSet(state.Blocked, dto.Blocked);
                    AbsorbBlockedNames(state.BlockedLabels, dto.Blocked, dto.BlockedNames);
                    AbsorbSet(state.ViewedStories, dto.ViewedStories);
                    AbsorbSet(state.LikedPeople, dto.LikedPeople);
                    AbsorbKeys(state.Hearts, dto.Hearts);
                    AbsorbKeys(state.StarredChats, dto.StarredChats);
                    AbsorbKeys(state.HiddenChats, dto.HiddenChats);
                    AbsorbKeys(state.JoinedGroups, dto.JoinedGroups);
                    if (dto.Clubs is { Length: > 0 })
                    {
                        state.Clubs.Clear();
                        state.Clubs.AddRange(dto.Clubs);
                    }

                    state.ActingAsClubId = dto.ActingAsClubId ?? string.Empty;
                    AbsorbKeys(state.KeptPosts, dto.KeptPosts);
                    AbsorbKeys(state.HiddenPearls, dto.HiddenPearls);
                    AbsorbKeys(state.KeptShots, dto.KeptShots);
                    state.NearbyDiscovery = dto.NearbyDiscovery;
                    state.DatingDiscovery = dto.DatingDiscovery;
                    state.LaneDone = dto.LaneDone;
                    state.LaneMarks = VybeLaneMap.Fit(dto.LaneMarks);
                    if (dto.PeopleFindVybe is not null)
                    {
                        state.PeopleFindVybe = dto.PeopleFindVybe;
                    }

                    if (dto.PeopleFindPlus is not null)
                    {
                        state.PeopleFindPlus = dto.PeopleFindPlus;
                    }

                    if (dto.Filters is { Length: > 0 })
                    {
                        foreach (var filter in dto.Filters)
                        {
                            if (filter.Tag is { Length: > 0 })
                            {
                                state.Filters[filter.Tag] = (FilterPole)filter.Pole;
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
            }
            catch (IOException)
            {
            }
        }

        var signOut = paths.State("vybe.signout");
        if (File.Exists(signOut))
        {
            state.AccountId = string.Empty;
            try
            {
                File.Delete(signOut);
            }
            catch (IOException)
            {
            }
        }

        if (state.DisplayName.Length == 0)
        {
            state.DisplayName = fallbackName;
        }

        if (state.Handle.Length == 0)
        {
            state.Handle = "@" + state.DisplayName.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        }

        if (!state.HasAccount)
        {
            state.Page = NightPage.Auth;
        }
        else if (state.Night && !state.PlusAgreed)
        {
            state.Mode = SocialMode.Daylight;
            state.Page = NightPage.Tabs;
        }
        else if (!state.Onboarded)
        {
            state.Page = NightPage.OnboardIdentity;
        }
        else
        {
            state.Page = NightPage.Tabs;
        }

        if (fromDisk)
        {
            state.Save(paths);
        }

        return state;
    }

    public void Save(HostPaths paths)
    {
        try
        {
            var path = paths.State("vybe.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, JsonSerializer.Serialize(new NightSave
            {
                Night = Night,
                PickedMode = PickedMode,
                Consented = PlusAgreed,
                PlusAgreed = PlusAgreed,
                Onboarded = Onboarded,
                AccountId = AccountId,
                Discoverable = Discoverable,
                DisplayName = DisplayName,
                Honorific = Honorific,
                UsesHandsetProfile = UsesHandsetProfile,
                UsesHandsetIdentity = UsesHandsetIdentity,
                ProfileFacePath = ProfileFacePath,
                ProfileBannerPath = ProfileBannerPath,
                FaceZoom = FaceZoom,
                FaceFocusX = FaceFocusX,
                FaceFocusY = FaceFocusY,
                BannerZoom = BannerZoom,
                BannerFocusX = BannerFocusX,
                BannerFocusY = BannerFocusY,
                Handle = Handle,
                Pronouns = Pronouns,
                Race = Race,
                About = About,
                OwnStory = OwnStory,
                StoryMedia = StoryMedia,
                OwnStoryPermanent = OwnStoryPermanent,
                OwnStoryAtUnix = OwnStoryAtUnix,
                OwnStoryPlace = OwnStoryPlace,
                OwnStoryComments = OwnStoryComments.ToArray(),
                GuestStoryThreads = GuestStoryThreads.ToArray(),
                StoryHearts = StoryHearts.ToArray(),
                StoryReposts = StoryReposts.ToArray(),
                StorySaves = StorySaves.ToArray(),
                Sexuality = string.Join(", ", Sexualities),
                Sexualities = Sexualities.ToArray(),
                Relationship = Relationship,
                DmsOpen = DmsOpen,
                Genders = Genders.ToArray(),
                Intents = Intents.ToArray(),
                Tags = Tags.ToArray(),
                Blocked = Blocked.ToArray(),
                BlockedNames = Blocked.Select(id => BlockedLabels.GetValueOrDefault(id, string.Empty)).ToArray(),
                ViewedStories = ViewedStories.ToArray(),
                LikedPeople = LikedPeople.ToArray(),
                Hearts = Hearts.ToArray(),
                StarredChats = StarredChats.ToArray(),
                HiddenChats = HiddenChats.ToArray(),
                JoinedGroups = JoinedGroups.ToArray(),
                Clubs = Clubs.ToArray(),
                ActingAsClubId = ActingAsClubId,
                KeptPosts = KeptPosts.ToArray(),
                HiddenPearls = HiddenPearls.ToArray(),
                KeptShots = KeptShots.ToArray(),
                NearbyDiscovery = NearbyDiscovery,
                DatingDiscovery = DatingDiscovery,
                LaneDone = LaneDone,
                LaneMarks = VybeLaneMap.Fit(LaneMarks),
                PeopleFindVybe = PeopleFindVybe,
                PeopleFindPlus = PeopleFindPlus,
                Filters = Filters.Select(pair => new FilterSave { Tag = pair.Key, Pole = (int)pair.Value }).ToArray(),
            }));
        }
        catch (IOException)
        {
        }

        if (Ledger is not null && HasAccount)
        {
            Ledger.Keep(this);
            Ledger.Save(paths);
        }
    }

    public void Sit(VybeBook book)
    {
        Ledger = book;
        if (HasAccount && !book.Holds(AccountId))
        {
            AccountId = string.Empty;
        }

        if (!HasAccount)
        {
            Page = NightPage.Auth;
        }
    }

    public void EnterSeat(VybeSeat seat)
    {
        AccountId = seat.Id;
        seat.Face.Apply(this);
        if (DisplayName.Length == 0)
        {
            DisplayName = seat.DisplayName;
        }

        if (Handle.Length == 0)
        {
            Handle = seat.Handle;
        }

        ClearAuthDrafts();
        Page = Onboarded ? NightPage.Tabs : NightPage.OnboardIdentity;
        Scroll = 0f;
    }

    public void ClearSession()
    {
        AccountId = string.Empty;
        Onboarded = false;
        DisplayName = string.Empty;
        Honorific = string.Empty;
        Handle = string.Empty;
        Pronouns = string.Empty;
        Race = string.Empty;
        About = string.Empty;
        ProfileFacePath = string.Empty;
        ProfileBannerPath = string.Empty;
        FaceZoom = 1f;
        FaceFocusX = 0.5f;
        FaceFocusY = 0.5f;
        BannerZoom = 1f;
        BannerFocusX = 0.5f;
        BannerFocusY = 0.5f;
        UsesHandsetProfile = true;
        UsesHandsetIdentity = true;
        Discoverable = true;
        Relationship = "Rather not say";
        DmsOpen = true;
        Genders.Clear();
        Sexualities.Clear();
        Intents.Clear();
        Tags.Clear();
        DropConfirm = false;
        DropSeatId = string.Empty;
        ClearAuthDrafts();
        Page = NightPage.Auth;
        ReturnTo = NightPage.Auth;
        Tab = NightTab.Home;
        Scroll = 0f;
    }

    public void ClearAuthDrafts()
    {
        EnterHandle = string.Empty;
        EnterSecret = string.Empty;
        JoinName = string.Empty;
        JoinTitle = string.Empty;
        JoinHandle = string.Empty;
        JoinSecret = string.Empty;
        JoinAgain = string.Empty;
        AuthNote = string.Empty;
    }

    public void Open(NightPage page)
    {
        StoryMoreOpen = false;
        ReturnTo = Page;
        Page = page;
        Scroll = 0f;
    }

    public void Peek(NightPage page)
    {
        PeopleFind.Scroll = Scroll;
        ReturnTo = Page;
        Page = page;
        Scroll = 0f;
    }

    public void Back()
    {
        StoryMoreOpen = false;
        var keep = PeopleFind.Scroll;
        Page = ReturnTo == Page ? NightPage.Tabs : ReturnTo;
        ReturnTo = NightPage.Tabs;
        Scroll = Page == NightPage.Tabs ? keep : 0f;
    }

    public void AdjustPlacing(float zoom, float focusX, float focusY)
    {
        zoom = Math.Clamp(zoom, CoverFit.PlaceZoomMin, CoverFit.PlaceZoomMax);
        if (PickingAvatar)
        {
            FaceZoom = zoom;
            FaceFocusX = focusX;
            FaceFocusY = focusY;
            return;
        }

        BannerZoom = zoom;
        BannerFocusX = focusX;
        BannerFocusY = focusY;
    }

    public void EnterMode(SocialMode mode)
    {
        if (mode == SocialMode.Daylight)
        {
            Mode = SocialMode.Daylight;
            HidePlusLanes();
            Scroll = 0f;
            if (Page is NightPage.Gate or NightPage.Rules)
            {
                Page = ReturnTo == NightPage.Settings ? NightPage.Settings : NightPage.Tabs;
            }

            return;
        }

        if (PlusBlocked)
        {
            Mode = SocialMode.Daylight;
            return;
        }

        if (!PlusAgreed)
        {
            Mode = SocialMode.Daylight;
            Open(NightPage.Gate);
            return;
        }

        Mode = SocialMode.AfterDark;
        PickedMode = true;
        Scroll = 0f;

        if (Page is NightPage.Gate or NightPage.Rules)
        {
            Page = Onboarded ? NightPage.Tabs : NightPage.OnboardIdentity;
        }
    }

    public void AgreePlus()
    {
        PlusAgreed = true;
        Consented = true;
        Mode = SocialMode.AfterDark;
        PickedMode = true;
    }

    public void RevokePlus()
    {
        PlusAgreed = false;
        Consented = false;
        Mode = SocialMode.Daylight;
        HidePlusLanes();
        Wash = 0f;
        if (Page is NightPage.Gate or NightPage.Rules)
        {
            Page = NightPage.OnboardIdentity;
        }
    }

    public void StartWash(bool toNight)
    {
        if (Washing || (toNight && PlusBlocked))
        {
            return;
        }

        if (toNight && !PlusAgreed)
        {
            Open(NightPage.Gate);
            return;
        }

        WashToNight = toNight;
        Wash = 0.001f;
        HoldMark = 0f;
    }

    public void TickHold(bool overMark, bool held, float delta)
    {
        if (Washing)
        {
            HoldMark = 0f;
            return;
        }

        if (overMark && held)
        {
            HoldMark += delta;
            if (HoldMark >= VybeChrome.HoldSeconds)
            {
                if (!Night && PlusBlocked)
                {
                    HoldMark = 0f;
                    return;
                }

                StartWash(!Night);
            }
        }
        else
        {
            HoldMark = 0f;
        }
    }

    public void TickWash(float delta, HostPaths paths)
    {
        if (!Washing)
        {
            return;
        }

        Wash += delta;
        if (Wash < VybeChrome.WashSeconds)
        {
            return;
        }

        Wash = 0f;
        EnterMode(WashToNight ? SocialMode.AfterDark : SocialMode.Daylight);
        DropExpiredStory();
        Save(paths);
    }

    public FilterPole Pole(string tag) =>
        Filters.TryGetValue(tag, out var pole) ? pole : FilterPole.Neutral;

    public void CycleFilter(string tag)
    {
        var next = Pole(tag) switch
        {
            FilterPole.Neutral => FilterPole.Include,
            FilterPole.Include => FilterPole.Exclude,
            _ => FilterPole.Neutral,
        };
        if (next == FilterPole.Neutral)
        {
            Filters.Remove(tag);
        }
        else
        {
            Filters[tag] = next;
        }
    }

    public bool Passes(ScenePerson person)
    {
        if (VybeChrome.IsLalafell(person.Race))
        {
            return false;
        }

        if (Blocked.Contains(person.Id))
        {
            return false;
        }

        if (!Night && person.NightOnly)
        {
            return false;
        }

        if (Search.Length > 0 &&
            person.Name.IndexOf(Search, StringComparison.OrdinalIgnoreCase) < 0 &&
            person.Handle.IndexOf(Search, StringComparison.OrdinalIgnoreCase) < 0 &&
            person.World.IndexOf(Search, StringComparison.OrdinalIgnoreCase) < 0 &&
            person.Line.IndexOf(Search, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (Hashtag.Length > 0 &&
            person.Line.IndexOf(Hashtag.TrimStart('#'), StringComparison.OrdinalIgnoreCase) < 0 &&
            !person.Tags.Any(tag => tag.Contains(Hashtag.TrimStart('#'), StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (Vibe == 1 && MilesAway(person) > 4)
        {
            return false;
        }

        if (Vibe == 2 && !person.Online)
        {
            return false;
        }

        if (Vibe == 3 && person.Photos >= 8)
        {
            return false;
        }

        if (Vibe == 4)
        {
            if (Night)
            {
                if (!person.NightOnly)
                {
                    return false;
                }
            }
            else if (!person.Intents.Contains("Friends", StringComparer.Ordinal))
            {
                return false;
            }
        }

        foreach (var (tag, pole) in Filters)
        {
            var has = person.Tags.Contains(tag, StringComparer.Ordinal) ||
                      person.Intents.Contains(tag, StringComparer.Ordinal);
            if (pole == FilterPole.Include && !has)
            {
                return false;
            }

            if (pole == FilterPole.Exclude && has)
            {
                return false;
            }
        }

        return true;
    }

    public bool TryFind(int id, out ScenePerson person)
    {
        foreach (var entry in Roster)
        {
            if (entry.Id == id)
            {
                person = entry;
                return true;
            }
        }

        person = default;
        return false;
    }

    public bool TryFindName(string name, out ScenePerson person)
    {
        foreach (var entry in Roster)
        {
            if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                person = entry;
                return true;
            }
        }

        person = default;
        return false;
    }

    public void BlockPerson(ScenePerson person)
    {
        Blocked.Add(person.Id);
        var label = person.Name.Trim();
        if (label.Length == 0)
        {
            label = person.Handle.Trim();
        }

        if (label.Length > 0)
        {
            BlockedLabels[person.Id] = label;
        }
    }

    public void Unblock(int id)
    {
        Blocked.Remove(id);
        BlockedLabels.Remove(id);
    }

    public void Bind(PearlSnapshot snap)
    {
        SignedIn = snap.SignedIn;
        GateFollowers = snap.Followers;
        GateFollowing = snap.Following;
        if (DisplayName.Length == 0 && snap.MeName.Length > 0)
        {
            DisplayName = snap.MeName;
        }

        if (Handle.Length == 0 && snap.MeHandle.Length > 0)
        {
            Handle = snap.MeHandle.StartsWith('@') ? snap.MeHandle : "@" + snap.MeHandle;
        }

        if (About.Length == 0 && snap.MeBio.Length > 0)
        {
            About = snap.MeBio;
        }
        if (!snap.SignedIn)
        {
            Roster.Clear();
            LiveStories.Clear();
            return;
        }

        if (snap.Busy && snap.People.Length == 0 && snap.Stories.Length == 0)
        {
            return;
        }

        Roster.Clear();
        LiveStories.Clear();
        var seen = new HashSet<int>();
        foreach (var person in snap.People)
        {
            if (VybeChrome.IsLalafell(person.Race))
            {
                continue;
            }

            var id = StableId(person.Id);
            if (!seen.Add(id))
            {
                continue;
            }

            var handle = person.Handle.Length > 0
                ? (person.Handle.StartsWith('@') ? person.Handle : "@" + person.Handle)
                : "@someone";
            Roster.Add(new ScenePerson(id, person.Id, person.DisplayName.Length > 0 ? person.DisplayName : "Someone",
                handle, person.World.Length > 0 ? person.World : person.PhoneNumber, string.Empty, person.IsMutual, 0,
                false, WashOf(id),
                Array.Empty<string>(), Array.Empty<string>(), person.AvatarUrl, TimeZoneId: person.TimeZoneId,
                Race: SceneBook.NamedRace(0, person.Race)));
            if (person.IsMutual)
            {
                Connected.Add(id);
            }
        }

        foreach (var story in snap.Stories)
        {
            var id = StableId(story.AuthorId);
            LiveStories.Add(id);
            if (seen.Contains(id))
            {
                continue;
            }

            seen.Add(id);
            Roster.Add(new ScenePerson(id, story.AuthorId, story.AuthorName.Length > 0 ? story.AuthorName : "Someone",
                "@story", string.Empty, story.Count + " in the tray", story.HasUnseen, 0, false, WashOf(id),
                Array.Empty<string>(), Array.Empty<string>(), string.Empty));
        }
    }

    public static int StableId(string key)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in key)
            {
                hash = (hash ^ ch) * 16777619;
            }

            var id = (int)(hash & 0x7fffffff);
            return id == 0 ? 1 : id;
        }
    }

    private static Vector4 WashOf(int id)
    {
        var r = 0.28f + id % 50 / 90f;
        var g = 0.22f + id / 7 % 50 / 90f;
        var b = 0.34f + id / 13 % 45 / 90f;
        return new Vector4(r, g, b, 1f);
    }

    public bool IsOwnPost(ScenePost post) =>
        post.AuthorId < 0 &&
        string.Equals(post.Author, DisplayName, StringComparison.OrdinalIgnoreCase) &&
        (post.Cite is not { Length: > 0 } ||
         string.Equals(post.Cite, DisplayName, StringComparison.OrdinalIgnoreCase) ||
         TryFindName(post.Cite, out _));

    public bool ShowsPost(ScenePost post)
    {
        if (!IsOwnPost(post))
        {
            return false;
        }

        if (HiddenPosts.Contains(post.Id))
        {
            return false;
        }

        if (FeedEveryone)
        {
            if (post.ConnectionsOnly)
            {
                return false;
            }
        }
        else if (post.AuthorId >= 0)
        {
            if (!Connected.Contains(post.AuthorId))
            {
                return false;
            }
        }
        else if (!post.ConnectionsOnly)
        {
            return false;
        }

        if (Hashtag.Length > 0 &&
            post.Body.IndexOf(Hashtag.TrimStart('#'), StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (Search.Length > 0 &&
            post.Body.IndexOf(Search, StringComparison.OrdinalIgnoreCase) < 0 &&
            post.Author.IndexOf(Search, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        return true;
    }

    public IEnumerable<ScenePost> Wall()
    {
        foreach (var post in Mine)
        {
            if (IsOwnPost(post))
            {
                yield return post;
            }
        }
    }

    public bool TryResolvePost(out ScenePost post)
    {
        foreach (var wall in Wall())
        {
            if (wall.Id == PostIndex)
            {
                post = wall;
                return true;
            }
        }

        post = default;
        return false;
    }

    public void ScrubMine()
    {
        Mine.RemoveAll(post => !IsOwnPost(post));
        var keep = new HashSet<int>();
        foreach (var post in Mine)
        {
            keep.Add(post.Id);
        }

        PruneOrphans(LikedPosts, keep);
        PruneOrphans(SavedPosts, keep);
        PruneOrphans(Reposted, keep);
        PruneOrphans(HiddenPosts, keep);
        foreach (var id in Comments.Keys.ToArray())
        {
            if (!keep.Contains(id))
            {
                Comments.Remove(id);
            }
        }
    }

    private static void PruneOrphans(HashSet<int> ids, HashSet<int> keep)
    {
        foreach (var id in ids.ToArray())
        {
            if (!keep.Contains(id))
            {
                ids.Remove(id);
            }
        }
    }

    public int CommentCount(ScenePost post) =>
        post.Comments + (Comments.TryGetValue(post.Id, out var lines) ? lines.Count : 0);

    public List<ChatLine> Thread(int personId)
    {
        if (!Threads.TryGetValue(personId, out var lines))
        {
            lines = new List<ChatLine>();
            Threads[personId] = lines;
        }

        return lines;
    }

    public bool TryFindGate(string gateId, out ScenePerson person)
    {
        foreach (var entry in Roster)
        {
            if (string.Equals(entry.GateId, gateId, StringComparison.Ordinal))
            {
                person = entry;
                return true;
            }
        }

        person = default;
        return false;
    }

    public bool ToggleLikedPerson(int id)
    {
        if (!LikedPeople.Add(id))
        {
            LikedPeople.Remove(id);
            return false;
        }

        return true;
    }

    public void FlipHeart(string postId)
    {
        if (postId.Length == 0)
        {
            return;
        }

        if (!Hearts.Add(postId))
        {
            Hearts.Remove(postId);
        }
    }

    public bool PostHearted(PearlPost post) => post.Liked ^ Hearts.Contains(post.Id);

    public bool PostHearted(string postId, bool baseline) =>
        postId.Length > 0 && (baseline ^ Hearts.Contains(postId));

    public int PostHearts(PearlPost post)
    {
        var liked = PostHearted(post);
        if (liked == post.Liked)
        {
            return post.Likes;
        }

        return liked ? post.Likes + 1 : Math.Max(0, post.Likes - 1);
    }

    public void FollowPerson(ScenePerson person, IPearlHub pearl, bool on)
    {
        if (on)
        {
            Connected.Add(person.Id);
        }
        else
        {
            Connected.Remove(person.Id);
        }

        if (person.GateId.Length > 0)
        {
            pearl.Follow(person.GateId, on);
        }
    }

    public void Note(string who, string kind, string line, int personId = -1, int postId = -1)
    {
        Notes.Insert(0, new SceneNote(who, kind, line, "now", personId, postId));
        if (Notes.Count > 40)
        {
            Notes.RemoveAt(Notes.Count - 1);
        }
    }

    public IEnumerable<SceneNote> AlertRows()
    {
        foreach (var id in Incoming)
        {
            if (!TryFind(id, out var person))
            {
                continue;
            }

            var line = Night ? "wants to connect" : "followed you";
            yield return new SceneNote(person.Name, "intro", line, "now", person.Id, -1);
        }

        foreach (var note in Notes)
        {
            yield return note;
        }
    }

    public static int MilesAway(ScenePerson person) => Math.Abs(person.Id % 7);

    private static void Absorb(List<string> target, string[]? source)
    {
        if (source is not { Length: > 0 })
        {
            return;
        }

        target.Clear();
        target.AddRange(source);
    }

    private static void AbsorbJoined(List<string> target, string? joined)
    {
        if (target.Count > 0 || string.IsNullOrWhiteSpace(joined))
        {
            return;
        }

        foreach (var part in joined.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!target.Contains(part))
            {
                target.Add(part);
            }
        }
    }

    public static string PearlTalkKey(string id) => "p:" + id;

    public static string LocalTalkKey(int id) => "l:" + id.ToString(CultureInfo.InvariantCulture);

    public bool TalkStarred(string key) => StarredChats.Contains(key);

    public bool TalkHidden(string key) => HiddenChats.Contains(key);

    public void ToggleTalkStar(string key)
    {
        if (!StarredChats.Add(key))
        {
            StarredChats.Remove(key);
        }
    }

    public void HideTalk(string key)
    {
        HiddenChats.Add(key);
        StarredChats.Remove(key);
    }

    private static void AbsorbKeys(HashSet<string> target, string[]? source)
    {
        if (source is not { Length: > 0 })
        {
            return;
        }

        target.Clear();
        for (var index = 0; index < source.Length; index++)
        {
            var key = source[index];
            if (!string.IsNullOrWhiteSpace(key))
            {
                target.Add(key.Trim());
            }
        }
    }

    private static void AbsorbSet(HashSet<int> target, int[]? source)
    {
        if (source is not { Length: > 0 })
        {
            return;
        }

        target.Clear();
        foreach (var id in source)
        {
            target.Add(id);
        }
    }

    private static void AbsorbBlockedNames(Dictionary<int, string> target, int[]? ids, string[]? names)
    {
        target.Clear();
        if (ids is not { Length: > 0 } || names is not { Length: > 0 })
        {
            return;
        }

        var count = Math.Min(ids.Length, names.Length);
        for (var index = 0; index < count; index++)
        {
            var name = names[index];
            if (!string.IsNullOrWhiteSpace(name))
            {
                target[ids[index]] = name.Trim();
            }
        }
    }

    private sealed class NightSave
    {
        public bool Night { get; set; }

        public bool PickedMode { get; set; }

        public bool Consented { get; set; }

        public bool PlusAgreed { get; set; }

        public bool Onboarded { get; set; }

        public string? AccountId { get; set; }

        public bool Discoverable { get; set; }

        public string? DisplayName { get; set; }

        public string? Honorific { get; set; }

        public bool? UsesHandsetProfile { get; set; }

        public bool? UsesHandsetIdentity { get; set; }

        public string? ProfileFacePath { get; set; }

        public string? ProfileBannerPath { get; set; }

        public float FaceZoom { get; set; }

        public float FaceFocusX { get; set; }

        public float FaceFocusY { get; set; }

        public float BannerZoom { get; set; }

        public float BannerFocusX { get; set; }

        public float BannerFocusY { get; set; }

        public string? Handle { get; set; }

        public string? Pronouns { get; set; }

        public string? Race { get; set; }

        public string? About { get; set; }

        public string? OwnStory { get; set; }

        public string? StoryMedia { get; set; }

        public bool OwnStoryPermanent { get; set; }

        public long OwnStoryAtUnix { get; set; }

        public string? OwnStoryPlace { get; set; }

        public PearlComment[]? OwnStoryComments { get; set; }

        public StoryThread[]? GuestStoryThreads { get; set; }

        public int[]? StoryHearts { get; set; }

        public PearlPost[]? StoryReposts { get; set; }

        public PearlPost[]? StorySaves { get; set; }

        public string? Sexuality { get; set; }

        public string[]? Sexualities { get; set; }

        public string? Relationship { get; set; }

        public bool? DmsOpen { get; set; }

        public int NextPostId { get; set; }

        public int FollowersSeed { get; set; }

        public string[]? Genders { get; set; }

        public string[]? Intents { get; set; }

        public string[]? Tags { get; set; }

        public int[]? Connected { get; set; }

        public int[]? Requested { get; set; }

        public int[]? Incoming { get; set; }

        public int[]? Blocked { get; set; }

        public string[]? BlockedNames { get; set; }

        public int[]? LikedPosts { get; set; }

        public int[]? LikedPeople { get; set; }

        public string[]? Hearts { get; set; }

        public string[]? StarredChats { get; set; }

        public string[]? HiddenChats { get; set; }

        public string[]? JoinedGroups { get; set; }

        public VybeClub[]? Clubs { get; set; }

        public string? ActingAsClubId { get; set; }

        public string[]? KeptPosts { get; set; }

        public string[]? HiddenPearls { get; set; }

        public string[]? KeptShots { get; set; }

        public int[]? SavedPosts { get; set; }

        public int[]? Reposted { get; set; }

        public int[]? ViewedStories { get; set; }

        public int[]? HiddenPosts { get; set; }

        public bool NearbyDiscovery { get; set; }

        public bool DatingDiscovery { get; set; }

        public bool LaneDone { get; set; }

        public int[]? LaneMarks { get; set; }

        public PeopleFindState? PeopleFindVybe { get; set; }

        public PeopleFindState? PeopleFindPlus { get; set; }

        public FilterSave[]? Filters { get; set; }

        public PostSave[]? Mine { get; set; }

        public SceneNote[]? Notes { get; set; }

        public ThreadSave[]? Threads { get; set; }

        public CommentPack[]? Comments { get; set; }
    }

    private sealed class FilterSave
    {
        public string Tag { get; set; } = string.Empty;

        public int Pole { get; set; }
    }

    private sealed class PostSave
    {
        public int Id { get; set; }

        public string Author { get; set; } = string.Empty;

        public int AuthorId { get; set; }

        public string When { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        public string Place { get; set; } = string.Empty;

        public bool ConnectionsOnly { get; set; }

        public int Likes { get; set; }

        public int Comments { get; set; }

        public string? Cite { get; set; }

        public static PostSave From(ScenePost post) => new()
        {
            Id = post.Id,
            Author = post.Author,
            AuthorId = post.AuthorId,
            When = post.When,
            Body = post.Body,
            Place = post.Place,
            ConnectionsOnly = post.ConnectionsOnly,
            Likes = post.Likes,
            Comments = post.Comments,
            Cite = post.Cite,
        };

        public ScenePost ToPost() =>
            new(Id, Author, AuthorId, When, Body, Place, ConnectionsOnly, Likes, Comments, Cite);
    }

    private sealed class ThreadSave
    {
        public int PersonId { get; set; }

        public ChatLine[]? Lines { get; set; }
    }

    private sealed class CommentPack
    {
        public int PostId { get; set; }

        public SceneComment[]? Lines { get; set; }
    }
}

internal sealed class StoryThread
{
    public int PersonId { get; set; }

    public long AtUnix { get; set; }

    public List<PearlComment> Lines { get; set; } = new();
}

internal readonly record struct ScenePerson(
    int Id, string GateId, string Name, string Handle, string World, string Line, bool Online, int Photos, bool NightOnly,
    Vector4 Wash, string[] Intents, string[] Tags, string AvatarUrl = "", string Gender = "", string Sexuality = "",
    string Relationship = "", bool? DmsOpen = null, bool PlusMember = false, string TimeZoneId = "",
    string Race = "");

internal readonly record struct ScenePost(
    int Id, string Author, int AuthorId, string When, string Body, string Place, bool ConnectionsOnly, int Likes,
    int Comments, string? Cite = null);

internal readonly record struct SceneNote(string Who, string Kind, string Line, string When, int PersonId, int PostId);

internal readonly record struct ChatLine(bool Mine, string Body, string When);

internal readonly record struct SceneComment(string Author, string Body, string When);

internal static class SceneBook
{
    public static readonly string[] Intents =
    {
        "Friends", "Dating", "GPose", "Collab", "Raiding", "Roulettes", "Venues & Clubbing",
        "Housing & Decoration", "Roleplaying", "Wandering", "Relationship", "ERP",
    };

    public static readonly string[] DayIntents =
    {
        "Friends", "Dating", "GPose", "Collab", "Raiding", "Roulettes", "Venues & Clubbing",
        "Housing & Decoration", "Roleplaying", "Wandering",
    };

    public static readonly string[] Races =
    {
        "Hyur", "Miqo'te", "Elezen", "Roegadyn", "Au Ra", "Viera", "Hrothgar",
    };

    public static string NamedRace(byte raceId, string raceName)
    {
        if (VybeChrome.IsLalafell(raceId, raceName))
        {
            return string.Empty;
        }

        if (raceId is >= 1 and <= 8)
        {
            return raceId switch
            {
                1 => "Hyur",
                2 => "Elezen",
                4 => "Miqo'te",
                5 => "Roegadyn",
                6 => "Au Ra",
                7 => "Hrothgar",
                8 => "Viera",
                _ => string.Empty,
            };
        }

        var name = (raceName ?? string.Empty).Trim();
        for (var index = 0; index < Races.Length; index++)
        {
            if (name.Contains(Races[index], StringComparison.OrdinalIgnoreCase))
            {
                return Races[index];
            }
        }

        return string.Empty;
    }

    public static readonly string[] Genders =
    {
        "Female", "Male", "Nonbinary", "Genderfluid", "Transgender", "Female+", "Male+", "Femboy",
    };

    public static readonly string[] Sexualities =
    {
        "Straight", "Gay", "Lesbian", "Bi", "Pan", "Asexual", "Demisexual", "Demiromantic",
    };

    public static readonly string[] Relationships =
    {
        "Rather not say", "Single", "Taken", "Poly", "Open relationship", "It's complicated",
    };

    public static readonly string[] Tone = { "Soft", "Playful", "Serious", "Teasing" };

    public static readonly string[] Pace = { "Slow burn", "Unhurried", "In the moment" };

    public static readonly string[] Style = { "Story-first", "Slice of life", "Scene writing" };

    public static IEnumerable<string> FilterTags(bool night)
    {
        foreach (var tag in Tone)
        {
            yield return tag;
        }

        foreach (var tag in Pace)
        {
            yield return tag;
        }

        foreach (var tag in Style)
        {
            yield return tag;
        }

        var intents = night ? Intents : DayIntents;
        foreach (var tag in intents)
        {
            yield return tag;
        }
    }

    public static string[] DayHashes { get; } = { "#Crystal", "#FreeCompany", "#GoodVibes", "#VYBE" };

    public static string[] NightHashes { get; } = { "#AfterHours", "#NightOwls", "#GoodVibes", "#NeonNights" };

}
