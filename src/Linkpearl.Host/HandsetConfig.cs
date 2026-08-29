using Dalamud.Configuration;
using Linkpearl.Chassis;
using Linkpearl.Media;
using Linkpearl.Preferences;

namespace Linkpearl.Host.Composition;

public sealed class HandsetConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public float ScaleStep { get; set; } = HandsetSizeCatalog.DefaultStep;

    public float PocketScale { get; set; } = 1f;

    public HandsetForm Form { get; set; } = HandsetForm.Phone;

    public HandsetFinish Finish { get; set; } = HandsetFinish.Crystal;

    public string SessionToken { get; set; } = string.Empty;

    public bool HandsetOpen { get; set; }

    public bool HandsetMinimized { get; set; }

    public bool PositionLocked { get; set; }

    public bool ShowLockTab { get; set; } = true;

    public bool Use24HourClock { get; set; }

    public int Appearance { get; set; }

    public string WallpaperId { get; set; } = WallpaperCatalog.DefaultId;

    public string CustomPlateFile { get; set; } = string.Empty;

    public string CustomBannerFile { get; set; } = string.Empty;

    public string Colorway { get; set; } = ColorwayId.Crystal;

    public string Core { get; set; } = CoreId.Gold;

    public int Shade { get; set; } = (int)ShadeLevel.Even;

    public int ClockFace { get; set; }

    public int Lettering { get; set; } = (int)LetteringSize.Medium;

    public bool ShowWorld { get; set; } = true;

    public bool ShowMarks { get; set; } = true;

    public bool ReduceMotion { get; set; }

    public bool Quiet { get; set; }

    public bool QuietWhenBusy { get; set; }

    public bool WakeInPocket { get; set; }

    public bool StayInPortraits { get; set; } = true;

    public bool TuckForCutscenes { get; set; } = true;

    public int Fight { get; set; }

    public int TuneLayout { get; set; }

    public string[] Replies { get; set; } = [];

    public void Sanitize()
    {
        ScaleStep = HandsetSizeCatalog.SnapToStep(ScaleStep);
        PocketScale = HandsetShapePreference.SnapPocket(PocketScale);
        if (Form is not (HandsetForm.Phone or HandsetForm.Tablet))
        {
            Form = HandsetForm.Phone;
        }

        if (Finish is not (HandsetFinish.Crystal or HandsetFinish.Etched))
        {
            Finish = HandsetFinish.Crystal;
        }

        SessionToken = SessionToken?.Trim() ?? string.Empty;
        WallpaperId = string.IsNullOrWhiteSpace(WallpaperId) ? WallpaperCatalog.DefaultId : WallpaperId.Trim();
        CustomPlateFile = Path.GetFileName(CustomPlateFile ?? string.Empty);
        CustomBannerFile = Path.GetFileName(CustomBannerFile ?? string.Empty);
        Colorway = ColorwayId.Sanitize(Colorway);
        Core = CoreId.Sanitize(Core);
        Appearance = Math.Clamp(Appearance, 0, 2);
        Shade = Math.Clamp(Shade, 0, 2);
        ClockFace = Math.Clamp(ClockFace, 0, 2);
        Lettering = Math.Clamp(Lettering, 0, 2);
        Fight = Math.Clamp(Fight, 0, 2);
        TuneLayout = Math.Clamp(TuneLayout, 0, 1);
        Replies ??= [];
    }
}
