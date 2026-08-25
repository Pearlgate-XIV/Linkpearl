using Linkpearl.Applets;

namespace Linkpearl.Destinations;

public enum DestinationTab : byte
{
    Home = 0,
    Social = 1,
    Explore = 2,
    You = 3,
}

// The four fixed, permanent primary destinations. Unlike IApplet, there is no catalog, no
// install/uninstall, no home-screen icon: exactly four of these exist and one is always current.
// Deeper navigation within a destination (opening a venue, a conversation) is that destination's
// own concern to build later, not something this contract anticipates.
public interface IDestinationScreen
{
    DestinationTab Tab { get; }

    string Glyph { get; }

    string Label { get; }

    // Returns the total content height drawn, in the same (already display-scale-adjusted)
    // units as frame.Content itself — not necessarily equal to frame.Content.Height, since a
    // destination is free to draw more than fits and let the shell scroll to it. Every current
    // implementation gets this for free from how far a Stack's Remaining shrank, not from any
    // extra bookkeeping.
    float Compose(in AppletFrame frame);
}
