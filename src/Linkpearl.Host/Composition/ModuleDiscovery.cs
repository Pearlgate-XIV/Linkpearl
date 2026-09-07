using Linkpearl.Applets.Core;
using Linkpearl.Applets.Life;
using Linkpearl.Modules;

namespace Linkpearl.Host.Composition;

// Curated for the foundation phase: one entry per applet family as each family lands. A
// reflection-based catalog (scan loaded assemblies for ILinkpearlModule) is follow-up work once
// there are enough modules that a hand-written list becomes the wrong kind of friction.
public static class ModuleDiscovery
{
    public static IReadOnlyList<ILinkpearlModule> Discover() => new ILinkpearlModule[]
    {
        new SettingsModule(),
        new FeedbackModule(),
        new ClockModule(),
        new AlarmsModule(),
        new PhoneModule(),
        new NotesModule(),
        new CalendarModule(),
        new MusicModule(),
        new VybeModule(),
        new CalculatorModule(),
        new TimerModule(),
        new StopwatchModule(),
        new WeatherModule(),
        new PlaceModule(),
        new EorzeaModule(),
        new CameraModule(),
        new WalletModule(),
        new MarketModule(),
        new AppStoreModule(),
    };
}
