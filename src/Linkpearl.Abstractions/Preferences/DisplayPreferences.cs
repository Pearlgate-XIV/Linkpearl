using Linkpearl.Applets;
using Linkpearl.Media;

namespace Linkpearl.Preferences;

// Live look of the glass and how the communicator behaves in the world. HandsetHost loads
// persisted values into here at boot and writes them back when anything changes.
public sealed class DisplayPreferences
{
    private bool use24HourClock;
    private AppearanceMode appearance = AppearanceMode.Night;
    private string wallpaperId = WallpaperCatalog.DefaultId;
    private string customPlateFile = string.Empty;
    private string[] customPlateFiles = [];
    private string customBannerFile = string.Empty;
    private string colorway = ColorwayId.Night;
    private string core = CoreId.Blue;
    private ShadeLevel shade = ShadeLevel.Even;
    private ClockFace clockFace;
    private LetteringSize lettering = LetteringSize.Medium;
    private NameStyle nameStyle;
    private string ownName = string.Empty;
    private string ownTitle = string.Empty;
    private TitleMotion titleMotion;
    private bool titleGlow = true;
    private float titleInkR = 1f;
    private float titleInkG = 1f;
    private float titleInkB = 1f;
    private float titleGlowR = 0.92f;
    private float titleGlowG = 0.78f;
    private float titleGlowB = 0.42f;
    private NameGlowWeight titleGlowWeight = NameGlowWeight.Medium;
    private bool testingAccount = true;
    private TitleMotion nameMotion;
    private bool nameGlow;
    private float nameGlowR = 0.92f;
    private float nameGlowG = 0.78f;
    private float nameGlowB = 0.42f;
    private NameGlowWeight nameGlowWeight = NameGlowWeight.Medium;
    private bool nameInkCustom;
    private float nameInkR = 1f;
    private float nameInkG = 1f;
    private float nameInkB = 1f;
    private string displayFace = FounderFaces.Inter;
    private bool founderFacesGranted;
    private bool showWorld = true;
    private bool showMarks = true;
    private int extraHomeScreens;
    private bool reduceMotion;
    private bool quiet;
    private bool quietWhenBusy;
    private bool wakeInPocket;
    private bool stayInPortraits = true;
    private bool tuckForCutscenes = true;
    private FightPresence fight = FightPresence.Stay;
    private TuneLayout layout;
    private float brightness = 1f;
    private float volume = 0.70f;
    private float micVolume = 0.80f;
    private string speakerId = string.Empty;
    private string microphoneId = string.Empty;
    private string callSpeakerId = string.Empty;
    private string callMicrophoneId = string.Empty;
    private bool autoRotate;
    private bool landscape;
    private string pictureDraft = string.Empty;
    private string bannerDraft = string.Empty;
    private string[] replies = DefaultReplies;
    private string[]? installedApps;
    private string[][] appPages = [AppShelf.DefaultInstalled];
    private string[] favoriteApps = [];
    private string[] appFolders = [];
    private string[] seenShelfApps = [];
    private string[] ownedApps = [];
    private string[] quickApps = ["", "", "", ""];
    private string[] studioWidgets = DefaultStudioWidgets;
    private bool[] studioHalf = DefaultStudioHalf;
    private int[] studioSeat = [];
    private string[] studioApps = DefaultStudioApps;
    private bool feedShowSay = true;
    private bool feedShowShout = true;
    private bool feedShowYell = true;
    private bool feedShowParty = true;

    private static readonly string[] DefaultReplies = { "On my way.", "One moment.", "Need a raise." };

    public static readonly string[] DefaultStudioWidgets =
        ["calendar", "announcements", "apps", "weather", "music"];

    public static readonly bool[] DefaultStudioHalf =
        [false, true, true, false, false];

    public static readonly string[] DefaultStudioApps =
        ["pearlchat", "party", "friends", "retainer", "market", "events"];

    public event Action? Changed;

    public bool Use24HourClock
    {
        get => use24HourClock;
        set => Set(ref use24HourClock, value);
    }

    public AppearanceMode Appearance
    {
        get => appearance;
        set => Set(ref appearance, value);
    }

    public string WallpaperId
    {
        get => wallpaperId;
        set => Set(ref wallpaperId, value ?? string.Empty);
    }

    public string CustomPlateFile
    {
        get => customPlateFile;
        set => Set(ref customPlateFile, value ?? string.Empty);
    }

    public IReadOnlyList<string> CustomPlateFiles => customPlateFiles;

    public bool UsingCustomPlate => customPlateFile.Length > 0;

    public void ReplaceCustomPlates(IReadOnlyList<string> files)
    {
        var next = SanitizePlates(files);
        if (PlatesEqual(customPlateFiles, next))
        {
            return;
        }

        customPlateFiles = next;
        if (customPlateFile.Length > 0 && Array.IndexOf(customPlateFiles, customPlateFile) < 0)
        {
            customPlateFile = string.Empty;
        }

        Changed?.Invoke();
    }

    public void AddCustomPlate(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (name.Length == 0 || customPlateFiles.Length >= PlateFiles.MaxSlots)
        {
            return;
        }

        for (var index = 0; index < customPlateFiles.Length; index++)
        {
            if (string.Equals(customPlateFiles[index], name, StringComparison.OrdinalIgnoreCase))
            {
                CustomPlateFile = name;
                return;
            }
        }

        var next = new string[customPlateFiles.Length + 1];
        customPlateFiles.CopyTo(next, 0);
        next[^1] = name;
        customPlateFiles = next;
        customPlateFile = name;
        Changed?.Invoke();
    }

    public void RemoveCustomPlate(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (name.Length == 0)
        {
            return;
        }

        var kept = new List<string>(customPlateFiles.Length);
        for (var index = 0; index < customPlateFiles.Length; index++)
        {
            if (!string.Equals(customPlateFiles[index], name, StringComparison.OrdinalIgnoreCase))
            {
                kept.Add(customPlateFiles[index]);
            }
        }

        if (kept.Count == customPlateFiles.Length)
        {
            return;
        }

        customPlateFiles = kept.ToArray();
        if (string.Equals(customPlateFile, name, StringComparison.OrdinalIgnoreCase))
        {
            customPlateFile = customPlateFiles.Length > 0 ? customPlateFiles[^1] : string.Empty;
        }

        Changed?.Invoke();
    }

    public void SwapCustomPlate(string previous, string next)
    {
        var from = Path.GetFileName(previous);
        var to = Path.GetFileName(next);
        if (from.Length == 0 || to.Length == 0)
        {
            return;
        }

        var copy = new string[customPlateFiles.Length];
        var found = false;
        for (var index = 0; index < customPlateFiles.Length; index++)
        {
            if (string.Equals(customPlateFiles[index], from, StringComparison.OrdinalIgnoreCase))
            {
                copy[index] = to;
                found = true;
            }
            else
            {
                copy[index] = customPlateFiles[index];
            }
        }

        if (!found)
        {
            return;
        }

        customPlateFiles = copy;
        if (string.Equals(customPlateFile, from, StringComparison.OrdinalIgnoreCase))
        {
            customPlateFile = to;
        }

        Changed?.Invoke();
    }

    public string CustomBannerFile
    {
        get => customBannerFile;
        set => Set(ref customBannerFile, value ?? string.Empty);
    }

    public bool UsingBanner => customBannerFile.Length > 0;

    public string Colorway
    {
        get => colorway;
        set => Set(ref colorway, ColorwayId.Sanitize(value));
    }

    public string Core
    {
        get => core;
        set => Set(ref core, CoreId.Sanitize(value));
    }

    public ShadeLevel Shade
    {
        get => shade;
        set => Set(ref shade, value);
    }

    public ClockFace ClockFace
    {
        get => clockFace;
        set => Set(ref clockFace, value);
    }

    public LetteringSize Lettering
    {
        get => lettering;
        set => Set(ref lettering, value);
    }

    public float LetteringScale => lettering switch
    {
        LetteringSize.Small => 0.92f,
        LetteringSize.Large => 1.10f,
        _ => 1f,
    };

    public NameStyle NameStyle
    {
        get => nameStyle;
        set => Set(ref nameStyle, value);
    }

    public bool TestingAccount
    {
        get => testingAccount;
        set => Set(ref testingAccount, value);
    }

    public string OwnName
    {
        get => ownName;
        set => Set(ref ownName, ShownName.ClampLive(value));
    }

    public string OwnTitle
    {
        get => ownTitle;
        set => Set(ref ownTitle, ShownName.ClampTitle(value));
    }

    public TitleMotion TitleMotion
    {
        get => titleMotion;
        set => Set(ref titleMotion, value);
    }

    public bool TitleGlow
    {
        get => titleGlow;
        set => Set(ref titleGlow, value);
    }

    public NameGlowWeight TitleGlowWeight
    {
        get => titleGlowWeight;
        set => Set(ref titleGlowWeight, value);
    }

    public float TitleInkR
    {
        get => titleInkR;
        set => Set(ref titleInkR, Math.Clamp(value, 0f, 1f));
    }

    public float TitleInkG
    {
        get => titleInkG;
        set => Set(ref titleInkG, Math.Clamp(value, 0f, 1f));
    }

    public float TitleInkB
    {
        get => titleInkB;
        set => Set(ref titleInkB, Math.Clamp(value, 0f, 1f));
    }

    public float TitleGlowR
    {
        get => titleGlowR;
        set => Set(ref titleGlowR, Math.Clamp(value, 0f, 1f));
    }

    public float TitleGlowG
    {
        get => titleGlowG;
        set => Set(ref titleGlowG, Math.Clamp(value, 0f, 1f));
    }

    public float TitleGlowB
    {
        get => titleGlowB;
        set => Set(ref titleGlowB, Math.Clamp(value, 0f, 1f));
    }

    public void SetTitleInkColor(Vector4 color)
    {
        TitleInkR = color.X;
        TitleInkG = color.Y;
        TitleInkB = color.Z;
    }

    public void SetTitleGlowColor(Vector4 color)
    {
        TitleGlowR = color.X;
        TitleGlowG = color.Y;
        TitleGlowB = color.Z;
    }

    public TitleMotion NameMotion
    {
        get => nameMotion;
        set => Set(ref nameMotion, value);
    }

    public bool NameGlow
    {
        get => nameGlow;
        set => Set(ref nameGlow, value);
    }

    public float NameGlowR
    {
        get => nameGlowR;
        set => Set(ref nameGlowR, Math.Clamp(value, 0f, 1f));
    }

    public float NameGlowG
    {
        get => nameGlowG;
        set => Set(ref nameGlowG, Math.Clamp(value, 0f, 1f));
    }

    public float NameGlowB
    {
        get => nameGlowB;
        set => Set(ref nameGlowB, Math.Clamp(value, 0f, 1f));
    }

    public NameGlowWeight NameGlowWeight
    {
        get => nameGlowWeight;
        set => Set(ref nameGlowWeight, value);
    }

    public bool NameInkCustom
    {
        get => nameInkCustom;
        set => Set(ref nameInkCustom, value);
    }

    public float NameInkR
    {
        get => nameInkR;
        set => Set(ref nameInkR, Math.Clamp(value, 0f, 1f));
    }

    public float NameInkG
    {
        get => nameInkG;
        set => Set(ref nameInkG, Math.Clamp(value, 0f, 1f));
    }

    public float NameInkB
    {
        get => nameInkB;
        set => Set(ref nameInkB, Math.Clamp(value, 0f, 1f));
    }

    public void SetNameGlowColor(Vector4 color)
    {
        NameGlowR = color.X;
        NameGlowG = color.Y;
        NameGlowB = color.Z;
    }

    public void SetNameInkColor(Vector4 color)
    {
        NameInkCustom = true;
        NameInkR = color.X;
        NameInkG = color.Y;
        NameInkB = color.Z;
    }

    public string DisplayFace
    {
        get => displayFace;
        set => Set(ref displayFace, FounderFaces.Sanitize(value));
    }

    public bool FounderFacesGranted
    {
        get => founderFacesGranted;
        set => Set(ref founderFacesGranted, value);
    }

    public float ShadeMul => shade switch
    {
        ShadeLevel.Light => 0.62f,
        ShadeLevel.Deep => 1.38f,
        _ => 1f,
    };

    public bool ShowWorld
    {
        get => showWorld;
        set => Set(ref showWorld, value);
    }

    public bool ShowMarks
    {
        get => showMarks;
        set => Set(ref showMarks, value);
    }

    public bool FeedShowSay
    {
        get => feedShowSay;
        set => Set(ref feedShowSay, value);
    }

    public bool FeedShowShout
    {
        get => feedShowShout;
        set => Set(ref feedShowShout, value);
    }

    public bool FeedShowYell
    {
        get => feedShowYell;
        set => Set(ref feedShowYell, value);
    }

    public bool FeedShowParty
    {
        get => feedShowParty;
        set => Set(ref feedShowParty, value);
    }

    public bool FeedShows(string tag) => tag switch
    {
        "SAY" => feedShowSay,
        "SHOUT" => feedShowShout,
        "YELL" => feedShowYell,
        "PARTY" => feedShowParty,
        _ => true,
    };

    public int ExtraHomeScreens
    {
        get => extraHomeScreens;
        set
        {
            var next = Math.Clamp(value, 0, 5);
            if (next < extraHomeScreens)
            {
                RetreatStudioSeats(next);
            }

            Set(ref extraHomeScreens, next);
        }
    }

    public IReadOnlyList<string> StudioWidgets => studioWidgets;

    public IReadOnlyList<string> StudioApps => studioApps;

    public string PackedStudioWidgets
    {
        get
        {
            if (studioWidgets.Length == 0)
            {
                return "-";
            }

            var parts = new string[studioWidgets.Length];
            for (var index = 0; index < studioWidgets.Length; index++)
            {
                var token = studioWidgets[index] + (HalfAt(index) ? ":2" : "");
                var seat = SeatAt(index);
                if (seat >= 0)
                {
                    token += "@" + seat.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                parts[index] = token;
            }

            return "v4:" + string.Join(',', parts);
        }
    }

    public string PackedStudioApps => string.Join(',', studioApps);

    public bool StudioWidgetHalf(string id)
    {
        var at = IndexOn(studioWidgets, id);
        return at >= 0 && HalfAt(at);
    }

    public void LoadStudioLayout(string? widgets, string? apps)
    {
        ParseStudioLayout(widgets, out studioWidgets, out studioHalf, out studioSeat);
        RelaxHalves();
        RestoreStudioDocks();
        studioApps = SanitizeHomeApps(apps);
    }

    public bool HasStudioWidget(string id) => ContainsId(studioWidgets, id);

    public int StudioWidgetSeat(string id)
    {
        var at = IndexOn(studioWidgets, id);
        return at < 0 ? -1 : SeatAt(at);
    }

    public void SetStudioWidgetSeat(string id, int seat)
    {
        var at = IndexOn(studioWidgets, id);
        if (at < 0)
        {
            return;
        }

        AlignStudioSeat();
        var next = seat < 0 ? -1 : seat;
        if (studioSeat[at] == next)
        {
            return;
        }

        studioSeat[at] = next;
        Changed?.Invoke();
    }

    public void RestoreStudioDocks()
    {
        var added = false;
        for (var index = 0; index < DefaultStudioWidgets.Length; index++)
        {
            var id = DefaultStudioWidgets[index];
            if (HasStudioWidget(id))
            {
                continue;
            }

            PlaceStudioWidget(id, -1);
            added = true;
        }

        if (!added)
        {
            return;
        }

        RelaxHalves();
    }

    public void RemoveStudioWidget(string id)
    {
        if (ContainsId(DefaultStudioWidgets, id))
        {
            return;
        }

        var at = IndexOn(studioWidgets, id);
        if (at < 0)
        {
            return;
        }

        var ids = new string[studioWidgets.Length - 1];
        var half = new bool[ids.Length];
        var seats = new int[ids.Length];
        var write = 0;
        for (var index = 0; index < studioWidgets.Length; index++)
        {
            if (index == at)
            {
                continue;
            }

            ids[write] = studioWidgets[index];
            half[write] = HalfAt(index);
            seats[write] = SeatAt(index);
            write++;
        }

        studioWidgets = ids;
        studioHalf = half;
        studioSeat = seats;
        Changed?.Invoke();
    }

    public void PlaceStudioWidget(string id) => PlaceStudioWidget(id, -1);

    public void PlaceStudioWidget(string id, int seat)
    {
        if (!ContainsId(DefaultStudioWidgets, id))
        {
            return;
        }

        if (HasStudioWidget(id))
        {
            SetStudioWidgetSeat(id, seat);
            return;
        }

        var ids = new string[studioWidgets.Length + 1];
        var half = new bool[ids.Length];
        var seats = new int[ids.Length];
        studioWidgets.CopyTo(ids, 0);
        for (var index = 0; index < studioWidgets.Length; index++)
        {
            half[index] = HalfAt(index);
            seats[index] = SeatAt(index);
        }

        ids[^1] = id;
        half[^1] = false;
        seats[^1] = seat < 0 ? -1 : seat;
        studioWidgets = ids;
        studioHalf = half;
        studioSeat = seats;
        RelaxHalves();
        Changed?.Invoke();
    }

    public void SwapStudioWidgets(string left, string right)
    {
        var from = IndexOn(studioWidgets, left);
        var to = IndexOn(studioWidgets, right);
        if (from < 0 || to < 0 || from == to)
        {
            return;
        }

        AlignStudioHalf();
        AlignStudioSeat();
        (studioWidgets[from], studioWidgets[to]) = (studioWidgets[to], studioWidgets[from]);
        (studioHalf[from], studioHalf[to]) = (studioHalf[to], studioHalf[from]);
        (studioSeat[from], studioSeat[to]) = (studioSeat[to], studioSeat[from]);
        Changed?.Invoke();
    }

    public void SeatStudioWidget(string id, string neighbor, bool left)
    {
        if (!LiftStudioWidget(id, neighbor, out var ids, out var half, out var seats, out var at))
        {
            return;
        }

        // A widget only shares a row with one mate. Dropping onto apps (or anything
        // already paired) must break that old pair or LayoutWidgets will pair the
        // leftover halves first and leave the target full-width.
        if (at > 0 && half[at] && half[at - 1])
        {
            half[at - 1] = false;
        }

        if (at + 1 < half.Count && half[at] && half[at + 1])
        {
            half[at + 1] = false;
        }

        half[at] = true;
        var insert = left ? at : at + 1;
        ids.Insert(insert, id);
        half.Insert(insert, true);
        seats.Insert(insert, seats[at]);
        studioWidgets = ids.ToArray();
        studioHalf = half.ToArray();
        studioSeat = seats.ToArray();
        RelaxHalves();
        Changed?.Invoke();
    }

    public void StackStudioWidget(string id, string neighbor, bool above)
    {
        if (!LiftStudioWidget(id, neighbor, out var ids, out var half, out var seats, out var at))
        {
            return;
        }

        half[at] = false;
        var insert = above ? at : at + 1;
        ids.Insert(insert, id);
        half.Insert(insert, false);
        seats.Insert(insert, seats[at]);
        studioWidgets = ids.ToArray();
        studioHalf = half.ToArray();
        studioSeat = seats.ToArray();
        RelaxHalves();
        Changed?.Invoke();
    }

    public void SwapStudioApps(string left, string right)
    {
        if (!SwapIds(studioApps, left, right))
        {
            return;
        }

        Changed?.Invoke();
    }

    public bool CanPlaceHomeApp(string id) =>
        CanBeHomeApp(id) && (string.Equals(id, "party", StringComparison.Ordinal) || IsOwned(id));

    public void ReplaceStudioApp(string id, int slot)
    {
        if (!CanPlaceHomeApp(id))
        {
            return;
        }

        studioApps = HomeAppSlots(studioApps);
        slot = Math.Clamp(slot, 0, studioApps.Length - 1);
        var have = IndexOn(studioApps, id);
        if (have == slot)
        {
            return;
        }

        if (have >= 0)
        {
            (studioApps[have], studioApps[slot]) = (studioApps[slot], studioApps[have]);
            Changed?.Invoke();
            return;
        }

        var displaced = studioApps[slot];
        studioApps[slot] = id;
        if (displaced.Length > 0 && PageOf(displaced) < 0 && CanPlaceHomeApp(displaced))
        {
            PlaceAppAt(displaced, 0, AppsOnScreen(0).Count);
        }

        Changed?.Invoke();
    }

    public void PlaceStudioApp(string id)
    {
        if (!CanPlaceHomeApp(id) || ContainsId(studioApps, id))
        {
            return;
        }

        studioApps = HomeAppSlots(studioApps);
        ReplaceStudioApp(id, studioApps.Length - 1);
    }

    public void RemoveStudioApp(string id)
    {
        var at = IndexOn(studioApps, id);
        if (at < 0)
        {
            return;
        }

        studioApps = HomeAppSlots(studioApps);
        at = IndexOn(studioApps, id);
        if (at < 0)
        {
            return;
        }

        var fill = UnusedOwnedHomeApp(studioApps, id);
        studioApps[at] = fill;
        Changed?.Invoke();
    }

    public bool ReduceMotion
    {
        get => reduceMotion;
        set => Set(ref reduceMotion, value);
    }

    public bool Quiet
    {
        get => quiet;
        set => Set(ref quiet, value);
    }

    public bool QuietWhenBusy
    {
        get => quietWhenBusy;
        set => Set(ref quietWhenBusy, value);
    }

    public bool Hushed(bool busy) => quiet || (quietWhenBusy && busy);

    public bool WakeInPocket
    {
        get => wakeInPocket;
        set => Set(ref wakeInPocket, value);
    }

    public bool StayInPortraits
    {
        get => stayInPortraits;
        set => Set(ref stayInPortraits, value);
    }

    public bool TuckForCutscenes
    {
        get => tuckForCutscenes;
        set => Set(ref tuckForCutscenes, value);
    }

    public FightPresence Fight
    {
        get => fight;
        set => Set(ref fight, value);
    }

    public TuneLayout Layout
    {
        get => layout;
        set => Set(ref layout, value);
    }

    public bool UsingList => layout == TuneLayout.List;

    public float Brightness
    {
        get => brightness;
        set => Set(ref brightness, Math.Clamp(value, 0.20f, 1f));
    }

    public float Volume
    {
        get => volume;
        set => Set(ref volume, Math.Clamp(value, 0f, 1f));
    }

    public float MicVolume
    {
        get => micVolume;
        set => Set(ref micVolume, Math.Clamp(value, 0f, 1f));
    }

    public string SpeakerId
    {
        get => speakerId;
        set => Set(ref speakerId, value ?? string.Empty);
    }

    public string MicrophoneId
    {
        get => microphoneId;
        set => Set(ref microphoneId, value ?? string.Empty);
    }

    public string CallSpeakerId
    {
        get => callSpeakerId;
        set => Set(ref callSpeakerId, value ?? string.Empty);
    }

    public string CallMicrophoneId
    {
        get => callMicrophoneId;
        set => Set(ref callMicrophoneId, value ?? string.Empty);
    }

    public string ActiveCallSpeaker => callSpeakerId.Length > 0 ? callSpeakerId : speakerId;

    public string ActiveCallMicrophone => callMicrophoneId.Length > 0 ? callMicrophoneId : microphoneId;

    public bool AutoRotate
    {
        get => autoRotate;
        set => Set(ref autoRotate, value);
    }

    public bool Landscape
    {
        get => landscape;
        set => Set(ref landscape, value);
    }

    public string PictureDraft
    {
        get => pictureDraft;
        set => pictureDraft = value ?? string.Empty;
    }

    public string BannerDraft
    {
        get => bannerDraft;
        set => bannerDraft = value ?? string.Empty;
    }

    public IReadOnlyList<string> Replies => replies;

    public void SetReplies(IReadOnlyList<string> next)
    {
        var copy = new string[Math.Min(next.Count, 6)];
        var count = 0;
        for (var index = 0; index < next.Count && count < copy.Length; index++)
        {
            var line = next[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            copy[count] = line;
            count++;
        }

        if (count == 0)
        {
            copy = DefaultReplies;
            count = copy.Length;
        }
        else if (count != copy.Length)
        {
            Array.Resize(ref copy, count);
        }

        replies = copy;
        Changed?.Invoke();
    }

    public IReadOnlyList<string> InstalledApps => installedApps ?? AppShelf.DefaultInstalled;

    public IReadOnlyList<string> SeenShelfApps => seenShelfApps;

    public IReadOnlyList<string> OwnedApps => ownedApps.Length > 0 ? ownedApps : InstalledApps;

    public IReadOnlyList<string> FavoriteApps => favoriteApps;

    public IReadOnlyList<string> AppFolders => appFolders;

    public const int QuickAppSlots = 4;

    public const int AppScreenCap = 6;

    public IReadOnlyList<string> QuickApps => quickApps;

    public int AppScreenCount => Math.Max(1, appPages.Length);

    public IReadOnlyList<string> AppsOnScreen(int screen)
    {
        var pages = appPages;
        if (pages.Length == 0)
        {
            return InstalledApps;
        }

        var index = Math.Clamp(screen, 0, pages.Length - 1);
        return pages[index];
    }

    public string[] PackedAppScreens()
    {
        var packed = new string[AppScreenCount];
        for (var index = 0; index < packed.Length; index++)
        {
            packed[index] = string.Join(',', AppsOnScreen(index));
        }

        return packed;
    }

    public void LoadAppShelf(string[]? installed, string[]? favorites, string[]? folders, string[]? quick,
        string[]? seen, string[]? screens = null, string[]? owned = null)
    {
        favoriteApps = SanitizeIds(favorites ?? [], allowFolder: true);
        appFolders = folders ?? [];
        seenShelfApps = SanitizeIds(seen ?? [], allowFolder: false);
        appPages = LoadPages(installed, screens);
        installedApps = FlattenPages(appPages);
        ownedApps = SanitizeIds(owned ?? [], allowFolder: false);
        GraftNewDefaultApps();
        EnsureOwnedFromShelf();
        LoadQuickApps(quick is { Length: > 0 } ? quick : favoriteApps);
    }

    private void GraftNewDefaultApps()
    {
        var seen = new HashSet<string>(seenShelfApps, StringComparer.Ordinal);
        if (seen.Count == 0)
        {
            var current = InstalledApps;
            for (var index = 0; index < current.Count; index++)
            {
                seen.Add(current[index]);
            }
        }

        var added = false;
        List<string>? next = null;
        for (var index = 0; index < AppShelf.Catalog.Length; index++)
        {
            var spec = AppShelf.Catalog[index];
            if (!spec.OnShelfByDefault || seen.Contains(spec.Id))
            {
                continue;
            }

            seen.Add(spec.Id);
            if (IsOnShelf(spec.Id))
            {
                continue;
            }

            next ??= new List<string>(AppsOnScreen(0));
            next.Add(spec.Id);
            added = true;
        }

        if (added && next is not null)
        {
            ReplacePage(0, next.ToArray(), notify: false);
        }

        seenShelfApps = new string[seen.Count];
        seen.CopyTo(seenShelfApps);
    }

    private void EnsureOwnedFromShelf()
    {
        var next = new List<string>(ownedApps);
        var shelf = InstalledApps;
        var first = ownedApps.Length == 0;
        var changed = first;
        for (var index = 0; index < shelf.Count; index++)
        {
            var id = shelf[index];
            if (id.StartsWith("folder:", StringComparison.Ordinal) || ContainsId(next, id))
            {
                continue;
            }

            next.Add(id);
            changed = true;
        }

        if (first)
        {
            for (var index = 0; index < AppShelf.DefaultInstalled.Length; index++)
            {
                var id = AppShelf.DefaultInstalled[index];
                if (ContainsId(next, id))
                {
                    continue;
                }

                next.Add(id);
                changed = true;
            }
        }

        if (changed)
        {
            ownedApps = next.ToArray();
        }
    }

    public void LoadQuickApps(IReadOnlyList<string> ids)
    {
        var next = new string[QuickAppSlots];
        var source = SanitizeIds(ids, allowFolder: false);
        var write = 0;
        for (var index = 0; index < source.Length && write < QuickAppSlots; index++)
        {
            next[write++] = source[index];
        }

        for (var index = write; index < QuickAppSlots; index++)
        {
            next[index] = string.Empty;
        }

        quickApps = next;
    }

    public bool IsPlaced(string id) => IsOnShelf(id) || IsNested(id);

    public bool IsNested(string id)
    {
        if (id.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < appFolders.Length; index++)
        {
            var parts = appFolders[index].Split('\u001f');
            if (parts.Length < 3 || parts[2].Length == 0)
            {
                continue;
            }

            foreach (var child in parts[2].Split(','))
            {
                if (string.Equals(child, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool IsOnShelf(string id)
    {
        var shelf = InstalledApps;
        for (var index = 0; index < shelf.Count; index++)
        {
            if (string.Equals(shelf[index], id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsFavorite(string id)
    {
        for (var index = 0; index < favoriteApps.Length; index++)
        {
            if (string.Equals(favoriteApps[index], id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsOwned(string id)
    {
        if (id.Length == 0 || id.StartsWith("folder:", StringComparison.Ordinal))
        {
            return false;
        }

        return ContainsId(OwnedApps, id);
    }

    public void AcquireApp(string id)
    {
        if (AppShelf.Find(id) is null || IsOwned(id))
        {
            return;
        }

        var next = new List<string>(OwnedApps) { id };
        ownedApps = next.ToArray();
        Changed?.Invoke();
    }

    public void UninstallApp(string id)
    {
        if (id.Length == 0 || string.Equals(id, "appstore", StringComparison.Ordinal))
        {
            return;
        }

        StripFromFolders(id);
        if (IsOnShelf(id))
        {
            RemoveApp(id);
        }

        if (!IsOwned(id))
        {
            StripStudioApp(id);
            return;
        }

        var next = new List<string>(OwnedApps.Count);
        for (var index = 0; index < OwnedApps.Count; index++)
        {
            if (!string.Equals(OwnedApps[index], id, StringComparison.Ordinal))
            {
                next.Add(OwnedApps[index]);
            }
        }

        ownedApps = next.ToArray();
        DropQuickApp(id);
        StripStudioApp(id);
        if (IsFavorite(id))
        {
            ToggleFavorite(id);
            return;
        }

        Changed?.Invoke();
    }

    public void InstallApp(string id)
    {
        if (id.Length == 0 || IsOnShelf(id))
        {
            return;
        }

        AcquireApp(id);
        var page = SlotArray(AppsOnScreen(0), 0);
        var hole = FirstEmpty(page);
        if (hole >= 0)
        {
            page[hole] = id;
            ReplacePage(0, page);
            return;
        }

        var grown = new string[page.Length + 1];
        page.CopyTo(grown, 0);
        grown[^1] = id;
        ReplacePage(0, grown);
    }

    public void RemoveApp(string id)
    {
        if (id.Length == 0 || !IsOnShelf(id))
        {
            return;
        }

        var unpacked = Array.Empty<string>();
        if (TryFolder(id, out _, out var children))
        {
            unpacked = children;
            var keptFolders = new List<string>(appFolders.Length);
            for (var index = 0; index < appFolders.Length; index++)
            {
                if (!appFolders[index].StartsWith(id + "\u001f", StringComparison.Ordinal))
                {
                    keptFolders.Add(appFolders[index]);
                }
            }

            appFolders = keptFolders.ToArray();
        }

        var pageIndex = PageOf(id);
        if (pageIndex < 0)
        {
            return;
        }

        var page = SlotArray(AppsOnScreen(pageIndex), 0);
        for (var index = 0; index < page.Length; index++)
        {
            if (string.Equals(page[index], id, StringComparison.Ordinal))
            {
                page[index] = string.Empty;
            }
        }

        for (var child = 0; child < unpacked.Length; child++)
        {
            var nested = unpacked[child];
            if (nested.Length == 0 || ContainsId(page, nested))
            {
                continue;
            }

            var hole = FirstEmpty(page);
            if (hole >= 0)
            {
                page[hole] = nested;
                continue;
            }

            var grown = new string[page.Length + 1];
            page.CopyTo(grown, 0);
            grown[^1] = nested;
            page = grown;
        }

        ReplacePage(pageIndex, page, notify: false);
        DropQuickApp(id);
        if (IsFavorite(id))
        {
            ToggleFavorite(id);
            return;
        }

        Changed?.Invoke();
    }

    public void MoveApp(string id, int delta)
    {
        var pageIndex = PageOf(id);
        if (pageIndex < 0)
        {
            return;
        }

        var current = AppsOnScreen(pageIndex);
        var from = IndexOn(current, id);
        if (from < 0)
        {
            return;
        }

        var to = Math.Clamp(from + delta, 0, current.Count - 1);
        if (to == from)
        {
            return;
        }

        var next = CopyIds(current);
        (next[from], next[to]) = (next[to], next[from]);
        ReplacePage(pageIndex, next);
    }

    public void MoveAppTo(string id, int toIndex)
    {
        var pageIndex = PageOf(id);
        if (pageIndex < 0)
        {
            return;
        }

        PlaceAppAt(id, pageIndex, toIndex);
    }

    public void PlaceAppAt(string id, int screen, int slot)
    {
        if (id.Length == 0 || slot < 0)
        {
            return;
        }

        var to = Math.Clamp(screen, 0, Math.Max(0, AppScreenCount - 1));
        var from = PageOf(id);
        var pages = CopyPages();
        if (from >= 0 && from != to)
        {
            var source = SlotArray(pages[from], 0);
            var at = IndexOn(source, id);
            if (at >= 0)
            {
                source[at] = string.Empty;
            }

            pages[from] = source;
        }

        var dest = SlotArray(from == to ? pages[to] : pages[to], slot + 1);
        if (from == to)
        {
            dest = SlotArray(pages[to], slot + 1);
        }

        var existing = IndexOn(dest, id);
        if (existing == slot)
        {
            pages[to] = dest;
            CommitPages(pages);
            return;
        }

        if (existing >= 0)
        {
            (dest[existing], dest[slot]) = (dest[slot], dest[existing]);
        }
        else
        {
            dest[slot] = id;
        }

        pages[to] = dest;
        CommitPages(pages);
    }

    public void MoveAppToScreen(string id, int screen)
    {
        var from = PageOf(id);
        var to = Math.Clamp(screen, 0, Math.Max(0, AppScreenCount - 1));
        if (from < 0 || from == to)
        {
            return;
        }

        var pages = CopyPages();
        var source = SlotArray(pages[from], 0);
        var at = IndexOn(source, id);
        if (at >= 0)
        {
            source[at] = string.Empty;
        }

        var dest = SlotArray(pages[to], 0);
        var hole = FirstEmpty(dest);
        if (hole >= 0)
        {
            dest[hole] = id;
        }
        else
        {
            var grown = new string[dest.Length + 1];
            dest.CopyTo(grown, 0);
            grown[^1] = id;
            dest = grown;
        }

        pages[from] = source;
        pages[to] = dest;
        CommitPages(pages);
    }

    public void ToggleFavorite(string id)
    {
        if (id.Length == 0)
        {
            return;
        }

        if (IsFavorite(id))
        {
            var next = new string[favoriteApps.Length - 1];
            var write = 0;
            for (var index = 0; index < favoriteApps.Length; index++)
            {
                if (!string.Equals(favoriteApps[index], id, StringComparison.Ordinal))
                {
                    next[write++] = favoriteApps[index];
                }
            }

            favoriteApps = next;
        }
        else
        {
            var next = new string[favoriteApps.Length + 1];
            favoriteApps.CopyTo(next, 0);
            next[^1] = id;
            favoriteApps = next;
        }

        Changed?.Invoke();
    }

    public bool IsQuickApp(string id)
    {
        if (id.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < quickApps.Length; index++)
        {
            if (string.Equals(quickApps[index], id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public void PlaceQuickApp(int slot, string id)
    {
        if (slot < 0 || slot >= QuickAppSlots)
        {
            return;
        }

        var value = (id ?? string.Empty).Trim();
        if (value.StartsWith("folder:", StringComparison.Ordinal))
        {
            return;
        }

        if (value.Length > 0 && AppShelf.Find(value) is null)
        {
            return;
        }

        var next = (string[])quickApps.Clone();
        if (value.Length > 0)
        {
            for (var index = 0; index < next.Length; index++)
            {
                if (index != slot && string.Equals(next[index], value, StringComparison.Ordinal))
                {
                    next[index] = string.Empty;
                }
            }
        }

        if (string.Equals(next[slot], value, StringComparison.Ordinal))
        {
            return;
        }

        next[slot] = value;
        quickApps = next;
        Changed?.Invoke();
    }

    public void ClearQuickApp(int slot) => PlaceQuickApp(slot, string.Empty);

    public bool TryAddQuickApp(string id)
    {
        if (id.Length == 0 || IsQuickApp(id) || AppShelf.Find(id) is null)
        {
            return false;
        }

        for (var index = 0; index < quickApps.Length; index++)
        {
            if (quickApps[index].Length == 0)
            {
                PlaceQuickApp(index, id);
                return true;
            }
        }

        return false;
    }

    public void DropQuickApp(string id)
    {
        if (id.Length == 0)
        {
            return;
        }

        var next = (string[])quickApps.Clone();
        var changed = false;
        for (var index = 0; index < next.Length; index++)
        {
            if (string.Equals(next[index], id, StringComparison.Ordinal))
            {
                next[index] = string.Empty;
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        quickApps = next;
        Changed?.Invoke();
    }

    public void CreateAppFolder(IReadOnlyList<string> childIds)
    {
        var children = SanitizeIds(childIds, allowFolder: false);
        if (childIds.Count > 0 && children.Length == 0)
        {
            return;
        }

        for (var child = 0; child < children.Length; child++)
        {
            StripFromFolders(children[child]);
        }

        var id = "folder:" + Guid.NewGuid().ToString("N")[..8];
        var name = "Folder " + (appFolders.Length + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var packed = id + "\u001f" + name + "\u001f" + string.Join(',', children);
        var folders = new string[appFolders.Length + 1];
        appFolders.CopyTo(folders, 0);
        folders[^1] = packed;
        appFolders = folders;

        var pageIndex = 0;
        for (var child = 0; child < children.Length; child++)
        {
            var found = PageOf(children[child]);
            if (found >= 0)
            {
                pageIndex = found;
                break;
            }
        }

        var current = AppsOnScreen(pageIndex);
        var shelf = new List<string>(current.Count);
        var insertAt = current.Count;
        for (var index = 0; index < current.Count; index++)
        {
            var item = current[index];
            var skip = false;
            for (var child = 0; child < children.Length; child++)
            {
                if (string.Equals(item, children[child], StringComparison.Ordinal))
                {
                    skip = true;
                    if (index < insertAt)
                    {
                        insertAt = index;
                    }

                    break;
                }
            }

            if (!skip)
            {
                shelf.Add(item);
            }
        }

        insertAt = Math.Clamp(insertAt, 0, shelf.Count);
        shelf.Insert(insertAt, id);
        var pages = CopyPages();
        pages[pageIndex] = shelf.ToArray();
        for (var page = 0; page < pages.Length; page++)
        {
            if (page == pageIndex)
            {
                continue;
            }

            pages[page] = WithoutIds(pages[page], children);
        }

        CommitPages(pages);
    }

    public void CreateEmptyFolder(int screen)
    {
        var pageIndex = Math.Clamp(screen, 0, Math.Max(0, AppScreenCount - 1));
        var id = "folder:" + Guid.NewGuid().ToString("N")[..8];
        var name = "Folder " + (appFolders.Length + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var packed = id + "\u001f" + name + "\u001f";
        var folders = new string[appFolders.Length + 1];
        appFolders.CopyTo(folders, 0);
        folders[^1] = packed;
        appFolders = folders;
        var page = new List<string>(AppsOnScreen(pageIndex)) { id };
        ReplacePage(pageIndex, page.ToArray());
    }

    public bool TryAddAppScreen()
    {
        if (AppScreenCount >= AppScreenCap)
        {
            return false;
        }

        var pages = CopyPages();
        var next = new string[pages.Length + 1][];
        pages.CopyTo(next, 0);
        next[^1] = [];
        CommitPages(next);
        return true;
    }

    public bool RemoveAppScreen(int index)
    {
        if (index <= 0 || index >= AppScreenCount)
        {
            return false;
        }

        var home = new List<string>(AppsOnScreen(0));
        var leaving = AppsOnScreen(index);
        for (var item = 0; item < leaving.Count; item++)
        {
            if (!ContainsId(home, leaving[item]))
            {
                home.Add(leaving[item]);
            }
        }

        var pages = new List<string[]>(AppScreenCount - 1);
        for (var page = 0; page < appPages.Length; page++)
        {
            if (page == index)
            {
                continue;
            }

            pages.Add(page == 0 ? home.ToArray() : CopyIds(appPages[page]));
        }

        CommitPages(pages.ToArray());
        return true;
    }

    public void PruneEmptyAppScreens(int keep = -1)
    {
        if (AppScreenCount <= 1)
        {
            return;
        }

        var kept = new List<string[]>(AppScreenCount);
        for (var index = 0; index < appPages.Length; index++)
        {
            if (index == 0 || index == keep || appPages[index].Length > 0)
            {
                kept.Add(appPages[index]);
            }
        }

        if (kept.Count == appPages.Length)
        {
            return;
        }

        CommitPages(kept.ToArray());
    }

    public void NestInFolder(string folderId, string childId)
    {
        if (!TryFolder(folderId, out var name, out var children) || childId.Length == 0 ||
            childId.StartsWith("folder:", StringComparison.Ordinal) || AppShelf.Find(childId) is null)
        {
            return;
        }

        for (var index = 0; index < children.Length; index++)
        {
            if (string.Equals(children[index], childId, StringComparison.Ordinal))
            {
                return;
            }
        }

        StripFromFolders(childId);
        var nextChildren = new string[children.Length + 1];
        children.CopyTo(nextChildren, 0);
        nextChildren[^1] = childId;
        WriteFolder(folderId, name, nextChildren);
        DropFromShelf(childId);
        Changed?.Invoke();
    }

    public void DropFromFolder(string folderId, string childId)
    {
        if (!TryFolder(folderId, out var name, out var children))
        {
            return;
        }

        var next = new List<string>(children.Length);
        var found = false;
        for (var index = 0; index < children.Length; index++)
        {
            if (string.Equals(children[index], childId, StringComparison.Ordinal))
            {
                found = true;
                continue;
            }

            next.Add(children[index]);
        }

        if (!found)
        {
            return;
        }

        WriteFolder(folderId, name, next.ToArray());
        if (!IsOnShelf(childId))
        {
            InstallApp(childId);
            return;
        }

        Changed?.Invoke();
    }

    public void MoveFolderChild(string folderId, string childId, int toIndex)
    {
        if (!TryFolder(folderId, out var name, out var children))
        {
            return;
        }

        var from = -1;
        for (var index = 0; index < children.Length; index++)
        {
            if (string.Equals(children[index], childId, StringComparison.Ordinal))
            {
                from = index;
                break;
            }
        }

        if (from < 0)
        {
            return;
        }

        var to = Math.Clamp(toIndex, 0, children.Length - 1);
        if (to == from)
        {
            return;
        }

        var next = new List<string>(children.Length);
        for (var index = 0; index < children.Length; index++)
        {
            if (index != from)
            {
                next.Add(children[index]);
            }
        }

        next.Insert(to, childId);
        WriteFolder(folderId, name, next.ToArray());
        Changed?.Invoke();
    }

    public bool TryFolder(string id, out string name, out string[] children)
    {
        name = string.Empty;
        children = [];
        for (var index = 0; index < appFolders.Length; index++)
        {
            var parts = appFolders[index].Split('\u001f');
            if (parts.Length < 3 || !string.Equals(parts[0], id, StringComparison.Ordinal))
            {
                continue;
            }

            name = parts[1];
            children = parts[2].Length == 0 ? [] : parts[2].Split(',');
            return true;
        }

        return false;
    }

    private void StripFromFolders(string childId)
    {
        if (childId.Length == 0)
        {
            return;
        }

        for (var index = 0; index < appFolders.Length; index++)
        {
            var parts = appFolders[index].Split('\u001f');
            if (parts.Length < 3 || parts[2].Length == 0)
            {
                continue;
            }

            var kids = parts[2].Split(',');
            var next = new List<string>(kids.Length);
            var changed = false;
            for (var child = 0; child < kids.Length; child++)
            {
                if (string.Equals(kids[child], childId, StringComparison.Ordinal))
                {
                    changed = true;
                    continue;
                }

                next.Add(kids[child]);
            }

            if (changed)
            {
                appFolders[index] = parts[0] + "\u001f" + parts[1] + "\u001f" + string.Join(',', next);
            }
        }
    }

    private void WriteFolder(string folderId, string name, string[] children)
    {
        for (var index = 0; index < appFolders.Length; index++)
        {
            var parts = appFolders[index].Split('\u001f');
            if (parts.Length < 3 || !string.Equals(parts[0], folderId, StringComparison.Ordinal))
            {
                continue;
            }

            appFolders[index] = folderId + "\u001f" + name + "\u001f" + string.Join(',', children);
            return;
        }
    }

    private void DropFromShelf(string id)
    {
        var pageIndex = PageOf(id);
        if (pageIndex < 0)
        {
            return;
        }

        ReplacePage(pageIndex, WithoutIds(AppsOnScreen(pageIndex), [id]), notify: false);
        DropQuickApp(id);
    }

    private string[][] LoadPages(string[]? installed, string[]? screens)
    {
        if (screens is { Length: > 0 })
        {
            var count = Math.Clamp(screens.Length, 1, AppScreenCap);
            var pages = new string[count][];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < count; index++)
            {
                var ids = SanitizeSlots((screens[index] ?? string.Empty).Split(','));
                var unique = new List<string>(ids.Length);
                for (var item = 0; item < ids.Length; item++)
                {
                    if (seen.Add(ids[item]))
                    {
                        unique.Add(ids[item]);
                    }
                }

                pages[index] = unique.ToArray();
            }

            if (installed is not null)
            {
                var leftover = SanitizeIds(installed, allowFolder: true);
                var home = new List<string>(pages[0]);
                for (var index = 0; index < leftover.Length; index++)
                {
                    if (seen.Add(leftover[index]))
                    {
                        home.Add(leftover[index]);
                    }
                }

                pages[0] = home.ToArray();
            }

            return pages;
        }

        return [installed is null ? AppShelf.DefaultInstalled : SanitizeIds(installed, allowFolder: true)];
    }

    private void MoveAppOnPage(int pageIndex, string id, int toIndex)
    {
        var current = AppsOnScreen(pageIndex);
        var from = IndexOn(current, id);
        if (from < 0)
        {
            return;
        }

        var to = Math.Clamp(toIndex, 0, current.Count - 1);
        if (to == from)
        {
            return;
        }

        var next = new List<string>(current.Count);
        for (var index = 0; index < current.Count; index++)
        {
            if (index != from)
            {
                next.Add(current[index]);
            }
        }

        next.Insert(to, id);
        ReplacePage(pageIndex, next.ToArray());
    }

    private void ReplacePage(int pageIndex, string[] ids, bool notify = true)
    {
        var pages = CopyPages();
        var index = Math.Clamp(pageIndex, 0, pages.Length - 1);
        pages[index] = SanitizeSlots(ids);
        CommitPages(pages, notify);
    }

    private void CommitPages(string[][] pages, bool notify = true)
    {
        appPages = pages.Length == 0 ? [AppShelf.DefaultInstalled] : pages;
        installedApps = FlattenPages(appPages);
        if (notify)
        {
            Changed?.Invoke();
        }
    }

    private string[][] CopyPages()
    {
        var pages = appPages.Length == 0 ? [CopyIds(InstalledApps)] : appPages;
        var copy = new string[pages.Length][];
        for (var index = 0; index < pages.Length; index++)
        {
            copy[index] = CopyIds(pages[index]);
        }

        return copy;
    }

    private int PageOf(string id)
    {
        for (var page = 0; page < appPages.Length; page++)
        {
            if (IndexOn(appPages[page], id) >= 0)
            {
                return page;
            }
        }

        return -1;
    }

    private static bool ContainsId(IReadOnlyList<string> ids, string id) => IndexOn(ids, id) >= 0;

    private static int IndexOn(IReadOnlyList<string> ids, string id)
    {
        for (var index = 0; index < ids.Count; index++)
        {
            if (string.Equals(ids[index], id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static string[] CopyIds(IReadOnlyList<string> ids)
    {
        var copy = new string[ids.Count];
        for (var index = 0; index < ids.Count; index++)
        {
            copy[index] = ids[index];
        }

        return copy;
    }

    private static string[] WithoutIds(IReadOnlyList<string> ids, IReadOnlyList<string> drop)
    {
        var next = new List<string>(ids.Count);
        for (var index = 0; index < ids.Count; index++)
        {
            var skip = false;
            for (var other = 0; other < drop.Count; other++)
            {
                if (string.Equals(ids[index], drop[other], StringComparison.Ordinal))
                {
                    skip = true;
                    break;
                }
            }

            if (!skip)
            {
                next.Add(ids[index]);
            }
        }

        return next.ToArray();
    }

    private static string[] FlattenPages(string[][] pages)
    {
        var all = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var page = 0; page < pages.Length; page++)
        {
            for (var index = 0; index < pages[page].Length; index++)
            {
                if (pages[page][index].Length > 0 && seen.Add(pages[page][index]))
                {
                    all.Add(pages[page][index]);
                }
            }
        }

        return all.ToArray();
    }

    private static string[] SanitizeIds(IReadOnlyList<string> ids, bool allowFolder)
    {
        var kept = new List<string>(ids.Count);
        for (var index = 0; index < ids.Count; index++)
        {
            var id = (ids[index] ?? string.Empty).Trim();
            if (id.Length == 0)
            {
                continue;
            }

            var folder = id.StartsWith("folder:", StringComparison.Ordinal);
            if (folder && !allowFolder)
            {
                continue;
            }

            if (!folder && AppShelf.Find(id) is null)
            {
                continue;
            }

            var seen = false;
            for (var prior = 0; prior < kept.Count; prior++)
            {
                if (string.Equals(kept[prior], id, StringComparison.Ordinal))
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
            {
                kept.Add(id);
            }
        }

        return kept.ToArray();
    }

    private static string[] SanitizePlates(IReadOnlyList<string> files)
    {
        var names = new List<string>(PlateFiles.MaxSlots);
        for (var index = 0; index < files.Count && names.Count < PlateFiles.MaxSlots; index++)
        {
            var name = Path.GetFileName(files[index] ?? string.Empty);
            if (name.Length == 0)
            {
                continue;
            }

            var seen = false;
            for (var prior = 0; prior < names.Count; prior++)
            {
                if (string.Equals(names[prior], name, StringComparison.OrdinalIgnoreCase))
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
            {
                names.Add(name);
            }
        }

        return names.ToArray();
    }

    private static bool PlatesEqual(string[] left, string[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void Set(ref bool field, bool value)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Changed?.Invoke();
    }

    private void Set<T>(ref T field, T value) where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        Changed?.Invoke();
    }

    private void Set(ref string field, string value)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        Changed?.Invoke();
    }

    private static bool SwapIds(string[] ids, string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return false;
        }

        var from = -1;
        var to = -1;
        for (var index = 0; index < ids.Length; index++)
        {
            if (string.Equals(ids[index], left, StringComparison.Ordinal))
            {
                from = index;
            }

            if (string.Equals(ids[index], right, StringComparison.Ordinal))
            {
                to = index;
            }
        }

        if (from < 0 || to < 0)
        {
            return false;
        }

        (ids[from], ids[to]) = (ids[to], ids[from]);
        return true;
    }

    private bool HalfAt(int index) =>
        index >= 0 && index < studioHalf.Length && studioHalf[index];

    private int SeatAt(int index) =>
        index >= 0 && index < studioSeat.Length ? studioSeat[index] : -1;

    private void AlignStudioHalf()
    {
        if (studioHalf.Length == studioWidgets.Length)
        {
            return;
        }

        studioHalf = DefaultHalves(studioWidgets);
    }

    private void AlignStudioSeat()
    {
        if (studioSeat.Length == studioWidgets.Length)
        {
            return;
        }

        studioSeat = new int[studioWidgets.Length];
        for (var index = 0; index < studioSeat.Length; index++)
        {
            studioSeat[index] = -1;
        }
    }

    private void RetreatStudioSeats(int extras)
    {
        AlignStudioSeat();
        var changed = false;
        for (var index = 0; index < studioSeat.Length; index++)
        {
            if (studioSeat[index] < extras)
            {
                continue;
            }

            studioSeat[index] = -1;
            changed = true;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    private bool LiftStudioWidget(string id, string neighbor, out List<string> ids, out List<bool> half,
        out List<int> seats, out int at)
    {
        ids = [];
        half = [];
        seats = [];
        at = -1;
        var from = IndexOn(studioWidgets, id);
        if (from < 0 || IndexOn(studioWidgets, neighbor) < 0 || string.Equals(id, neighbor, StringComparison.Ordinal))
        {
            return false;
        }

        AlignStudioHalf();
        AlignStudioSeat();
        for (var index = 0; index < studioWidgets.Length; index++)
        {
            if (index == from)
            {
                continue;
            }

            ids.Add(studioWidgets[index]);
            half.Add(studioHalf[index]);
            seats.Add(studioSeat[index]);
        }

        at = ids.IndexOf(neighbor);
        return at >= 0;
    }

    private void RelaxHalves()
    {
        AlignStudioHalf();
        for (var index = 0; index < studioWidgets.Length; index++)
        {
            var mate = (index > 0 && studioHalf[index] && studioHalf[index - 1]) ||
                       (index + 1 < studioWidgets.Length && studioHalf[index] && studioHalf[index + 1]);
            if (!mate)
            {
                studioHalf[index] = false;
            }
        }
    }

    private static void ParseStudioLayout(string? packed, out string[] ids, out bool[] half, out int[] seats)
    {
        if (string.IsNullOrWhiteSpace(packed) ||
            !(packed.StartsWith("v3:", StringComparison.Ordinal) ||
              packed.StartsWith("v4:", StringComparison.Ordinal)))
        {
            ids = (string[])DefaultStudioWidgets.Clone();
            half = (bool[])DefaultStudioHalf.Clone();
            seats = EmptySeats(ids.Length);
            return;
        }

        packed = packed[3..];
        if (string.Equals(packed.Trim(), "-", StringComparison.Ordinal))
        {
            ids = [];
            half = [];
            seats = [];
            return;
        }

        var kept = new List<string>(DefaultStudioWidgets.Length);
        var marks = new List<bool>(DefaultStudioWidgets.Length);
        var parked = new List<int>(DefaultStudioWidgets.Length);
        var anySpan = false;
        var anySeat = false;
        var parts = packed.Split(',', StringSplitOptions.TrimEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            var token = parts[index];
            var seat = -1;
            var at = token.LastIndexOf('@');
            if (at > 0 && int.TryParse(token[(at + 1)..], out var parsed) && parsed >= 0)
            {
                seat = parsed;
                token = token[..at];
                anySeat = true;
            }

            var pair = token.EndsWith(":2", StringComparison.Ordinal);
            var id = pair ? token[..^2] : token;
            if (!ContainsId(DefaultStudioWidgets, id) || ContainsId(kept, id))
            {
                continue;
            }

            kept.Add(id);
            marks.Add(pair);
            parked.Add(seat);
            anySpan |= pair;
        }

        if (kept.Count == 0)
        {
            ids = (string[])DefaultStudioWidgets.Clone();
            half = (bool[])DefaultStudioHalf.Clone();
            seats = EmptySeats(ids.Length);
            return;
        }

        if (!anySpan && !anySeat && SameStudioSet(kept))
        {
            ids = (string[])DefaultStudioWidgets.Clone();
            half = (bool[])DefaultStudioHalf.Clone();
            seats = EmptySeats(ids.Length);
            return;
        }

        ids = kept.ToArray();
        half = anySpan ? marks.ToArray() : DefaultHalves(ids);
        seats = parked.ToArray();
    }

    private static int[] EmptySeats(int count)
    {
        var seats = new int[count];
        for (var index = 0; index < seats.Length; index++)
        {
            seats[index] = -1;
        }

        return seats;
    }

    private static bool[] DefaultHalves(IReadOnlyList<string> ids)
    {
        var half = new bool[ids.Count];
        for (var index = 0; index < ids.Count; index++)
        {
            half[index] = ids[index] is "announcements" or "apps";
        }

        return half;
    }

    private static bool SameStudioSet(IReadOnlyList<string> ids)
    {
        if (ids.Count != DefaultStudioWidgets.Length)
        {
            return false;
        }

        for (var index = 0; index < DefaultStudioWidgets.Length; index++)
        {
            if (!ContainsId(ids, DefaultStudioWidgets[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string[] SlotArray(IReadOnlyList<string> ids, int minLength)
    {
        var count = Math.Max(ids.Count, minLength);
        var copy = new string[count];
        for (var index = 0; index < count; index++)
        {
            copy[index] = index < ids.Count ? ids[index] ?? string.Empty : string.Empty;
        }

        return copy;
    }

    private static int FirstEmpty(IReadOnlyList<string> ids)
    {
        for (var index = 0; index < ids.Count; index++)
        {
            if (ids[index].Length == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static string[] SanitizeSlots(IReadOnlyList<string> ids)
    {
        var kept = new List<string>(ids.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < ids.Count; index++)
        {
            var id = (ids[index] ?? string.Empty).Trim();
            if (id.Length == 0)
            {
                kept.Add(string.Empty);
                continue;
            }

            var folder = id.StartsWith("folder:", StringComparison.Ordinal);
            if (!folder && AppShelf.Find(id) is null)
            {
                continue;
            }

            if (!seen.Add(id))
            {
                continue;
            }

            kept.Add(id);
        }

        while (kept.Count > 0 && kept[^1].Length == 0)
        {
            kept.RemoveAt(kept.Count - 1);
        }

        return kept.ToArray();
    }

    private static string[] SanitizeStudio(string? packed, string[] catalog)
    {
        var kept = new List<string>(catalog.Length);
        var parts = (packed ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            var id = parts[index];
            if (!ContainsId(catalog, id) || ContainsId(kept, id))
            {
                continue;
            }

            kept.Add(id);
        }

        for (var index = 0; index < catalog.Length; index++)
        {
            if (!ContainsId(kept, catalog[index]))
            {
                kept.Add(catalog[index]);
            }
        }

        return kept.ToArray();
    }

    private void StripStudioApp(string id)
    {
        var at = IndexOn(studioApps, id);
        if (at < 0)
        {
            return;
        }

        var next = (string[])studioApps.Clone();
        for (var index = 0; index < next.Length; index++)
        {
            if (string.Equals(next[index], id, StringComparison.Ordinal))
            {
                next[index] = string.Empty;
            }
        }

        studioApps = HomeAppSlots(next);
    }

    private string[] SanitizeHomeApps(string? packed)
    {
        var kept = new List<string>(6);
        var parts = (packed ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
        for (var index = 0; index < parts.Length && kept.Count < 6; index++)
        {
            var id = parts[index];
            if (!CanPlaceHomeApp(id) || ContainsId(kept, id))
            {
                continue;
            }

            kept.Add(id);
        }

        return HomeAppSlots(kept);
    }

    private string[] HomeAppSlots(IReadOnlyList<string> ids)
    {
        var kept = new List<string>(6);
        for (var index = 0; index < ids.Count && kept.Count < 6; index++)
        {
            var id = ids[index];
            if (!CanPlaceHomeApp(id) || ContainsId(kept, id))
            {
                continue;
            }

            kept.Add(id);
        }

        while (kept.Count < 6)
        {
            var fill = UnusedOwnedHomeApp(kept, string.Empty);
            if (fill.Length == 0)
            {
                kept.Add(string.Empty);
                continue;
            }

            kept.Add(fill);
        }

        return kept.ToArray();
    }

    private string UnusedOwnedHomeApp(IReadOnlyList<string> used, string ignore)
    {
        if (CanPlaceHomeApp("party") &&
            !string.Equals(ignore, "party", StringComparison.Ordinal) && !ContainsId(used, "party"))
        {
            return "party";
        }

        var owned = OwnedApps;
        for (var index = 0; index < owned.Count; index++)
        {
            var id = owned[index];
            if (string.Equals(id, ignore, StringComparison.Ordinal) || ContainsId(used, id) ||
                !CanPlaceHomeApp(id))
            {
                continue;
            }

            return id;
        }

        return string.Empty;
    }

    private static bool CanBeHomeApp(string id) =>
        id.Length > 0 && (string.Equals(id, "party", StringComparison.Ordinal) || AppShelf.Find(id) is not null);
}
