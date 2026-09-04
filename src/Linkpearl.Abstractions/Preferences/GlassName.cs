using Linkpearl.Badges;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Preferences;

public static class GlassName
{
    public static readonly Vector4[] GlowSwatches =
    {
        new(0.92f, 0.78f, 0.42f, 1f),
        new(1.00f, 1.00f, 1.00f, 1f),
        new(0.95f, 0.42f, 0.58f, 1f),
        new(0.68f, 0.48f, 0.98f, 1f),
        new(0.32f, 0.82f, 0.92f, 1f),
        new(0.98f, 0.52f, 0.28f, 1f),
        new(0.42f, 0.88f, 0.55f, 1f),
        new(0.55f, 0.72f, 1.00f, 1f),
    };

    public static bool IsPatron(BadgeBook book, PearlSnapshot snapshot) =>
        IsPatron(book, snapshot, testing: false);

    public static bool IsPatron(BadgeBook book, PearlSnapshot snapshot, DisplayPreferences display, bool development) =>
        IsPatron(book, snapshot, development && display.TestingAccount);

    public static bool IsPatron(BadgeBook book, PearlSnapshot snapshot, bool testing) =>
        testing || snapshot.IsPatron;

    public static string Resolve(DisplayPreferences display, string linked, bool patron) =>
        ShownName.ForGlass(display, linked, patron);

    public static TextStyle Title(Palette palette, DisplayPreferences display, bool patron,
        FontRole role = FontRole.Display, TextAlign align = TextAlign.Left, float unit = 1f)
    {
        var ink = palette.Ink;
        return new TextStyle(role, ink, align);
    }

    public static float Spread(NameGlowWeight weight) => weight switch
    {
        NameGlowWeight.Soft => 1.35f,
        NameGlowWeight.Strong => 3.40f,
        _ => 2.20f,
    };
}
