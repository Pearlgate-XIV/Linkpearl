using Dalamud.Configuration;
using Linkpearl.Chassis;
using Linkpearl.Media;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Host.Composition;

public sealed class HandsetConfig : IPluginConfiguration
{
    public const int FreshBootMark = 3;

    public int Version { get; set; } = 1;

    public int FreshBoot { get; set; }

    public float ScaleStep { get; set; } = HandsetSizeCatalog.DefaultStep;

    public float PocketScale { get; set; } = 1f;

    public HandsetForm Form { get; set; } = HandsetForm.Phone;

    public HandsetFinish Finish { get; set; } = HandsetFinish.Crystal;

    public HandsetCase Case { get; set; } = HandsetCase.Pearl;

    public string SessionToken { get; set; } = string.Empty;

    public string IcecastListenBase { get; set; } = string.Empty;

    public string IcecastSourceUser { get; set; } = "source";

    public string IcecastSourcePassword { get; set; } = string.Empty;

    public string GiphyApiKey { get; set; } = string.Empty;

    public string StreamDeskMode { get; set; } = "pearlgate";

    public bool HandsetOpen { get; set; }

    public bool HandsetMinimized { get; set; }

    public bool HasOpenPos { get; set; }

    public float OpenX { get; set; }

    public float OpenY { get; set; }

    public bool HasPocketPos { get; set; }

    public float PocketX { get; set; }

    public float PocketY { get; set; }

    public bool PositionLocked { get; set; }

    public bool ShowLockTab { get; set; } = true;

    public bool Use24HourClock { get; set; }

    public string LanguageId { get; set; } = "en-US";

    public int Appearance { get; set; } = (int)AppearanceMode.Night;

    public string WallpaperId { get; set; } = WallpaperCatalog.DefaultId;

    public string CustomPlateFile { get; set; } = string.Empty;

    public string[] CustomPlateFiles { get; set; } = [];

    public string CustomBannerFile { get; set; } = string.Empty;

    public float BannerZoom { get; set; } = 1f;

    public float BannerFocusX { get; set; } = 0.5f;

    public float BannerFocusY { get; set; } = 0.5f;

    public string Colorway { get; set; } = ColorwayId.Night;

    public string Core { get; set; } = CoreId.White;

    public int Shade { get; set; } = (int)ShadeLevel.Even;

    public int ClockFace { get; set; }

    public int Lettering { get; set; } = (int)LetteringSize.Medium;

    public int NameStyle { get; set; }

    public bool TestingAccount { get; set; }

    public string OwnName { get; set; } = string.Empty;

    public string OwnTitle { get; set; } = string.Empty;

    public string OwnTimeZoneId { get; set; } = string.Empty;

    public int TitleMotion { get; set; }

    public bool TitleGlow { get; set; } = true;

    public int TitleGlowWeight { get; set; } = 1;

    public float TitleInkR { get; set; } = 1f;

    public float TitleInkG { get; set; } = 1f;

    public float TitleInkB { get; set; } = 1f;

    public float TitleGlowR { get; set; } = 0.92f;

    public float TitleGlowG { get; set; } = 0.78f;

    public float TitleGlowB { get; set; } = 0.42f;

    public int NameMotion { get; set; }

    public bool NameGlow { get; set; }

    public float NameGlowR { get; set; } = 0.92f;

    public float NameGlowG { get; set; } = 0.78f;

    public float NameGlowB { get; set; } = 0.42f;

    public int NameGlowWeight { get; set; } = 1;

    public bool NameInkCustom { get; set; }

    public float NameInkR { get; set; } = 1f;

    public float NameInkG { get; set; } = 1f;

    public float NameInkB { get; set; } = 1f;

    public string DisplayFace { get; set; } = FounderFaces.Inter;

    public bool FounderFacesGranted { get; set; }

    public bool ShowWorld { get; set; } = true;

    public bool ShowMarks { get; set; } = true;

    public bool FeedShowSay { get; set; } = true;

    public bool FeedShowShout { get; set; } = true;

    public bool FeedShowYell { get; set; } = true;

    public bool FeedShowParty { get; set; } = true;

    public int ExtraHomeScreens { get; set; }

    public bool ReduceMotion { get; set; }

    public string PhotosFolder { get; set; } = string.Empty;

    public bool Quiet { get; set; }

    public bool QuietWhenBusy { get; set; }

    public bool ChatE2E { get; set; } = true;

    public bool WakeInPocket { get; set; }

    public bool StayInPortraits { get; set; } = true;

    public bool TuckForCutscenes { get; set; } = true;

    public int Fight { get; set; }

    public int TuneLayout { get; set; }

    public float Brightness { get; set; } = 1f;

    public float Volume { get; set; } = 0.70f;

    public float MusicVolume { get; set; } = 0.70f;

    public float MicVolume { get; set; } = 0.80f;

    public string SpeakerDeviceId { get; set; } = string.Empty;

    public string MicrophoneDeviceId { get; set; } = string.Empty;

    public string CallSpeakerDeviceId { get; set; } = string.Empty;

    public string CallMicrophoneDeviceId { get; set; } = string.Empty;

    public bool AutoRotate { get; set; }

    public string[] Replies { get; set; } = [];

    public string[]? InstalledApps { get; set; }

    public string[]? AppScreens { get; set; }

    public string[]? OwnedApps { get; set; }

    public string[] FavoriteApps { get; set; } = [];

    public string[] AppFolders { get; set; } = [];

    public string[] QuickApps { get; set; } = [];

    public string StudioWidgets { get; set; } = string.Empty;

    public string StudioApps { get; set; } = string.Empty;

    public string[] RecentAppIds { get; set; } = [];

    public string[] RecentAppPlaces { get; set; } = [];

    public string[] SeenShelfApps { get; set; } = [];

    public string[] PopoutTalkIds { get; set; } = [];

    public string[] PopoutTalkPlaces { get; set; } = [];

    public void Sanitize()
    {
        ScaleStep = HandsetSizeCatalog.ClampFree(ScaleStep, HandsetSizeCatalog.FreeCeiling);
        PocketScale = HandsetShapePreference.SnapPocket(PocketScale);
        if (Form is not (HandsetForm.Phone or HandsetForm.Tablet))
        {
            Form = HandsetForm.Phone;
        }

        if (Finish is not (HandsetFinish.Crystal or HandsetFinish.Etched))
        {
            Finish = HandsetFinish.Crystal;
        }

        if (Case is not (HandsetCase.Pearl or HandsetCase.Android))
        {
            Case = HandsetCase.Pearl;
        }

        SessionToken = SessionToken?.Trim() ?? string.Empty;
        IcecastListenBase = IcecastListenBase?.Trim() ?? string.Empty;
        IcecastSourceUser = string.IsNullOrWhiteSpace(IcecastSourceUser) ? "source" : IcecastSourceUser.Trim();
        IcecastSourcePassword = IcecastSourcePassword ?? string.Empty;
        GiphyApiKey = GiphyApiKey?.Trim() ?? string.Empty;
        var streamMode = (StreamDeskMode ?? "pearlgate").Trim().ToLowerInvariant();
        StreamDeskMode = streamMode is "mock" or "pearlgate" ? streamMode : "pearlgate";
        WallpaperId = string.IsNullOrWhiteSpace(WallpaperId) ? WallpaperCatalog.DefaultId : WallpaperId.Trim();
        CustomPlateFile = Path.GetFileName(CustomPlateFile ?? string.Empty);
        var plates = CustomPlateFiles ?? [];
        if (plates.Length > PlateFiles.MaxSlots)
        {
            Array.Resize(ref plates, PlateFiles.MaxSlots);
        }

        for (var index = 0; index < plates.Length; index++)
        {
            plates[index] = Path.GetFileName(plates[index] ?? string.Empty);
        }

        CustomPlateFiles = plates;
        CustomBannerFile = Path.GetFileName(CustomBannerFile ?? string.Empty);
        BannerZoom = BannerZoom <= 0f ? 1f : Math.Clamp(BannerZoom, CoverFit.PlaceZoomMin, CoverFit.PlaceZoomMax);
        if (BannerFocusX == 0f && BannerFocusY == 0f)
        {
            BannerFocusX = 0.5f;
            BannerFocusY = 0.5f;
        }
        else
        {
            BannerFocusX = Math.Clamp(BannerFocusX, 0f, 1f);
            BannerFocusY = Math.Clamp(BannerFocusY, 0f, 1f);
        }
        Colorway = ColorwayId.Sanitize(Colorway);
        Core = CoreId.Sanitize(Core);
        Appearance = Math.Clamp(Appearance, 0, 2);
        Shade = Math.Clamp(Shade, 0, 2);
        LanguageId = PhoneLanguages.Sanitize(LanguageId);
        ClockFace = Math.Clamp(ClockFace, 0, 2);
        Lettering = Math.Clamp(Lettering, 0, 2);
        NameStyle = Math.Clamp(NameStyle, 0, 2);
        OwnName = ShownName.Sanitize(OwnName ?? string.Empty);
        OwnTitle = ShownName.ClampTitle(OwnTitle ?? string.Empty).Trim();
        OwnTimeZoneId = WorldZones.Sanitize(OwnTimeZoneId);
        TitleMotion = Math.Clamp(TitleMotion, 0, 2);
        TitleGlowWeight = Math.Clamp(TitleGlowWeight, 0, 2);
        TitleInkR = Math.Clamp(TitleInkR, 0f, 1f);
        TitleInkG = Math.Clamp(TitleInkG, 0f, 1f);
        TitleInkB = Math.Clamp(TitleInkB, 0f, 1f);
        TitleGlowR = Math.Clamp(TitleGlowR, 0f, 1f);
        TitleGlowG = Math.Clamp(TitleGlowG, 0f, 1f);
        TitleGlowB = Math.Clamp(TitleGlowB, 0f, 1f);
        NameMotion = Math.Clamp(NameMotion, 0, 2);
        NameGlowR = Math.Clamp(NameGlowR, 0f, 1f);
        NameGlowG = Math.Clamp(NameGlowG, 0f, 1f);
        NameGlowB = Math.Clamp(NameGlowB, 0f, 1f);
        NameGlowWeight = Math.Clamp(NameGlowWeight, 0, 2);
        NameInkR = Math.Clamp(NameInkR, 0f, 1f);
        NameInkG = Math.Clamp(NameInkG, 0f, 1f);
        NameInkB = Math.Clamp(NameInkB, 0f, 1f);
        DisplayFace = FounderFaces.Sanitize(DisplayFace ?? string.Empty);
        ExtraHomeScreens = Math.Clamp(ExtraHomeScreens, 0, 5);
        Fight = Math.Clamp(Fight, 0, 2);
        TuneLayout = Math.Clamp(TuneLayout, 0, 1);
        Brightness = Math.Clamp(Brightness, 0.20f, 1f);
        Volume = Math.Clamp(Volume, 0f, 1f);
        MusicVolume = Math.Clamp(MusicVolume, 0f, 1f);
        MicVolume = Math.Clamp(MicVolume, 0f, 1f);
        SpeakerDeviceId = SpeakerDeviceId?.Trim() ?? string.Empty;
        MicrophoneDeviceId = MicrophoneDeviceId?.Trim() ?? string.Empty;
        CallSpeakerDeviceId = CallSpeakerDeviceId?.Trim() ?? string.Empty;
        CallMicrophoneDeviceId = CallMicrophoneDeviceId?.Trim() ?? string.Empty;
        Replies ??= [];
        FavoriteApps ??= [];
        OwnedApps ??= [];
        AppFolders ??= [];
        QuickApps ??= [];
        RecentAppIds ??= [];
        RecentAppPlaces ??= [];
        SeenShelfApps ??= [];
        PopoutTalkIds ??= [];
        PopoutTalkPlaces ??= [];
    }
}
