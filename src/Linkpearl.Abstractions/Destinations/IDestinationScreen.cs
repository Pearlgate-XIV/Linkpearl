using Linkpearl.Applets;

namespace Linkpearl.Destinations;

public enum DestinationTab : byte
{
    Home = 0,
    Social = 1,
    Explore = 2,
    You = 3,
    Settings = 4,
}

public static class SocialPane
{
    public const int Feed = 0;
    public const int Messages = 1;
    public const int People = 2;
    public const int Communities = 3;
    public const int Linkshells = 4;
    public const int Phone = 5;
}

public static class ExplorePane
{
    public const int ForYou = 0;
    public const int Places = 1;
    public const int Activities = 2;
    public const int Events = 3;
    public const int Groups = 4;
}

public static class HomePane
{
    public const int Dashboard = 0;
    public const int Announcements = 1;
    public const int Profile = 2;
}

public static class SettingsPane
{
    public const int Front = 0;
    public const int Presence = 1;
    public const int Notices = 2;
}

public sealed class DestinationHub
{
    private DestinationTab pendingTab;
    private int pendingSection;
    private string pendingTalk = string.Empty;
    private string pendingProfile = string.Empty;
    private string pendingNotice = string.Empty;
    private string pendingLabel = string.Empty;
    private string pendingApplet = string.Empty;
    private string pendingAppletHint = string.Empty;
    private bool pending;
    private bool appletPending;
    private bool searchPending;
    private bool menuPending;

    public void Open(DestinationTab tab, int section = 0, string label = "")
    {
        pendingTab = tab;
        pendingSection = section;
        pendingTalk = string.Empty;
        pendingProfile = string.Empty;
        pendingNotice = string.Empty;
        pendingLabel = label ?? string.Empty;
        pending = true;
    }

    public void OpenAnnouncement(string announcementId)
    {
        pendingTab = DestinationTab.Home;
        pendingSection = HomePane.Announcements;
        pendingTalk = string.Empty;
        pendingProfile = string.Empty;
        pendingNotice = announcementId ?? string.Empty;
        pendingLabel = string.Empty;
        pending = true;
    }

    public void OpenTalk(string threadId)
    {
        pendingTab = DestinationTab.Social;
        pendingSection = SocialPane.Messages;
        pendingTalk = threadId;
        pendingProfile = string.Empty;
        pendingNotice = string.Empty;
        pendingLabel = string.Empty;
        pending = true;
    }

    public void OpenProfile(string peerId)
    {
        pendingTab = DestinationTab.Social;
        pendingSection = SocialPane.People;
        pendingTalk = string.Empty;
        pendingProfile = peerId;
        pendingNotice = string.Empty;
        pendingLabel = string.Empty;
        pending = true;
    }

    public void OpenApplet(string appletId, string routeHint = "")
    {
        pendingApplet = appletId ?? string.Empty;
        pendingAppletHint = routeHint ?? string.Empty;
        appletPending = pendingApplet.Length > 0;
    }

    public void OpenSearch() => searchPending = true;

    public void OpenMenu() => menuPending = true;

    public bool TryTake(out DestinationTab tab, out int section, out string talkId, out string profileId,
        out string noticeId)
    {
        tab = pendingTab;
        section = pendingSection;
        talkId = pendingTalk;
        profileId = pendingProfile;
        noticeId = pendingNotice;
        if (!pending)
        {
            return false;
        }

        pending = false;
        pendingTalk = string.Empty;
        pendingProfile = string.Empty;
        pendingNotice = string.Empty;
        return true;
    }

    public string TakeLabel()
    {
        var label = pendingLabel;
        pendingLabel = string.Empty;
        return label;
    }

    public bool TryTakeApplet(out string appletId, out string routeHint)
    {
        appletId = pendingApplet;
        routeHint = pendingAppletHint;
        if (!appletPending)
        {
            return false;
        }

        appletPending = false;
        pendingApplet = string.Empty;
        pendingAppletHint = string.Empty;
        return appletId.Length > 0;
    }

    public bool TryTakeSearch()
    {
        if (!searchPending)
        {
            return false;
        }

        searchPending = false;
        return true;
    }

    public bool TryTakeMenu()
    {
        if (!menuPending)
        {
            return false;
        }

        menuPending = false;
        return true;
    }
}

public interface ISectionedDestination
{
    int CurrentSection { get; }

    void ShowSection(int section);
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

    // Soft-key Back. Return true when an inner page was closed so the shell stays on this tab.
    bool CanGoBack => false;

    bool Back() => false;
}
