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
    private string customBannerFile = string.Empty;
    private string colorway = ColorwayId.Crystal;
    private string core = CoreId.Gold;
    private ShadeLevel shade = ShadeLevel.Even;
    private ClockFace clockFace;
    private LetteringSize lettering = LetteringSize.Medium;
    private bool showWorld = true;
    private bool showMarks = true;
    private bool reduceMotion;
    private bool quiet;
    private bool quietWhenBusy;
    private bool wakeInPocket;
    private bool stayInPortraits = true;
    private bool tuckForCutscenes = true;
    private FightPresence fight = FightPresence.Stay;
    private TuneLayout layout;
    private string pictureDraft = string.Empty;
    private string bannerDraft = string.Empty;
    private string[] replies = DefaultReplies;

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

    public bool UsingCustomPlate => customPlateFile.Length > 0;

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
