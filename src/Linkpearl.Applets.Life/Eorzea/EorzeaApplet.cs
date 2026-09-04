using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Destinations;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Talk;

namespace Linkpearl.Applets.Life.Eorzea;

public sealed class EorzeaApplet : IApplet
{
    private static readonly string[] Sections = { "Character", "Social", "Duty" };

    public static readonly AppletManifest Manifest = new()
    {
        Id = "eorzea",
        DisplayNameKey = "Eorzea",
        Family = AppletFamily.Life,
        Glyph = "◎",
        HomeOrder = 26,
        Capabilities = AppletCapabilities.WritesGameChat,
    };

    private readonly IGameSession game;
    private readonly IChatBridge chat;
    private readonly ITalk talk;
    private readonly IPearlHub pearl;
    private readonly DestinationHub hub;
    private int section;
    private float scroll;
    private bool purseOpen;

    public EorzeaApplet(IGameSession game, IChatBridge chat, ITalk talk, IPearlHub pearl, DestinationHub hub)
    {
        this.game = game;
        this.chat = chat;
        this.talk = talk;
        this.pearl = pearl;
        this.hub = hub;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public string Place => Sections[section].ToLowerInvariant();

    public void Enter(AppletEntry entry)
    {
        if (string.Equals(entry.RouteHint, "social", StringComparison.OrdinalIgnoreCase))
        {
            section = 1;
        }
        else if (string.Equals(entry.RouteHint, "duty", StringComparison.OrdinalIgnoreCase))
        {
            section = 2;
        }
        else
        {
            section = 0;
        }

        scroll = 0f;
        purseOpen = false;
    }

    public void Leave()
    {
        purseOpen = false;
    }

    public bool CanGoBack => purseOpen || section != 0;

    public bool Back()
    {
        if (purseOpen)
        {
            purseOpen = false;
            return true;
        }

        if (section == 0)
        {
            return false;
        }

        section = 0;
        scroll = 0f;
        return true;
    }

    public void Compose(in AppletFrame frame)
    {
        if (purseOpen)
        {
            DrawPurse(frame);
            return;
        }

        var pad = frame.Units(12f);
        var tabH = frame.Units(32f);
        var gap = frame.Units(8f);
        var page = frame.Content.Inset(new Edges(pad, frame.Units(6f), pad, pad));
        DrawTabs(frame, page.TopSlice(tabH));

        var body = page.Inset(new Edges(0f, tabH + gap, 0f, 0f));
        frame.Paint.PushClip(body);
        var shifted = body.Translate(new Vector2(0f, -scroll));
        var stack = new Stack(shifted, StackAxis.Vertical, frame.Units(6f));
        if (section == 1)
        {
            DrawSocial(frame, ref stack, body);
        }
        else if (section == 2)
        {
            DrawDuty(frame, ref stack, body);
        }
        else
        {
            DrawCharacter(frame, ref stack, body);
        }

        var height = MathF.Max(0f, shifted.Height - stack.Remaining.Height);
        frame.Paint.PopClip();
        if (frame.Input.IsHovering(body) && frame.Input.ScrollDelta != 0f)
        {
            scroll = Math.Clamp(scroll - frame.Input.ScrollDelta * frame.Units(22f), 0f,
                MathF.Max(0f, height - body.Height));
        }
    }

    private void DrawTabs(in AppletFrame frame, Rect row)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var radius = row.Height * 0.5f;
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceOverlay, radius);
        frame.Paint.Stroke(row, gold with { W = 0.32f }, frame.Units(1f), radius);
        var cellWidth = row.Width / Sections.Length;
        for (var index = 0; index < Sections.Length; index++)
        {
            var cell = row.Translate(new Vector2(index * cellWidth, 0f)).WithWidth(cellWidth);
            var active = index == section;
            if (active)
            {
                frame.Paint.Fill(cell.Inset(frame.Units(3f)), gold with { W = 0.22f }, radius);
            }

            frame.Text.DrawIn(cell, Sections[index],
                new TextStyle(FontRole.CaptionStrong, active ? gold : frame.Theme.Palette.InkMuted, TextAlign.Center));
            if (frame.Input.ConsumeClick(cell) && section != index)
            {
                section = index;
                scroll = 0f;
            }
        }
    }

    private void DrawCharacter(in AppletFrame frame, ref Stack stack, Rect clip)
    {
        var job = game.JobName.Length > 0 ? game.JobName : "Not logged in";
        var zone = game.ZoneName.Length > 0 ? game.ZoneName : string.Empty;
        Menu(frame, ref stack, clip, "Character", job, zone.Length > 0 ? zone : "Sheet", GameMenu.Character);
        Menu(frame, ref stack, clip, "Actions & Traits", string.Empty, string.Empty, GameMenu.Actions);
        Menu(frame, ref stack, clip, "Adventurer Plate", string.Empty, string.Empty, GameMenu.AdventurerPlate);
        Menu(frame, ref stack, clip, "Portraits", string.Empty, string.Empty, GameMenu.Portraits);
        OpenPurse(frame, ref stack, clip);
        JumpPhone(frame, ref stack, clip, "Pearls", pearl.Current.SignedIn ? "On Pearlgate" : "Phone purse", "wallet");
        Menu(frame, ref stack, clip, "Armoury Chest", string.Empty, string.Empty, GameMenu.ArmouryChest);
        Menu(frame, ref stack, clip, "Inventory", string.Empty, string.Empty, GameMenu.Inventory);
        Menu(frame, ref stack, clip, "Chocobo Saddlebag", string.Empty, string.Empty, GameMenu.Saddlebag);
        Menu(frame, ref stack, clip, "Companion", string.Empty, string.Empty, GameMenu.Companion);
        Menu(frame, ref stack, clip, "Mount Guide", string.Empty, string.Empty, GameMenu.MountGuide);
        Menu(frame, ref stack, clip, "Minion Guide", string.Empty, string.Empty, GameMenu.MinionGuide);
        Menu(frame, ref stack, clip, "Facewear", string.Empty, string.Empty, GameMenu.Facewear);
        Menu(frame, ref stack, clip, "Fashion Accessories", string.Empty, string.Empty, GameMenu.Fashion);
        Menu(frame, ref stack, clip, "Blue Magic Spellbook", string.Empty, string.Empty, GameMenu.BlueSpellbook);
        Menu(frame, ref stack, clip, "PvP Profile", string.Empty, string.Empty, GameMenu.PvpProfile);
        Menu(frame, ref stack, clip, "Gold Saucer", string.Empty, string.Empty, GameMenu.GoldSaucer);
        Menu(frame, ref stack, clip, "Achievements", string.Empty, string.Empty, GameMenu.Achievements);
        JumpHub(frame, ref stack, clip, "Retainers", RetainerLine(), DestinationTab.You, 0);
    }

    private void DrawSocial(in AppletFrame frame, ref Stack stack, Rect clip)
    {
        JumpHub(frame, ref stack, clip, "Friends", FriendLine(), DestinationTab.Social, SocialPane.People);
        Menu(frame, ref stack, clip, "Friend List", string.Empty, "In-game list", GameMenu.FriendList);
        JumpHub(frame, ref stack, clip, "Messages", string.Empty, DestinationTab.Social, SocialPane.Messages);
        JumpHub(frame, ref stack, clip, "Party", PartyLine(), DestinationTab.Social, SocialPane.Linkshells);
        Menu(frame, ref stack, clip, "Party Finder", string.Empty, string.Empty, GameMenu.PartyFinder);
        Menu(frame, ref stack, clip, "Blacklist", string.Empty, string.Empty, GameMenu.Blacklist);
        Menu(frame, ref stack, clip, "Linkshell", string.Empty, string.Empty, GameMenu.Linkshell);
        Menu(frame, ref stack, clip, "Cross-world Linkshell", string.Empty, string.Empty, GameMenu.CrossWorldLinkshell);
        Menu(frame, ref stack, clip, "Free Company", string.Empty, string.Empty, GameMenu.FreeCompany);
        Menu(frame, ref stack, clip, "Emotes", string.Empty, string.Empty, GameMenu.Emotes);
        Menu(frame, ref stack, clip, "Player Search", string.Empty, string.Empty, GameMenu.PlayerSearch);
    }

    private void DrawDuty(in AppletFrame frame, ref Stack stack, Rect clip)
    {
        var duty = game.IsInDuty ? "In a duty" : "Not in a duty";
        Menu(frame, ref stack, clip, "Duty Finder", duty, PartyLine(), GameMenu.DutyFinder);
        Menu(frame, ref stack, clip, "Raid Finder", string.Empty, string.Empty, GameMenu.RaidFinder);
        Menu(frame, ref stack, clip, "Journal", string.Empty, "Quests and logs", GameMenu.Journal);
        Menu(frame, ref stack, clip, "Challenge Log", string.Empty, string.Empty, GameMenu.ChallengeLog);
        Menu(frame, ref stack, clip, "Novice Network", string.Empty, string.Empty, GameMenu.NoviceNetwork);
    }

    private void OpenPurse(in AppletFrame frame, ref Stack stack, Rect clip)
    {
        var row = TakeRow(frame, ref stack, "Currency", GilLine());
        if (Hit(frame, clip, row))
        {
            purseOpen = true;
            scroll = 0f;
        }
    }

    private void DrawPurse(in AppletFrame frame)
    {
        var pad = frame.Units(12f);
        var page = frame.Content.Inset(new Edges(pad, frame.Units(6f), pad, pad));
        var back = page.TopSlice(frame.Units(28f));
        frame.Text.DrawIn(back.LeftSlice(frame.Units(24f)), "‹",
            new TextStyle(FontRole.Title, frame.Theme.Palette.WarmAccent, TextAlign.Center));
        frame.Text.DrawIn(back.Inset(new Edges(frame.Units(24f), 0f, 0f, 0f)), "Currency",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        if (frame.Input.ConsumeClick(back))
        {
            purseOpen = false;
            scroll = 0f;
            return;
        }

        var body = page.Inset(new Edges(0f, frame.Units(36f), 0f, 0f));
        frame.Paint.PushClip(body);
        var shifted = body.Translate(new Vector2(0f, -scroll));
        var stack = new Stack(shifted, StackAxis.Vertical, frame.Units(4f));
        var rows = game.Currencies;
        var group = "\u0000";
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (!string.Equals(group, row.Group, StringComparison.Ordinal))
            {
                group = row.Group;
                if (group.Length > 0)
                {
                    DrawGroup(frame, stack.Take(frame.Units(22f)), group);
                }
            }

            DrawPurseRow(frame, stack.Take(frame.Units(40f)), row);
        }

        var native = stack.Take(frame.Units(36f));
        frame.Text.DrawIn(native, "Open game window",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
        if (frame.Input.ConsumeClick(native))
        {
            chat.OpenGameMenu(GameMenu.Currency);
        }

        var height = MathF.Max(0f, shifted.Height - stack.Remaining.Height);
        frame.Paint.PopClip();
        if (frame.Input.IsHovering(body) && frame.Input.ScrollDelta != 0f)
        {
            scroll = Math.Clamp(scroll - frame.Input.ScrollDelta * frame.Units(22f), 0f,
                MathF.Max(0f, height - body.Height));
        }
    }

    private static void DrawGroup(in AppletFrame frame, Rect row, string label)
    {
        var gold = frame.Theme.Palette.WarmAccent with { W = 0.45f };
        var mid = row.Center.Y;
        var inset = row.Inset(new Edges(0f, frame.Units(4f)));
        frame.Paint.Fill(new Rect(new Vector2(inset.Min.X, mid), new Vector2(inset.Min.X + inset.Width * 0.18f, mid + 1f)),
            gold);
        frame.Paint.Fill(new Rect(new Vector2(inset.Max.X - inset.Width * 0.18f, mid),
            new Vector2(inset.Max.X, mid + 1f)), gold);
        frame.Text.DrawIn(inset, label,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
    }

    private static void DrawPurseRow(in AppletFrame frame, Rect row, GameCurrency currency)
    {
        CardChrome.DrawGold(frame, row);
        var inset = row.Inset(new Edges(frame.Units(8f), frame.Units(6f)));
        var mark = inset.LeftSlice(frame.Units(28f));
        var texture = currency.IconId == 0 ? null : frame.Textures.GameIcon(currency.IconId);
        if (texture is { IsReady: true })
        {
            frame.Paint.Image(texture, CoverFit.Contained(texture.Size, mark), Vector4.One);
        }

        var copy = inset.Inset(new Edges(frame.Units(34f), 0f, 0f, 0f));
        frame.Text.DrawEllipsized(copy.LeftSlice(copy.Width * 0.48f), currency.Name,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(copy.RightSlice(copy.Width * 0.52f), PurseAmount(currency),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Right));
    }

    private static string PurseAmount(GameCurrency currency)
    {
        var held = currency.Held.ToString("N0", CultureInfo.CurrentCulture);
        if (currency.Cap == 0)
        {
            return held;
        }

        var line = held + " / " + currency.Cap.ToString("N0", CultureInfo.CurrentCulture);
        if (currency.WeeklyCap == 0)
        {
            return line;
        }

        return line + " (" + currency.WeeklyHeld.ToString("N0", CultureInfo.CurrentCulture) + " / " +
            currency.WeeklyCap.ToString("N0", CultureInfo.CurrentCulture) + ")";
    }

    private void Menu(in AppletFrame frame, ref Stack stack, Rect clip, string title, string detail, string kicker,
        GameMenu menu)
    {
        var row = TakeRow(frame, ref stack, title, Line(detail, kicker));
        if (Hit(frame, clip, row))
        {
            chat.OpenGameMenu(menu);
        }
    }

    private void JumpHub(in AppletFrame frame, ref Stack stack, Rect clip, string title, string detail,
        DestinationTab tab, int pane)
    {
        var row = TakeRow(frame, ref stack, title, Line(detail, "On the phone"));
        if (Hit(frame, clip, row))
        {
            hub.Open(tab, pane);
        }
    }

    private static void JumpPhone(in AppletFrame frame, ref Stack stack, Rect clip, string title, string detail,
        string appletId)
    {
        var row = TakeRow(frame, ref stack, title, Line(detail, "On the phone"));
        if (Hit(frame, clip, row))
        {
            frame.Router.Open(appletId);
        }
    }

    private static Rect TakeRow(in AppletFrame frame, ref Stack stack, string title, string line)
    {
        var row = stack.Take(line.Length == 0 ? frame.Units(44f) : frame.Units(52f));
        CardChrome.DrawGold(frame, row);
        DrawRow(frame, row, title, line);
        return row;
    }

    private static bool Hit(in AppletFrame frame, Rect clip, Rect row)
    {
        if (!row.Overlaps(clip))
        {
            return false;
        }

        return frame.Input.ConsumeClick(row.Intersect(clip));
    }

    private static string Line(string detail, string kicker)
    {
        if (detail.Length == 0)
        {
            return kicker;
        }

        if (kicker.Length == 0)
        {
            return detail;
        }

        return kicker + " · " + detail;
    }

    private static void DrawRow(in AppletFrame frame, Rect row, string title, string line)
    {
        var inset = row.Inset(new Edges(frame.Units(12f), frame.Units(8f)));
        if (line.Length == 0)
        {
            frame.Text.DrawEllipsized(inset, title,
                new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
            return;
        }

        var lines = new Stack(inset, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawEllipsized(lines.Take(frame.Units(18f)), title,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(lines.Take(frame.Units(16f)), line,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private string GilLine()
    {
        if (!game.IsLoggedIn)
        {
            return string.Empty;
        }

        return game.Gil.ToString("N0", CultureInfo.InvariantCulture) + " gil";
    }

    private string RetainerLine()
    {
        var count = game.Retainers.Count;
        if (count == 0)
        {
            count = pearl.Current.Retainers.Length;
        }

        if (count == 0)
        {
            return game.RetainersReady ? "None yet" : "Open a retainer bell";
        }

        return count == 1 ? "1 retainer" : count.ToString(CultureInfo.InvariantCulture) + " retainers";
    }

    private string FriendLine()
    {
        var roster = talk.Friends();
        var online = 0;
        for (var index = 0; index < roster.Count; index++)
        {
            if (roster[index].Online)
            {
                online++;
            }
        }

        if (roster.Count == 0)
        {
            return "No game friends yet";
        }

        return online.ToString(CultureInfo.InvariantCulture) + " online · " +
            roster.Count.ToString(CultureInfo.InvariantCulture) + " in Eorzea";
    }

    private string PartyLine()
    {
        if (!game.IsInParty)
        {
            return "Not in a party";
        }

        var size = game.PartySize;
        return size == 1 ? "1 in party" : size.ToString(CultureInfo.InvariantCulture) + " in party";
    }
}
