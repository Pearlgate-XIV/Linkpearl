using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Talk;
using Linkpearl.Time;

namespace Linkpearl.Destinations.Social;

public sealed class SocialDestination : IDestinationScreen, ISectionedDestination
{
    private static readonly (string Label, int Pane)[] Sections =
    {
        ("Direct", SocialPane.Messages),
        ("Messages", SocialPane.Linkshells),
        ("People", SocialPane.People),
        ("Feed", SocialPane.Feed),
        ("Phone", SocialPane.Phone),
    };

    private readonly IPearlHub pearl;
    private readonly ITalk talk;
    private readonly MessagesSurface messages;
    private readonly PhonePad phone = new();
    private int selectedSection = SocialPane.Messages;

    public SocialDestination(IPearlHub pearl, IClock clock, ITalk talk, IGameSession game, DisplayPreferences display)
    {
        this.pearl = pearl;
        this.talk = talk;
        messages = new MessagesSurface(talk, clock, game, display);
    }

    public DestinationTab Tab => DestinationTab.Social;

    public string Glyph => "👥";

    public string Label => "Social";

    public int CurrentSection => selectedSection;

    public void ShowSection(int section)
    {
        selectedSection = section is SocialPane.Feed or SocialPane.Messages or SocialPane.People
            or SocialPane.Communities or SocialPane.Linkshells or SocialPane.Phone
            ? section
            : SocialPane.Messages;
        messages.ShowInbox(selectedSection);
        messages.Close();
    }

    public void OpenThread(string threadId)
    {
        selectedSection = RoomThread(talk.Find(threadId)) ? SocialPane.Linkshells : SocialPane.Messages;
        messages.ShowInbox(selectedSection);
        messages.Open(threadId);
    }

    public void OpenProfile(string peerId) => messages.OpenProfile(peerId);

    public float Compose(in AppletFrame frame)
    {
        if (messages.ProfileOpen)
        {
            return messages.Compose(frame);
        }

        if (selectedSection is SocialPane.Messages or SocialPane.Linkshells)
        {
            return ComposeMessages(frame);
        }

        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        var snapshot = pearl.Current;

        DrawHeading(frame, stack.Take(frame.Units(30f)));
        DrawSectionTabs(frame, stack.Take(frame.Units(32f)));

        if (!snapshot.SignedIn && selectedSection == SocialPane.Feed)
        {
            DrawEmpty(frame, stack.TakeRemaining(), "Sign in from You to load Feed.");
            return content.Height + inset * 2f;
        }

        if (selectedSection == SocialPane.Phone)
        {
            return DrawPhone(frame, stack, content, inset);
        }

        if (selectedSection == SocialPane.Communities)
        {
            DrawEmpty(frame, stack.TakeRemaining(),
                "Communities are not on Pearlgate yet (muster and yellow pages are off).");
            return content.Height + inset * 2f;
        }

        if (selectedSection == SocialPane.Feed)
        {
            DrawStories(frame, stack, snapshot);
        }
        else
        {
            DrawPeople(frame, stack, snapshot);
        }

        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private float ComposeMessages(in AppletFrame frame)
    {
        if (messages.ThreadOpen)
        {
            return messages.Compose(frame);
        }

        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        DrawHeading(frame, stack.Take(frame.Units(30f)));
        DrawSectionTabs(frame, stack.Take(frame.Units(32f)));
        var restArea = stack.TakeRemaining();
        var used = messages.Compose(frame.WithContent(restArea));
        return (content.Height - restArea.Height) + used + inset * 2f;
    }

    private void DrawHeading(in AppletFrame frame, Rect row)
    {
        frame.Text.DrawIn(row, SectionTitle(), new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
    }

    private string SectionTitle()
    {
        for (var index = 0; index < Sections.Length; index++)
        {
            if (Sections[index].Pane == selectedSection)
            {
                return Sections[index].Label;
            }
        }

        return "Social";
    }

    private void DrawSectionTabs(in AppletFrame frame, Rect row)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var radius = row.Height * 0.5f;
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceOverlay, radius);
        frame.Paint.Stroke(row, gold with { W = 0.32f }, frame.Units(1f), radius);
        var cellWidth = row.Width / Sections.Length;
        for (var index = 0; index < Sections.Length; index++)
        {
            var cell = row.Translate(new Vector2(index * cellWidth, 0f)).WithWidth(cellWidth);
            var isActive = Sections[index].Pane == selectedSection;
            if (isActive)
            {
                frame.Paint.Fill(cell.Inset(frame.Units(2f)), gold with { W = 0.22f }, radius);
            }

            frame.Text.DrawIn(cell, Sections[index].Label,
                new TextStyle(FontRole.CaptionStrong, isActive ? gold : frame.Theme.Palette.InkMuted, TextAlign.Center));
            if (frame.Input.ConsumeClick(cell))
            {
                ShowSection(Sections[index].Pane);
            }
        }
    }

    private static bool RoomThread(TalkThread? thread) =>
        thread is { Kind: TalkKind.Party or TalkKind.Alliance or TalkKind.Linkshell or TalkKind.CrossWorldLinkshell
            or TalkKind.FreeCompany or TalkKind.Novice };

    private float DrawPhone(in AppletFrame frame, Stack stack, Rect content, float inset)
    {
        var rest = stack.TakeRemaining();
        phone.Compose(frame.WithContent(rest), talk, messages);
        return content.Height + inset * 2f;
    }

    private static void DrawStories(in AppletFrame frame, Stack stack, PearlSnapshot snapshot)
    {
        if (snapshot.Stories.Length == 0)
        {
            DrawEmpty(frame, stack.Take(frame.Units(72f)), "No stories yet. Chirper is off on this server.");
            return;
        }

        for (var index = 0; index < snapshot.Stories.Length; index++)
        {
            var row = stack.Take(frame.Units(72f));
            CardChrome.DrawGold(frame, row);
            DrawStory(frame, row.Inset(frame.Units(12f)), snapshot.Stories[index]);
        }
    }

    private static void DrawStory(in AppletFrame frame, Rect inset, PearlStory story)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(3f));
        frame.Text.DrawIn(stack.Take(frame.Units(20f)), story.AuthorName,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var detail = story.HasUnseen ? "New story" : "Story";
        frame.Text.DrawIn(stack.Take(frame.Units(18f)),
            detail + " · " + story.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private void DrawPeople(in AppletFrame frame, Stack stack, PearlSnapshot snapshot)
    {
        var peers = talk.Peers();
        var hints = talk.SuggestTells();
        if (peers.Count > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Talked to",
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
            for (var index = 0; index < peers.Count; index++)
            {
                var peer = peers[index];
                var row = stack.Take(frame.Units(56f));
                CardChrome.DrawGold(frame, row);
                DrawPeer(frame, row.Inset(frame.Units(12f)), peer);
                if (frame.Input.ConsumeClick(row))
                {
                    messages.OpenProfile(peer.Id);
                }
            }
        }

        if (hints.Count > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Nearby to tell",
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
            for (var index = 0; index < hints.Count; index++)
            {
                var hint = hints[index];
                if (AlreadyPeer(peers, hint.Name))
                {
                    continue;
                }

                var row = stack.Take(frame.Units(56f));
                CardChrome.DrawGold(frame, row);
                DrawHint(frame, row.Inset(frame.Units(12f)), hint);
                if (frame.Input.ConsumeClick(row))
                {
                    messages.OpenProfile(TalkIds.Tell(hint.Name, hint.World));
                }
            }
        }

        if (!snapshot.SignedIn)
        {
            if (peers.Count == 0 && hints.Count == 0)
            {
                DrawEmpty(frame, stack.Take(frame.Units(72f)), "People you tell are saved here, even off Pearlgate.");
            }

            return;
        }

        var shownGate = 0;
        for (var index = 0; index < snapshot.People.Length; index++)
        {
            if (AlreadyPeer(peers, snapshot.People[index].DisplayName))
            {
                continue;
            }

            if (shownGate == 0)
            {
                frame.Text.DrawIn(stack.Take(frame.Units(18f)), "On Pearlgate",
                    new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
            }

            var row = stack.Take(frame.Units(72f));
            CardChrome.DrawGold(frame, row);
            DrawPerson(frame, row.Inset(frame.Units(12f)), snapshot.People[index]);
            if (frame.Input.ConsumeClick(row))
            {
                messages.OpenProfile(TalkIds.Person(snapshot.People[index].Id));
            }

            shownGate++;
        }

        if (peers.Count == 0 && hints.Count == 0 && shownGate == 0)
        {
            DrawEmpty(frame, stack.Take(frame.Units(72f)), "No people saved yet. Tells you send are kept on this character.");
        }
    }

    private static void DrawPeer(in AppletFrame frame, Rect inset, TalkPeer peer)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawIn(stack.Take(frame.Units(20f)), peer.Name,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var gate = peer.OnPearlgate ? "Pearlgate" : "Tell";
        var detail = peer.World.Length > 0 ? gate + " · " + peer.World : gate;
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static bool AlreadyPeer(IReadOnlyList<TalkPeer> peers, string name)
    {
        for (var index = 0; index < peers.Count; index++)
        {
            if (peers[index].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawHint(in AppletFrame frame, Rect inset, GamePeerHint hint)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawIn(stack.Take(frame.Units(20f)), hint.Name,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var detail = hint.World.Length > 0 ? hint.Reason + " · " + hint.World : hint.Reason;
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawPerson(in AppletFrame frame, Rect inset, PearlPerson person)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(3f));
        frame.Text.DrawIn(stack.Take(frame.Units(20f)), person.DisplayName,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var handle = person.Handle.Length > 0 ? "@" + person.Handle : person.PhoneNumber;
        var detail = handle.Length > 0 ? handle + " · Tell" : "Tell";
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawEmpty(in AppletFrame frame, Rect area, string text)
    {
        frame.Text.DrawWrapped(area.TopSlice(frame.Units(72f)), text,
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Center));
    }
}