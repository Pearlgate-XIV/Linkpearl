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

    void Compose(in AppletFrame frame);
}
