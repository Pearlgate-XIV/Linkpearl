using System.Globalization;
using System.Linq;
using Linkpearl.Applets;
using Linkpearl.Badges;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Pearls;

namespace Linkpearl.Applets.Life.Wallet;

public sealed class WalletApplet : IApplet
{
    private enum Page : byte
    {
        Home = 0,
        Earn = 1,
        Daily = 2,
        Shop = 3,
        Item = 4,
        Rewards = 5,
        History = 6,
        Spin = 7,
        Shells = 8,
    }

    public static readonly AppletManifest Manifest = new()
    {
        Id = "wallet",
        DisplayNameKey = "Pearls",
        Family = AppletFamily.Life,
        Glyph = "⬡",
        HomeOrder = 32,
    };

    private readonly HostPaths paths;
    private readonly BadgeBook badges;
    private readonly PearlLedger pearls;
    private Page page = Page.Home;
    private int earnTab;
    private int shopTab;
    private int historyTab;
    private int rewardTab;
    private int wager = 50;
    private string itemId = string.Empty;
    private string notice = string.Empty;
    private float scroll;

    public WalletApplet(HostPaths paths, BadgeBook badges, PearlLedger pearls)
    {
        this.paths = paths;
        this.badges = badges;
        this.pearls = pearls;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => pearls.CanCheckIn() ? new AppletBadge(1, true) : AppletBadge.None;

    public string Place => page.ToString().ToLowerInvariant();

    public void Enter(AppletEntry entry)
    {
        pearls.TryClaim(40, "Opened Pearls", "You opened the currency glass.", "app-open");
        if (entry.RouteHint is { Length: > 0 } hint)
        {
            page = Enum.TryParse<Page>(hint, true, out var parsed) ? parsed : Page.Home;
            scroll = 0f;
        }
    }

    public void Leave()
    {
    }

    public bool CanGoBack => page != Page.Home;

    public bool Back()
    {
        if (page == Page.Home)
        {
            return false;
        }

        if (page == Page.Item)
        {
            Open(Page.Shop);
            return true;
        }

        Open(Page.Home);
        return true;
    }

    public void Compose(in AppletFrame frame)
    {
        WalletChrome.PaintGround(frame, frame.Content);
        var nav = frame.Units(52f);
        var body = frame.Content.Inset(new Edges(frame.Units(14f), frame.Units(8f), frame.Units(14f),
            nav + frame.Units(6f)));
        frame.Paint.PushClip(body);
        var shifted = body.Translate(new Vector2(0f, -scroll));
        var height = page switch
        {
            Page.Earn => DrawEarn(frame, shifted),
            Page.Daily => DrawDaily(frame, shifted),
            Page.Shop => DrawShop(frame, shifted),
            Page.Item => DrawItem(frame, shifted),
            Page.Rewards => DrawRewards(frame, shifted),
            Page.History => DrawHistory(frame, shifted),
            Page.Spin => DrawSpin(frame, shifted),
            Page.Shells => DrawShells(frame, shifted),
            _ => DrawHome(frame, shifted),
        };
        frame.Paint.PopClip();
        ScrollSlider.Apply(frame, body, ref scroll, height);

        DrawNav(frame, frame.Content.BottomSlice(nav));
    }

    private void Open(Page next)
    {
        page = next;
        scroll = 0f;
        notice = string.Empty;
    }

    private float DrawHome(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        WalletChrome.Hero(frame, stack.Take(frame.Units(112f)), pearls.Balance, pearls.LifetimeEarned,
            pearls.LifetimeSpent);

        var today = stack.Take(frame.Units(58f));
        CardChrome.DrawGold(frame, today);
        var todayIn = today.Inset(new Edges(frame.Units(12f), frame.Units(8f)));
        frame.Text.DrawIn(todayIn.TopSlice(frame.Units(14f)), "TODAY",
            new TextStyle(FontRole.CaptionStrong, gold));
        frame.Text.DrawIn(todayIn.Inset(new Edges(0f, frame.Units(16f), todayIn.Width * 0.42f, 0f)),
            "+" + pearls.EarnedToday.ToString("N0", CultureInfo.CurrentCulture) + " earned",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(todayIn.RightSlice(todayIn.Width * 0.42f).Inset(new Edges(0f, frame.Units(16f), 0f, 0f)),
            pearls.SpinRemaining().ToString(CultureInfo.InvariantCulture) + " spin  ·  " +
            pearls.ShellsRemaining().ToString(CultureInfo.InvariantCulture) + " shells",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Right));

        var check = stack.Take(frame.Units(108f));
        CardChrome.DrawGold(frame, check);
        var inset = check.Inset(frame.Units(12f));
        var head = inset.TopSlice(frame.Units(16f));
        frame.Text.DrawIn(head.LeftSlice(head.Width * 0.58f), "DAILY CHECK-IN",
            new TextStyle(FontRole.CaptionStrong, gold));
        frame.Text.DrawIn(head.RightSlice(head.Width * 0.42f),
            pearls.Streak.ToString(CultureInfo.InvariantCulture) + " day streak",
            new TextStyle(FontRole.Caption, gold, TextAlign.Right));
        DrawStreak(frame, inset.Inset(new Edges(0f, frame.Units(20f), 0f, frame.Units(36f))), pearls.Streak);
        var claim = inset.BottomSlice(frame.Units(32f));
        if (pearls.CanCheckIn())
        {
            if (WalletChrome.GoldButton(frame, claim,
                    "Check in  ·  +" + pearls.NextCheckReward().ToString(CultureInfo.InvariantCulture)))
            {
                pearls.TryCheckIn(out _);
            }
        }
        else
        {
            WalletChrome.GhostButton(frame, claim, "Checked in  ·  back at midnight");
            if (frame.Input.ConsumeClick(check))
            {
                Open(Page.Daily);
            }
        }

        var lane = stack.Take(frame.Units(64f));
        var gap = frame.Units(8f);
        var half = (lane.Width - gap) * 0.5f;
        DrawLane(frame, Rect.FromSize(lane.Min, new Vector2(half, lane.Height)), "EARN", "How Pearls arrive",
            Page.Earn);
        DrawLane(frame, Rect.FromSize(new Vector2(lane.Min.X + half + gap, lane.Min.Y), new Vector2(half, lane.Height)),
            "PLAY", "Spin and shells", Page.Spin);

        var recent = stack.Take(frame.Units(18f));
        WalletChrome.Kicker(frame, recent.LeftSlice(recent.Width * 0.62f), "RECENT");
        frame.Text.DrawIn(recent.RightSlice(recent.Width * 0.38f), "See all",
            new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Right));
        if (frame.Input.ConsumeClick(recent.RightSlice(recent.Width * 0.38f)))
        {
            Open(Page.History);
        }

        var shown = 0;
        foreach (var row in pearls.History)
        {
            if (shown >= 4)
            {
                break;
            }

            DrawTx(frame, stack.Take(frame.Units(48f)), row);
            shown++;
        }

        if (shown == 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(22f)), "Use Linkpearl. Pearls follow.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        frame.Text.DrawWrapped(stack.Take(frame.Units(36f)),
            "Play money only. Pearls cannot be bought with gil or real money, and they do not move between people.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));

        return area.Height - stack.Remaining.Height;
    }

    private void DrawLane(in AppletFrame frame, Rect cell, string kicker, string title, Page next)
    {
        CardChrome.DrawGold(frame, cell);
        var inset = cell.Inset(new Edges(frame.Units(12f), frame.Units(10f)));
        WalletChrome.Kicker(frame, inset.TopSlice(frame.Units(14f)), kicker);
        frame.Text.DrawEllipsized(inset.BottomSlice(frame.Units(20f)), title,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        if (frame.Input.ConsumeClick(cell))
        {
            Open(next);
        }
    }

    private float DrawEarn(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        DrawBack(frame, stack.Take(frame.Units(26f)), "Earn", Page.Home);
        DrawBalanceStrip(frame, stack.Take(frame.Units(40f)));
        var tabs = stack.Take(frame.Units(28f));
        DrawTabs(frame, tabs, ["Daily", "Achievements", "Apps"], ref earnTab);

        if (earnTab == 0)
        {
            DrawEarnRow(frame, stack.Take(frame.Units(56f)), "Daily check-in",
                "A small morning pearl. Streaks grow the seventh day.",
                pearls.CanCheckIn() ? "+" + pearls.NextCheckReward() : "Claimed", Page.Daily);
            DrawEarnRow(frame, stack.Take(frame.Units(56f)), "Achievements",
                "Milestones from using the communicator.", "+75–200", Page.Earn, 1);
            DrawEarnRow(frame, stack.Take(frame.Units(56f)), "App tasks",
                "Participation inside Linkpearl apps.", "+25–50", Page.Earn, 2);
        }
        else if (earnTab == 1)
        {
            DrawClaim(frame, stack.Take(frame.Units(52f)), "First check-in", "Claim a morning once.", 100,
                "ach-first-checkin", pearls.CheckIns > 0 || pearls.Claimed("ach-first-checkin"));
            DrawClaim(frame, stack.Take(frame.Units(52f)), "Week streak", "Seven consecutive mornings.", 200,
                "ach-week-7", pearls.Streak >= 7 || pearls.Claimed("ach-week-7"));
            DrawClaim(frame, stack.Take(frame.Units(52f)), "Regular", "Five check-ins, any days.", 100, "ach-regular",
                pearls.CheckIns >= 5 || pearls.Claimed("ach-regular"));
            DrawClaim(frame, stack.Take(frame.Units(52f)), "First cosmetic", "Buy a shop badge.", 75, "ach-first-buy",
                false);
            DrawClaim(frame, stack.Take(frame.Units(52f)), "Earner", "Earn 1,000 Pearls in total.", 150, "ach-earner",
                pearls.LifetimeEarned >= 1000);
            frame.Text.DrawIn(stack.Take(frame.Units(28f)),
                "Achievement badges stay earned. They never appear in the shop.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }
        else
        {
            DrawClaim(frame, stack.Take(frame.Units(52f)), "Opened Pearls", "Visit the currency glass.", 40, "app-open",
                true);
            DrawClaim(frame, stack.Take(frame.Units(52f)), "Daily habit", "Complete any check-in.", 50, "app-daily",
                pearls.CheckIns > 0);
            DrawClaim(frame, stack.Take(frame.Units(52f)), "Shop window", "Open the Pearl Shop.", 25, "app-shop",
                shopTab >= 0);
            if (badges.Owns("patron"))
            {
                DrawClaim(frame, stack.Take(frame.Units(52f)), "Supporter thank-you",
                    "A gift beside your patron mark. Never sold.", 500, "gift-patron", true, PearlKind.Gift);
            }

            frame.Text.DrawIn(stack.Take(frame.Units(32f)),
                "Pearls come from using Linkpearl. They cannot be bought with gil or real money.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        return area.Height - stack.Remaining.Height;
    }

    private float DrawDaily(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        DrawBack(frame, stack.Take(frame.Units(26f)), "Check-in", Page.Earn);
        frame.Text.DrawIn(stack.Take(frame.Units(16f)),
            pearls.Streak.ToString(CultureInfo.InvariantCulture) + " DAY STREAK",
            new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center));
        DrawStreak(frame, stack.Take(frame.Units(36f)), pearls.Streak);
        var claim = stack.Take(frame.Units(40f));
        if (pearls.CanCheckIn())
        {
            if (WalletChrome.GoldButton(frame, claim,
                    "CLAIM " + pearls.NextCheckReward().ToString(CultureInfo.InvariantCulture)))
            {
                pearls.TryCheckIn(out _);
            }
        }
        else
        {
            WalletChrome.GhostButton(frame, claim, "Already claimed today");
        }

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "HOW IT WORKS",
            new TextStyle(FontRole.CaptionStrong, gold));
        frame.Text.DrawWrapped(stack.Take(frame.Units(72f)),
            "Check in once per local day. Miss a day and the streak returns to one. Day seven is the weekly bonus. Pearls only — no gil, no real money.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        return area.Height - stack.Remaining.Height;
    }

    private float DrawShop(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        WalletChrome.Title(frame, stack.Take(frame.Units(28f)), "Shop");
        DrawBalanceStrip(frame, stack.Take(frame.Units(40f)));
        pearls.TryClaim(25, "Shop window", "You opened the Pearl Shop.", "app-shop");
        DrawTabs(frame, stack.Take(frame.Units(30f)), ["Featured", "Badges", "Owned"], ref shopTab);

        if (shopTab == 2)
        {
            var any = false;
            foreach (var spec in BadgeCatalog.ExclusiveItems().Concat(BadgeCatalog.ShopItems()))
            {
                if (!badges.Owns(spec.Id))
                {
                    continue;
                }

                DrawShopRow(frame, stack.Take(frame.Units(64f)), spec, owned: true);
                any = true;
            }

            if (!any)
            {
                frame.Text.DrawIn(stack.Take(frame.Units(24f)), "Nothing collected yet.",
                    new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            }

            return area.Height - stack.Remaining.Height;
        }

        var items = BadgeCatalog.ShopItems().ToArray();
        var exclusives = BadgeCatalog.ExclusiveItems().ToArray();
        if (shopTab == 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(16f)), "EXCLUSIVE",
                new TextStyle(FontRole.CaptionStrong, gold));
            for (var index = 0; index < exclusives.Length; index++)
            {
                DrawShopRow(frame, stack.Take(frame.Units(64f)), exclusives[index], badges.Owns(exclusives[index].Id));
            }

            frame.Text.DrawIn(stack.Take(frame.Units(16f)), "FEATURED COSMETICS",
                new TextStyle(FontRole.CaptionStrong, gold));
            DrawShopRow(frame, stack.Take(frame.Units(64f)), items[0], badges.Owns(items[0].Id));
            DrawShopRow(frame, stack.Take(frame.Units(64f)), items[8], badges.Owns(items[8].Id));
            DrawShopRow(frame, stack.Take(frame.Units(64f)), items[24], badges.Owns(items[24].Id));
            DrawShopRow(frame, stack.Take(frame.Units(64f)), items[32], badges.Owns(items[32].Id));
            frame.Text.DrawIn(stack.Take(frame.Units(28f)),
                "Founder and Patron are granted, not sold. Themes and frames will land here later.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            return area.Height - stack.Remaining.Height;
        }

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "EXCLUSIVE",
            new TextStyle(FontRole.CaptionStrong, gold));
        for (var index = 0; index < exclusives.Length; index++)
        {
            DrawShopRow(frame, stack.Take(frame.Units(64f)), exclusives[index], badges.Owns(exclusives[index].Id));
        }

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "PEARL BADGES",
            new TextStyle(FontRole.CaptionStrong, gold));
        foreach (var spec in items)
        {
            DrawShopRow(frame, stack.Take(frame.Units(64f)), spec, badges.Owns(spec.Id));
        }

        return area.Height - stack.Remaining.Height;
    }

    private float DrawItem(in AppletFrame frame, Rect area)
    {
        if (BadgeCatalog.Find(itemId) is not { } spec)
        {
            Open(Page.Shop);
            return 0f;
        }

        var gold = frame.Theme.Palette.WarmAccent;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        DrawBack(frame, stack.Take(frame.Units(26f)), spec.Name, Page.Shop);
        var face = stack.Take(frame.Units(120f));
        CardChrome.DrawGold(frame, face);
        WalletChrome.BadgeFace(frame, face.Inset(frame.Units(16f)), spec, paths);
        var owned = badges.Owns(spec.Id);
        frame.Text.DrawIn(stack.Take(frame.Units(18f)),
            owned
                ? "Owned"
                : spec.Purchasable
                    ? spec.Price.ToString("N0", CultureInfo.CurrentCulture) + " Pearls"
                    : spec.Source,
            new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center));
        frame.Text.DrawWrapped(stack.Take(frame.Units(48f)), spec.Description,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), spec.Source,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint));
        if (notice.Length > 0 && notice != "confirm-buy")
        {
            frame.Text.DrawIn(stack.Take(frame.Units(18f)), notice,
                new TextStyle(FontRole.Caption, gold, TextAlign.Center));
        }

        if (owned)
        {
            if (WalletChrome.GoldButton(frame, stack.Take(frame.Units(36f)), "Equip on profile"))
            {
                badges.SetFeatured(spec.Id);
                notice = "Featured on your profile.";
            }
        }
        else if (spec.Purchasable && WalletChrome.GoldButton(frame, stack.Take(frame.Units(40f)),
                     notice == "confirm-buy"
                         ? "Confirm  ·  " + spec.Price.ToString("N0", CultureInfo.CurrentCulture)
                         : "Buy  ·  " + spec.Price.ToString("N0", CultureInfo.CurrentCulture)))
        {
            if (notice == "confirm-buy")
            {
                Buy(spec);
            }
            else
            {
                notice = "confirm-buy";
            }
        }

        return area.Height - stack.Remaining.Height;
    }

    private float DrawRewards(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        WalletChrome.Title(frame, stack.Take(frame.Units(28f)), "Items");
        DrawTabs(frame, stack.Take(frame.Units(30f)), ["Earned", "Collection"], ref rewardTab);
        var earned = 0;
        var bought = 0;
        for (var index = 0; index < badges.Owned.Count; index++)
        {
            if (BadgeCatalog.Find(badges.Owned[index].Id) is not { } spec)
            {
                continue;
            }

            if (spec.Kind == BadgeKind.Cosmetic)
            {
                bought++;
            }
            else
            {
                earned++;
            }
        }

        var stats = stack.Take(frame.Units(48f));
        DrawStat(frame, stats.LeftSlice(stats.Width * 0.48f), "Achievements",
            earned.ToString(CultureInfo.InvariantCulture) + " unlocked");
        DrawStat(frame, stats.RightSlice(stats.Width * 0.48f), "Collection",
            bought.ToString(CultureInfo.InvariantCulture) + " owned");

        var wantCosmetic = rewardTab == 1;
        var any = false;
        for (var index = 0; index < badges.Owned.Count; index++)
        {
            var own = badges.Owned[index];
            if (BadgeCatalog.Find(own.Id) is not { } spec)
            {
                continue;
            }

            if ((spec.Kind == BadgeKind.Cosmetic) != wantCosmetic)
            {
                continue;
            }

            var row = stack.Take(frame.Units(58f));
            CardChrome.Draw(frame, row);
            var inset = row.Inset(new Edges(frame.Units(8f), frame.Units(6f)));
            WalletChrome.BadgeFace(frame, inset.LeftSlice(frame.Units(40f)), spec, paths);
            var copy = inset.Inset(new Edges(frame.Units(46f), 0f, 0f, 0f));
            frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(16f)), spec.Name,
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
            frame.Text.DrawEllipsized(copy.Inset(new Edges(0f, frame.Units(16f), 0f, frame.Units(14f))), spec.Source,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(14f)).Inset(new Edges(0f, 0f, frame.Units(52f), 0f)),
                WalletChrome.When(own.EarnedAtUnix) + " · " + BadgeCatalog.CategoryName(spec.Category),
                new TextStyle(FontRole.Caption, gold));
            var wear = copy.RightSlice(frame.Units(52f)).BottomSlice(frame.Units(20f));
            frame.Text.DrawIn(wear, string.Equals(badges.FeaturedId, spec.Id, StringComparison.Ordinal) ? "On" : "Wear",
                new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Right));
            if (frame.Input.ConsumeClick(row))
            {
                badges.SetFeatured(spec.Id);
                notice = spec.Name + " is on your profile.";
            }

            any = true;
        }

        if (!any)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(24f)),
                wantCosmetic ? "Buy a cosmetic badge in the shop." : "Earned marks arrive as you use Linkpearl.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }
        else if (notice.Length > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(16f)), notice,
                new TextStyle(FontRole.Caption, gold, TextAlign.Center));
        }

        return area.Height - stack.Remaining.Height;
    }

    private float DrawHistory(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        WalletChrome.Title(frame, stack.Take(frame.Units(28f)), "History");
        DrawBalanceStrip(frame, stack.Take(frame.Units(40f)));
        DrawTabs(frame, stack.Take(frame.Units(30f)), ["All", "Earned", "Spent", "Gifts"], ref historyTab);
        var any = false;
        var lastDay = string.Empty;
        foreach (var row in pearls.Filtered(historyTab))
        {
            var day = WalletChrome.DayLabel(row.AtUnix);
            if (!string.Equals(day, lastDay, StringComparison.Ordinal))
            {
                WalletChrome.Kicker(frame, stack.Take(frame.Units(16f)), day);
                lastDay = day;
            }

            DrawTx(frame, stack.Take(frame.Units(48f)), row);
            any = true;
        }

        if (!any)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(22f)), "Nothing in this filter yet.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        return area.Height - stack.Remaining.Height;
    }

    private float DrawSpin(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        DrawBack(frame, stack.Take(frame.Units(26f)), "Pearl Spin", Page.Home);
        DrawBalanceStrip(frame, stack.Take(frame.Units(40f)));
        frame.Text.DrawIn(stack.Take(frame.Units(16f)),
            "Today's wager " + (PearlLedger.SpinCap - pearls.SpinRemaining()).ToString(CultureInfo.InvariantCulture) +
            "/" + PearlLedger.SpinCap.ToString(CultureInfo.InvariantCulture),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        var wheel = stack.Take(frame.Units(120f));
        DrawWheel(frame, wheel);
        var bets = stack.Take(frame.Units(32f));
        var wagers = new[] { 25, 50, 100 };
        var betW = bets.Width / 3f;
        for (var index = 0; index < wagers.Length; index++)
        {
            var cell = Rect.FromSize(new Vector2(bets.Min.X + betW * index + frame.Units(3f), bets.Min.Y),
                new Vector2(betW - frame.Units(6f), bets.Height));
            if (WalletChrome.Chip(frame, cell, wagers[index].ToString(CultureInfo.InvariantCulture),
                    wager == wagers[index]))
            {
                wager = wagers[index];
            }
        }

        if (notice.Length > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(18f)), notice,
                new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center));
        }

        if (WalletChrome.GoldButton(frame, stack.Take(frame.Units(36f)),
                "SPIN (" + wager.ToString(CultureInfo.InvariantCulture) + ")"))
        {
            if (pearls.TrySpin(wager, out var payout, out var slice))
            {
                notice = payout == 0
                    ? "The wheel kept it."
                    : "Returned " + payout.ToString("N0", CultureInfo.InvariantCulture) + " · " +
                      PearlLedger.SpinMultipliers[slice].ToString("0.#", CultureInfo.InvariantCulture) + "×";
            }
            else
            {
                notice = pearls.SpinRemaining() < wager ? "Daily spin cap reached." : "Not enough Pearls.";
            }
        }

        if (WalletChrome.GhostButton(frame, stack.Take(frame.Units(32f)), "Lucky shells"))
        {
            Open(Page.Shells);
        }

        frame.Text.DrawIn(stack.Take(frame.Units(36f)),
            "A small side game. Caps keep it from becoming the way Pearls are made.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        return area.Height - stack.Remaining.Height;
    }

    private float DrawShells(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        DrawBack(frame, stack.Take(frame.Units(26f)), "Lucky Shell", Page.Spin);
        DrawBalanceStrip(frame, stack.Take(frame.Units(36f)));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)),
            pearls.ShellsRemaining().ToString(CultureInfo.InvariantCulture) + " plays left today · " +
            PearlLedger.ShellCost.ToString(CultureInfo.InvariantCulture) + " Pearls",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        var row = stack.Take(frame.Units(88f));
        var cell = row.Width / 3f;
        for (var index = 0; index < 3; index++)
        {
            var shell = Rect.FromSize(new Vector2(row.Min.X + cell * index + frame.Units(4f), row.Min.Y),
                new Vector2(cell - frame.Units(8f), row.Height));
            CardChrome.DrawGold(frame, shell);
            WalletChrome.Pearl(frame, shell.Inset(frame.Units(12f)));
            if (frame.Input.ConsumeClick(shell))
            {
                if (pearls.TryShell(index, out var won))
                {
                    notice = won
                        ? "The pearl was yours. +" + PearlLedger.ShellWin.ToString(CultureInfo.InvariantCulture)
                        : "Empty shell.";
                }
                else
                {
                    notice = pearls.ShellsRemaining() == 0 ? "Come back tomorrow." : "Not enough Pearls.";
                }
            }
        }

        if (notice.Length > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(20f)), notice,
                new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center));
        }

        return area.Height - stack.Remaining.Height;
    }

    private void DrawNav(in AppletFrame frame, Rect bar)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var cell = bar.Width / WalletChrome.Nav.Length;
        var active = page switch
        {
            Page.Shop or Page.Item => 1,
            Page.Rewards => 2,
            Page.History => 3,
            _ => 0,
        };

        for (var index = 0; index < WalletChrome.Nav.Length; index++)
        {
            var item = Rect.FromSize(new Vector2(bar.Min.X + cell * index, bar.Min.Y), new Vector2(cell, bar.Height));
            var on = index == active;
            frame.Text.DrawIn(item.Inset(new Edges(0f, frame.Units(14f), 0f, frame.Units(6f))), WalletChrome.Nav[index],
                new TextStyle(on ? FontRole.CaptionStrong : FontRole.Caption,
                    on ? gold : frame.Theme.Palette.InkMuted, TextAlign.Center));
            if (on)
            {
                var mark = item.BottomSlice(frame.Units(4f));
                frame.Paint.Fill(Rect.FromSize(new Vector2(item.Center.X - frame.Units(10f), mark.Min.Y),
                    new Vector2(frame.Units(20f), frame.Units(3f))), gold, frame.Units(2f));
            }

            if (frame.Input.ConsumeClick(item))
            {
                Open(index switch
                {
                    1 => Page.Shop,
                    2 => Page.Rewards,
                    3 => Page.History,
                    _ => Page.Home,
                });
            }
        }
    }

    private void DrawBack(in AppletFrame frame, Rect row, string title, Page back)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var hit = row.LeftSlice(frame.Units(28f));
        frame.Text.DrawIn(hit, "‹", new TextStyle(FontRole.Title, gold, TextAlign.Center));
        frame.Text.DrawEllipsized(row.Inset(new Edges(frame.Units(28f), 0f, 0f, 0f)), title,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        if (frame.Input.ConsumeClick(row.LeftSlice(frame.Units(80f))))
        {
            Open(back);
        }
    }

    private void DrawBalanceStrip(in AppletFrame frame, Rect row)
    {
        CardChrome.Draw(frame, row);
        var inset = row.Inset(new Edges(frame.Units(10f), 0f));
        WalletChrome.Pearl(frame, inset.LeftSlice(frame.Units(28f)));
        frame.Text.DrawIn(inset.Inset(new Edges(frame.Units(34f), 0f, 0f, 0f)),
            pearls.Balance.ToString("N0", CultureInfo.CurrentCulture) + " Pearls",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.WarmAccent));
    }

    private static void DrawTabs(in AppletFrame frame, Rect row, string[] labels, ref int selected)
    {
        var width = row.Width / labels.Length;
        for (var index = 0; index < labels.Length; index++)
        {
            var cell = Rect.FromSize(new Vector2(row.Min.X + width * index + frame.Units(2f), row.Min.Y),
                new Vector2(width - frame.Units(4f), row.Height));
            if (WalletChrome.Chip(frame, cell, labels[index], selected == index))
            {
                selected = index;
            }
        }
    }

    private static void DrawStreak(in AppletFrame frame, Rect row, int streak)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var gap = frame.Units(4f);
        var size = (row.Width - gap * 6f) / 7f;
        for (var index = 0; index < 7; index++)
        {
            var cell = Rect.FromSize(new Vector2(row.Min.X + index * (size + gap), row.Min.Y),
                new Vector2(size, row.Height));
            var lit = streak > 0 && ((streak - 1) % 7) >= index || (streak > 0 && streak % 7 == 0);
            if (streak > 0 && streak % 7 == 0)
            {
                lit = true;
            }
            else
            {
                lit = index < (streak % 7);
            }

            frame.Paint.StrokeCircle(cell.Center, MathF.Min(cell.Width, cell.Height) * 0.32f,
                lit ? gold : gold with { W = 0.28f }, frame.Units(1.2f));
            if (lit)
            {
                frame.Paint.FillCircle(cell.Center, MathF.Min(cell.Width, cell.Height) * 0.12f, gold);
            }

            if (index == 6)
            {
                WalletChrome.Pearl(frame, cell.Inset(cell.Width * 0.22f));
            }
        }
    }

    private void DrawEarnRow(in AppletFrame frame, Rect row, string title, string detail, string reward, Page next,
        int tab = -1)
    {
        CardChrome.DrawGold(frame, row);
        var inset = row.Inset(new Edges(frame.Units(10f), frame.Units(6f)));
        frame.Text.DrawIn(inset.TopSlice(frame.Units(16f)), title,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(inset.Inset(new Edges(0f, frame.Units(16f), frame.Units(64f), 0f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        frame.Text.DrawIn(inset.RightSlice(frame.Units(58f)), reward,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent, TextAlign.Right));
        if (frame.Input.ConsumeClick(row))
        {
            if (tab >= 0)
            {
                earnTab = tab;
            }

            Open(next);
        }
    }

    private void DrawClaim(in AppletFrame frame, Rect row, string title, string detail, int amount, string flag,
        bool ready, PearlKind kind = PearlKind.Earned)
    {
        CardChrome.Draw(frame, row);
        var inset = row.Inset(new Edges(frame.Units(10f), frame.Units(6f)));
        var owned = pearls.Claimed(flag);
        frame.Text.DrawIn(inset.TopSlice(frame.Units(16f)), title,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(inset.BottomSlice(frame.Units(16f)).Inset(new Edges(0f, 0f, frame.Units(70f), 0f)),
            owned ? "Collected · " + detail : detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var action = inset.RightSlice(frame.Units(64f)).Inset(new Edges(0f, frame.Units(4f), 0f, frame.Units(4f)));
        if (owned)
        {
            frame.Text.DrawIn(action, "Done",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint, TextAlign.Right));
            return;
        }

        if (!ready)
        {
            frame.Text.DrawIn(action, "+" + amount.ToString(CultureInfo.InvariantCulture),
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent, TextAlign.Right));
            return;
        }

        if (WalletChrome.GoldButton(frame, action, "+" + amount.ToString(CultureInfo.InvariantCulture)))
        {
            pearls.TryClaim(amount, title, detail, flag, kind);
        }
    }

    private void DrawShopRow(in AppletFrame frame, Rect row, BadgeSpec spec, bool owned)
    {
        CardChrome.DrawGold(frame, row);
        var inset = row.Inset(new Edges(frame.Units(8f), frame.Units(6f)));
        WalletChrome.BadgeFace(frame, inset.LeftSlice(frame.Units(48f)), spec, paths);
        var copy = inset.Inset(new Edges(frame.Units(54f), 0f, frame.Units(64f), 0f));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(18f)), spec.Name,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)), BadgeCatalog.CategoryName(spec.Category),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        frame.Text.DrawIn(inset.RightSlice(frame.Units(60f)),
            owned ? "Owned" : spec.Purchasable ? spec.Price.ToString("N0", CultureInfo.CurrentCulture) : "Locked",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent, TextAlign.Right));
        if (frame.Input.ConsumeClick(row))
        {
            itemId = spec.Id;
            Open(Page.Item);
        }
    }

    private static void DrawTx(in AppletFrame frame, Rect row, PearlTx tx)
    {
        CardChrome.Draw(frame, row);
        var inset = row.Inset(new Edges(frame.Units(10f), frame.Units(4f)));
        frame.Text.DrawEllipsized(inset.TopSlice(frame.Units(16f)).Inset(new Edges(0f, 0f, frame.Units(64f), 0f)),
            tx.Title, new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        var detail = tx.Detail.Length > 0 ? tx.Detail : WalletChrome.When(tx.AtUnix);
        frame.Text.DrawEllipsized(inset.BottomSlice(frame.Units(16f)).Inset(new Edges(0f, 0f, frame.Units(64f), 0f)),
            detail + "  ·  " + WalletChrome.When(tx.AtUnix),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        WalletChrome.Signed(frame, inset.RightSlice(frame.Units(60f)), tx.Amount);
    }

    private static void DrawStat(in AppletFrame frame, Rect area, string title, string value)
    {
        CardChrome.DrawGold(frame, area);
        var inset = area.Inset(frame.Units(8f));
        frame.Text.DrawIn(inset.TopSlice(frame.Units(14f)), title,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        frame.Text.DrawIn(inset.BottomSlice(frame.Units(18f)), value,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
    }

    private static void DrawWheel(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var radius = MathF.Min(area.Width, area.Height) * 0.42f;
        frame.Paint.StrokeCircle(area.Center, radius, gold, frame.Units(2f));
        WalletChrome.Pearl(frame, Rect.FromSize(area.Center - new Vector2(radius * 0.28f),
            new Vector2(radius * 0.56f, radius * 0.56f)));
        for (var index = 0; index < PearlLedger.SpinMultipliers.Length; index++)
        {
            var angle = index * (MathF.PI * 2f / PearlLedger.SpinMultipliers.Length) - MathF.PI * 0.5f;
            var tip = area.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * 0.82f;
            frame.Text.Draw(tip, PearlLedger.SpinMultipliers[index].ToString("0.#", CultureInfo.InvariantCulture) + "×",
                new TextStyle(FontRole.Caption, gold, TextAlign.Center));
        }
    }

    private void Buy(BadgeSpec spec)
    {
        if (!spec.Purchasable || spec.Kind != BadgeKind.Cosmetic)
        {
            notice = "This mark is not for sale.";
            return;
        }

        if (badges.Owns(spec.Id))
        {
            notice = "Already in your collection.";
            return;
        }

        if (!pearls.TrySpend(spec.Price, spec.Name, "Cosmetic badge."))
        {
            notice = "Not enough Pearls.";
            return;
        }

        if (!badges.TryPurchase(spec.Id))
        {
            pearls.Refund(spec.Price, "Shop refund", "The badge could not be granted.");
            notice = "Could not grant that badge.";
            return;
        }

        pearls.NotePurchase();
        notice = "Unlocked. Find it under Rewards and on your profile.";
        if (badges.FeaturedId.Length == 0)
        {
            badges.SetFeatured(spec.Id);
        }
    }
}
