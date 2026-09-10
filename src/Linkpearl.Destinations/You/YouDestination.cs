using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Badges;
using Linkpearl.Destinations.Profile;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;

namespace Linkpearl.Destinations.You;

public sealed class YouDestination : IDestinationScreen
{
    private readonly IGameSession game;
    private readonly IPearlHub pearl;
    private readonly BadgeBook badges;
    private readonly DisplayPreferences display;
    private readonly bool development;
    private readonly ProfileChrome profile;

    public YouDestination(IGameSession game, IPearlHub pearl, BadgeBook badges, HostPaths paths,
        ITextureSource textures, IFilePicker files, DisplayPreferences display, bool development,
        HandsetProfileDesk profiles)
    {
        this.game = game;
        this.pearl = pearl;
        this.badges = badges;
        this.display = display;
        this.development = development;
        profile = new ProfileChrome(badges, paths, textures, files, pearl, game, display, development, profiles);
    }

    public DestinationTab Tab => DestinationTab.You;

    public string Glyph => "🧑";

    public string Label => PhoneLanguages.T("nav.you");

    public bool CanGoBack => profile.OverlayOpen;

    public bool Back() => profile.Back();

    public float Compose(in AppletFrame frame)
    {
        var snapshot = pearl.Current;
        badges.Sync(snapshot.SignedIn && snapshot.FounderSeat > 0 && snapshot.FounderSeat <= FounderFaces.SeatLimit,
            game.JobName, development, GlassName.IsPatron(badges, snapshot, display, development));
        if (profile.OverlayOpen)
        {
            return profile.DrawOverlay(frame);
        }

        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));

        var linked = ShownName.Linked(game.Character.Name, snapshot.MeName);
        var name = GlassName.ProfileName(display, linked,
            GlassName.IsPatron(badges, snapshot, display, development));
        if (name.Length == 0)
        {
            name = "Not logged in";
        }

        var job = game.JobName.Length > 0 ? TitleCase(game.JobName) : "Warrior of Light";
        var world = snapshot.MeWorld.Length > 0 ? snapshot.MeWorld : game.Character.WorldName;
        profile.DrawCard(frame, stack.Take(frame.Units(ProfileChrome.SheetHeightUnits)), name, job,
            world.Length > 0 ? world : "Eorzea", game.MapPlace, game.JobIconId);

        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private static string TitleCase(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        return char.ToUpper(value[0], CultureInfo.InvariantCulture) + value[1..];
    }
}
