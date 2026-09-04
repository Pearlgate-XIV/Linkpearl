using Linkpearl.Applets;
using Linkpearl.Media;

namespace Linkpearl.Preferences;

// Live look of the glass and how the communicator behaves in the world. HandsetHost loads
// persisted values into here at boot and writes them back when anything changes.
public sealed class DisplayPreferences
{
    private bool use24HourClock;
    private AppearanceMode appearance = AppearanceMode.FollowClock;
    private string wallpaperId = WallpaperCatalog.DefaultId;
    private string customPlateFile = string.Empty;
    private string[] customPlateFiles = [];
    private string customBannerFile = string.Empty;
    private string colorway = ColorwayId.Crystal;
    private string core = CoreId.Gold;
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
    private bool autoRotate;
    private bool landscape;
    private string pictureDraft = string.Empty;
    private string bannerDraft = string.Empty;
    private string[] replies = DefaultReplies;
    private string[]? installedApps;
    private string[] favoriteApps = [];
    private string[] appFolders = [];
    private string[] seenShelfApps = [];
    private string[] quickApps = ["", "", "", ""];
    private bool feedShowSay = true;
    private bool feedShowShout = true;
    private bool feedShowYell = true;
    private bool feedShowParty = true;

    private static readonly string[] DefaultReplies = { "On my way.", "One moment.", "Need a raise." };

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
        set => Set(ref extraHomeScreens, Math.Clamp(value, 0, 5));
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

    public IReadOnlyList<string> FavoriteApps => favoriteApps;

    public IReadOnlyList<string> AppFolders => appFolders;

    public const int QuickAppSlots = 4;

    public IReadOnlyList<string> QuickApps => quickApps;

    public void LoadAppShelf(string[]? installed, string[]? favorites, string[]? folders, string[]? quick,
        string[]? seen)
    {
        installedApps = installed is null ? null : SanitizeIds(installed, allowFolder: true);
        favoriteApps = SanitizeIds(favorites ?? [], allowFolder: true);
        appFolders = folders ?? [];
        seenShelfApps = SanitizeIds(seen ?? [], allowFolder: false);
        GraftNewDefaultApps();
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

            next ??= new List<string>(InstalledApps);
            next.Add(spec.Id);
            added = true;
        }

        if (added && next is not null)
        {
            installedApps = next.ToArray();
        }

        seenShelfApps = new string[seen.Count];
        seen.CopyTo(seenShelfApps);
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

    public void InstallApp(string id)
    {
        if (id.Length == 0 || IsOnShelf(id))
        {
            return;
        }

        var current = InstalledApps;
        var next = new string[current.Count + 1];
        for (var index = 0; index < current.Count; index++)
        {
            next[index] = current[index];
        }

        next[^1] = id;
        installedApps = next;
        Changed?.Invoke();
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

        var current = InstalledApps;
        var next = new List<string>(current.Count + unpacked.Length);
        for (var index = 0; index < current.Count; index++)
        {
            if (string.Equals(current[index], id, StringComparison.Ordinal))
            {
                for (var child = 0; child < unpacked.Length; child++)
                {
                    if (!next.Contains(unpacked[child]))
                    {
                        next.Add(unpacked[child]);
                    }
                }

                continue;
            }

            next.Add(current[index]);
        }

        installedApps = next.ToArray();
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
        var current = InstalledApps;
        var from = -1;
        for (var index = 0; index < current.Count; index++)
        {
            if (string.Equals(current[index], id, StringComparison.Ordinal))
            {
                from = index;
                break;
            }
        }

        if (from < 0)
        {
            return;
        }

        var to = Math.Clamp(from + delta, 0, current.Count - 1);
        if (to == from)
        {
            return;
        }

        var next = new string[current.Count];
        for (var index = 0; index < current.Count; index++)
        {
            next[index] = current[index];
        }

        (next[from], next[to]) = (next[to], next[from]);
        installedApps = next;
        Changed?.Invoke();
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
        if (childIds.Count == 0)
        {
            return;
        }

        var children = SanitizeIds(childIds, allowFolder: false);
        if (children.Length == 0)
        {
            return;
        }

        var id = "folder:" + Guid.NewGuid().ToString("N")[..8];
        var name = "Folder " + (appFolders.Length + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var packed = id + "\u001f" + name + "\u001f" + string.Join(',', children);
        var folders = new string[appFolders.Length + 1];
        appFolders.CopyTo(folders, 0);
        folders[^1] = packed;
        appFolders = folders;

        var shelf = new List<string>(InstalledApps.Count);
        for (var index = 0; index < InstalledApps.Count; index++)
        {
            var item = InstalledApps[index];
            var skip = false;
            for (var child = 0; child < children.Length; child++)
            {
                if (string.Equals(item, children[child], StringComparison.Ordinal))
                {
                    skip = true;
                    break;
                }
            }

            if (!skip)
            {
                shelf.Add(item);
            }
        }

        shelf.Add(id);
        installedApps = shelf.ToArray();
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
}
