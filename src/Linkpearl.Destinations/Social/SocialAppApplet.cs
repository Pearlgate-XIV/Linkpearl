using Linkpearl.Applets;
using Linkpearl.Talk;

namespace Linkpearl.Destinations.Social;

public sealed class SocialAppApplet : IApplet
{
    private readonly SocialDestination social;
    private readonly int homePane;
    private readonly ITalk talk;
    private readonly bool countsUnread;

    public SocialAppApplet(SocialDestination social, ITalk talk, string id, string name, string glyph, int homeOrder,
        int homePane, bool countsUnread)
    {
        this.social = social;
        this.talk = talk;
        this.homePane = homePane;
        this.countsUnread = countsUnread;
        Manifest = new AppletManifest
        {
            Id = id,
            DisplayNameKey = name,
            Family = AppletFamily.Social,
            Glyph = glyph,
            HomeOrder = homeOrder,
            Capabilities = AppletCapabilities.RequiresAccount | AppletCapabilities.ReadsGameChat |
                           AppletCapabilities.WritesGameChat,
        };
    }

    public AppletManifest Manifest { get; }

    public AppletBadge Badge =>
        countsUnread && talk.UnreadTotal > 0 ? new AppletBadge(talk.UnreadTotal) : AppletBadge.None;

    public string Place => homePane == SocialPane.Messages
        ? "direct"
        : social.CurrentSection switch
        {
            SocialPane.Feed => "feed",
            SocialPane.People => "friends",
            SocialPane.Linkshells => "messages",
            SocialPane.Communities => "communities",
            _ => "direct",
        };

    public bool CanGoBack => social.CanGoBack;

    public void Enter(AppletEntry entry)
    {
        social.ShowSection(homePane == SocialPane.Messages ? SocialPane.Messages : PaneOf(entry.RouteHint));
    }

    public void Leave()
    {
    }

    public bool Back() => social.Back();

    public void Compose(in AppletFrame frame)
    {
        social.Compose(frame);
    }

    private int PaneOf(string? hint)
    {
        if (string.Equals(hint, "feed", StringComparison.OrdinalIgnoreCase))
        {
            return SocialPane.Feed;
        }

        if (string.Equals(hint, "friends", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(hint, "people", StringComparison.OrdinalIgnoreCase))
        {
            return SocialPane.People;
        }

        if (string.Equals(hint, "messages", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(hint, "linkshells", StringComparison.OrdinalIgnoreCase))
        {
            return SocialPane.Linkshells;
        }

        if (string.Equals(hint, "direct", StringComparison.OrdinalIgnoreCase))
        {
            return SocialPane.Messages;
        }

        return homePane;
    }
}
