namespace Linkpearl.Chassis;

// A Linkpearl handset ships in two builds sharing one design language: Phone is the everyday
// 9:16 shape, Tablet widens for two-column layouts while keeping the same vertical rhythm.
public enum HandsetForm : byte
{
    Phone = 0,
    Tablet = 1,
}

public enum HandsetFinish : byte
{
    // Solid colorway: crystal glass over a metal frame, minimal ornament.
    Crystal = 0,

    // Etched art panel: wider bezel to frame artwork on the back plate.
    Etched = 1,
}

public enum HandsetCase : byte
{
    Pearl = 0,
    Android = 1,
}
