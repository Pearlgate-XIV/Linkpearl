using System.Globalization;
using Linkpearl.Diagnostics;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Persistence;
using Linkpearl.Platform;

namespace Linkpearl.Applets.Life.Market;

public sealed class MarketApplet : IApplet
{
    private static readonly string[] ScopeMarks = ["World", "DC", "Region"];
    private static readonly string[] QualityMarks = ["All", "NQ", "HQ"];

    public static readonly AppletManifest Manifest = new()
    {
        Id = "market",
        DisplayNameKey = "Market",
        Family = AppletFamily.Life,
        Glyph = "⚖",
        Capabilities = AppletCapabilities.RequiresNetwork,
        HomeOrder = 20,
    };

    private readonly IUniversalisMarket market;
    private readonly IGameItems items;
    private readonly IGameSession game;
    private readonly ILinkpearlLog log;
    private readonly string bookPath;
    private readonly List<GameMarketItem> recents = [];
    private readonly List<GameMarketItem> watches = [];
    private readonly List<int> watchIds = [];
    private IReadOnlyList<GameMarketItem> hits = [];
    private GameMarketItem opened;
    private string query = string.Empty;
    private string lastQuery = string.Empty;
    private string peeked = string.Empty;
    private bool itemOpen;
    private MarketScopeKind scope = MarketScopeKind.DataCenter;
    private int quality;
    private float scroll;
    private JsonCorruptHold corrupt;

    public MarketApplet(IUniversalisMarket market, IGameItems items, IGameSession game, HostPaths paths,
        ILinkpearlLog log)
    {
        this.market = market;
        this.items = items;
        this.game = game;
        this.log = log;
        bookPath = paths.State("market.json");
        LoadBook();
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public string Place => itemOpen && opened.Id > 0 ? "item/" + opened.Id : "browse";

    public void Enter(AppletEntry entry)
    {
        market.RefreshMaps();
        scroll = 0f;
        peeked = string.Empty;
        if (entry.RouteHint is { Length: > 0 } hint && int.TryParse(hint, out var itemId) && itemId > 0)
        {
            OpenItem(items.Find(itemId) ?? new GameMarketItem(itemId, game.ItemName((uint)itemId), 0));
        }
    }

    public void Leave()
    {
    }

    public bool CanGoBack => itemOpen;

    public bool Back()
    {
        if (!itemOpen)
        {
            return false;
        }

        itemOpen = false;
        scroll = 0f;
        return true;
    }

    public void Compose(in AppletFrame frame)
    {
        if (query.Trim() != lastQuery)
        {
            lastQuery = query.Trim();
            hits = lastQuery.Length == 0 ? [] : items.Search(lastQuery);
            peeked = string.Empty;
            scroll = 0f;
        }

        if (!itemOpen)
        {
            PeekShown();
        }

        MarketChrome.Ground(frame);
        var body = frame.Content.Inset(new Edges(frame.Units(14f), frame.Units(10f), frame.Units(14f),
            frame.Units(10f)));
        frame.Paint.PushClip(body);
        var shifted = body.Translate(new Vector2(0f, -scroll));
        var height = itemOpen ? DrawItem(frame, shifted) : DrawBrowse(frame, shifted);
        frame.Paint.PopClip();
        ScrollSlider.Apply(frame, body, ref scroll, height);
    }

    private float DrawBrowse(in AppletFrame frame, Rect area)
    {
        var stack = new LayoutFlow(area, StackAxis.Vertical, frame.Units(8f));
        MarketChrome.Title(frame, stack.Take(frame.Units(26f)), "Market");
        DrawSearch(frame, stack.Take(frame.Units(40f)));
        DrawScope(frame, stack.Take(frame.Units(30f)), reloadItem: false);

        var searching = query.Trim().Length > 0;
        var recentOnly = searching
            ? recents
            : recents.FindAll(row => !watchIds.Contains(row.Id));
        var list = searching ? hits : recentOnly;
        if (!searching && watches.Count > 0)
        {
            MarketChrome.Kicker(frame, stack.Take(frame.Units(14f)), "Watching");
            DrawHits(frame, ref stack, watches);
        }

        if (!searching && recentOnly.Count > 0)
        {
            MarketChrome.Kicker(frame, stack.Take(frame.Units(14f)), "Recent");
        }
        else if (searching && hits.Count == 0)
        {
            MarketChrome.Mute(frame, stack.Take(frame.Units(28f)), "No market item matches that name.");
        }
        else if (searching)
        {
            MarketChrome.Kicker(frame, stack.Take(frame.Units(14f)),
                hits.Count.ToString(CultureInfo.InvariantCulture) + " matches");
        }

        DrawHits(frame, ref stack, list);

        if (!searching && recents.Count == 0 && watches.Count == 0)
        {
            MarketChrome.Mute(frame, stack.Take(frame.Units(52f)),
                "Search an item, then watch it. Floors follow World, DC, or Region — tap a row for listings, sales, and an undercut.");
        }

        return area.Height - stack.Remaining.Height;
    }

    private float DrawItem(in AppletFrame frame, Rect area)
    {
        var board = market.Board;
        var stack = new LayoutFlow(area, StackAxis.Vertical, frame.Units(8f));
        DrawItemBar(frame, stack.Take(frame.Units(24f)));

        var head = stack.Take(frame.Units(72f));
        DrawIcon(frame, head.LeftSlice(frame.Units(64f)), board.IconId > 0 ? board.IconId : opened.IconId);
        var copy = head.Inset(new Edges(frame.Units(72f), frame.Units(4f), 0f, 0f));
        var title = board.ItemId == opened.Id && board.Name.Length > 0 ? board.Name : opened.Name;
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(22f)),
            title.Length > 0 ? title : "Item",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var stamp = MarketChrome.Ago(board.LastUploadUnixMs, true);
        frame.Text.DrawEllipsized(copy.Inset(new Edges(0f, frame.Units(24f), 0f, frame.Units(20f))),
            PlaceFor(scope) + (stamp.Length > 0 ? " · " + stamp : string.Empty),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(18f)),
            market.Busy ? "Refreshing Universalis…" : market.Notice,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.WarmAccent));

        DrawScope(frame, stack.Take(frame.Units(30f)), reloadItem: true);
        var nextQuality = MarketChrome.Segmented(frame, stack.Take(frame.Units(30f)), QualityMarks, quality);
        if (nextQuality != quality)
        {
            quality = nextQuality;
        }

        var shown = ShownListings(board);
        var cheapest = Cheapest(board, shown);
        var under = cheapest > 1 ? cheapest - 1 : cheapest;
        MarketChrome.Hero(frame, stack.Take(frame.Units(86f)), "Undercut",
            under > 0 ? MarketChrome.Gil(under) : "No live floor",
            under > 0
                ? "One gil under the cheapest " + QualityMarks[quality] + " listing on " + PlaceFor(scope)
                : "Nobody is selling this quality there right now.");

        DrawStats(frame, ref stack, board);

        MarketChrome.Kicker(frame, stack.Take(frame.Units(14f)),
            shown.Count == 0 ? "Listings" : "Listings · " + shown.Count.ToString(CultureInfo.InvariantCulture));
        if (!market.Busy && shown.Count == 0)
        {
            MarketChrome.Mute(frame, stack.Take(frame.Units(28f)), "Nobody is selling this there right now.");
        }

        var listingCap = Math.Min(shown.Count, 24);
        for (var index = 0; index < listingCap; index++)
        {
            DrawListing(frame, stack.Take(frame.Units(52f)), shown[index], board.Scope);
        }

        var sales = ShownSales(board);
        if (sales.Count > 0)
        {
            MarketChrome.Kicker(frame, stack.Take(frame.Units(14f)),
                "Recent sales · " + sales.Count.ToString(CultureInfo.InvariantCulture));
            var saleCap = Math.Min(sales.Count, 12);
            for (var index = 0; index < saleCap; index++)
            {
                DrawSale(frame, stack.Take(frame.Units(48f)), sales[index]);
            }
        }

        if (scope == MarketScopeKind.World && board.Taxes.Count > 0)
        {
            MarketChrome.Kicker(frame, stack.Take(frame.Units(14f)), "City tax");
            DrawTaxes(frame, stack.Take(frame.Units(36f)), board);
        }

        return area.Height - stack.Remaining.Height;
    }

    private void DrawItemBar(in AppletFrame frame, Rect row)
    {
        var back = row.LeftSlice(frame.Units(88f));
        frame.Text.DrawIn(back, "‹ Market",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
        if (frame.Input.ConsumeClick(back))
        {
            itemOpen = false;
            scroll = 0f;
            return;
        }

        var refresh = row.RightSlice(frame.Units(58f));
        var star = row.Inset(new Edges(0f, 0f, frame.Units(62f), 0f)).RightSlice(frame.Units(28f));
        if (MarketChrome.Star(frame, star, Watching(opened.Id)))
        {
            FlipWatch(opened);
        }

        frame.Text.DrawIn(refresh, market.Busy ? "…" : "Refresh",
            new TextStyle(FontRole.CaptionStrong, MarketChrome.Brand(), TextAlign.Right));
        if (!market.Busy && frame.Input.ConsumeClick(refresh))
        {
            ReloadOpened();
        }
    }

    private void DrawSearch(in AppletFrame frame, Rect field)
    {
        MarketChrome.SearchWell(frame, field);
        query = frame.TextField.Draw("market-query", field.Inset(new Edges(frame.Units(14f), 0f)), query,
            "Search items");
    }

    private void DrawScope(in AppletFrame frame, Rect row, bool reloadItem)
    {
        var next = MarketChrome.Segmented(frame, row, ScopeMarks, (int)scope);
        if (next == (int)scope)
        {
            return;
        }

        scope = (MarketScopeKind)next;
        peeked = string.Empty;
        if (reloadItem)
        {
            ReloadOpened();
        }
    }

    private void DrawHits(in AppletFrame frame, ref LayoutFlow stack, IReadOnlyList<GameMarketItem> list)
    {
        var floors = market.Floors;
        for (var index = 0; index < list.Count; index++)
        {
            DrawHit(frame, stack.Take(frame.Units(58f)), list[index],
                floors.TryGetValue(list[index].Id, out var price) ? price : 0);
        }
    }

    private void DrawHit(in AppletFrame frame, Rect row, GameMarketItem item, int price)
    {
        MarketChrome.Plate(frame, row);
        var inset = row.Inset(frame.Units(6f));
        var star = inset.RightSlice(frame.Units(28f));
        DrawIcon(frame, inset.LeftSlice(frame.Units(44f)), item.IconId);
        var body = inset.Inset(new Edges(frame.Units(52f), frame.Units(6f), frame.Units(32f), frame.Units(6f)));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), item.Name,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(body.BottomSlice(frame.Units(16f)),
            price > 0 ? "Floor · " + MarketChrome.Gil(price) : "Tap for listings",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        if (MarketChrome.Star(frame, star, Watching(item.Id)))
        {
            FlipWatch(item);
            return;
        }

        if (frame.Input.ConsumeClick(row))
        {
            OpenItem(item);
        }
    }

    private static void DrawStats(in AppletFrame frame, ref LayoutFlow stack, MarketBoard board)
    {
        var gap = frame.Units(8f);
        var row = stack.Take(frame.Units(52f));
        var half = (row.Width - gap) * 0.5f;
        MarketChrome.Stat(frame, row.LeftSlice(half), "NQ floor", MarketChrome.Gil(board.MinNq));
        MarketChrome.Stat(frame, row.RightSlice(half), "HQ floor", MarketChrome.Gil(board.MinHq));

        row = stack.Take(frame.Units(52f));
        var average = board.CurrentAverage > 0 ? board.CurrentAverage : board.Average;
        MarketChrome.Stat(frame, row.LeftSlice(half), "Average", GilAverage(average));
        MarketChrome.Stat(frame, row.RightSlice(half), "Listed / sold",
            CountMark(board.UnitsForSale) + " / " + CountMark(board.UnitsSold));
    }

    private void DrawListing(in AppletFrame frame, Rect row, MarketListing listing, string fallback)
    {
        var world = listing.World.Length > 0 ? listing.World : fallback;
        var home = IsHome(world) || (listing.World.Length == 0 && scope == MarketScopeKind.World);
        MarketChrome.Plate(frame, row, home);
        var inset = row.Inset(new Edges(frame.Units(10f), frame.Units(8f)));
        var title = MarketChrome.Gil(listing.Price) + (listing.Hq ? "  HQ" : "  NQ") +
                    (listing.Quantity > 1 ? "  ×" + listing.Quantity.ToString(CultureInfo.InvariantCulture) : string.Empty);
        frame.Text.DrawEllipsized(inset.TopSlice(frame.Units(18f)), title,
            new TextStyle(FontRole.CaptionStrong, listing.Hq ? frame.Theme.Palette.WarmAccent : frame.Theme.Palette.Ink));
        var reviewed = MarketChrome.Ago(listing.ReviewedUnix, false);
        var line = (home ? "Your world · " : string.Empty) +
                   (world.Length > 0 ? world : "Unknown world") +
                   (listing.Retainer.Length > 0 ? " · " + listing.Retainer : string.Empty) +
                   (reviewed.Length > 0 ? " · " + reviewed : string.Empty);
        frame.Text.DrawEllipsized(inset.BottomSlice(frame.Units(16f)), line,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawSale(in AppletFrame frame, Rect row, MarketSale sale)
    {
        MarketChrome.Plate(frame, row);
        var inset = row.Inset(new Edges(frame.Units(10f), frame.Units(8f)));
        var title = MarketChrome.Gil(sale.Price) + (sale.Hq ? "  HQ" : "  NQ") +
                    (sale.Quantity > 1 ? "  ×" + sale.Quantity.ToString(CultureInfo.InvariantCulture) : string.Empty);
        frame.Text.DrawEllipsized(inset.TopSlice(frame.Units(18f)), title,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        var when = MarketChrome.Ago(sale.SoldUnix, false);
        var line = (sale.World.Length > 0 ? sale.World : "Sale") +
                   (sale.Buyer.Length > 0 ? " · " + sale.Buyer : string.Empty) +
                   (when.Length > 0 ? " · " + when : string.Empty);
        frame.Text.DrawEllipsized(inset.BottomSlice(frame.Units(16f)), line,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawTaxes(in AppletFrame frame, Rect row, MarketBoard board)
    {
        MarketChrome.Plate(frame, row);
        var parts = new List<string>(board.Taxes.Count);
        for (var index = 0; index < board.Taxes.Count; index++)
        {
            var tax = board.Taxes[index];
            if (tax.City.Length == 0)
            {
                continue;
            }

            parts.Add(tax.City + " " + tax.Percent.ToString(CultureInfo.InvariantCulture) + "%");
        }

        frame.Text.DrawEllipsized(row.Inset(frame.Units(10f)), string.Join("  ·  ", parts),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawIcon(in AppletFrame frame, Rect area, uint iconId)
    {
        var texture = iconId == 0 ? null : frame.Textures.GameIcon(iconId);
        if (texture is { IsReady: true })
        {
            frame.Paint.Image(texture, CoverFit.Contained(texture.Size, area.Inset(frame.Units(2f))), Vector4.One);
            return;
        }

        frame.Paint.Fill(area, frame.Theme.Palette.SurfaceOverlay, frame.Units(8f));
    }

    private void OpenItem(GameMarketItem item)
    {
        if (item.Id <= 0)
        {
            return;
        }

        recents.RemoveAll(row => row.Id == item.Id);
        recents.Insert(0, item);
        while (recents.Count > 16)
        {
            recents.RemoveAt(recents.Count - 1);
        }

        SaveBook();
        opened = item;
        itemOpen = true;
        scroll = 0f;
        market.OpenItem(item.Id, item.Name, scope, PlaceFor(scope));
    }

    private void ReloadOpened()
    {
        if (opened.Id <= 0)
        {
            return;
        }

        market.OpenItem(opened.Id, opened.Name, scope, PlaceFor(scope));
    }

    private void PeekShown()
    {
        var ids = new List<int>(40);
        AddIds(ids, watches);
        if (hits.Count > 0)
        {
            AddIds(ids, hits);
        }
        else
        {
            AddIds(ids, recents);
        }

        var key = PlaceFor(scope) + ":" + string.Join(',', ids);
        if (key == peeked)
        {
            return;
        }

        peeked = key;
        if (ids.Count > 0)
        {
            market.Peek(ids, PlaceFor(scope));
        }
    }

    private static void AddIds(List<int> ids, IReadOnlyList<GameMarketItem> list)
    {
        for (var index = 0; index < list.Count && ids.Count < 40; index++)
        {
            var id = list[index].Id;
            if (id > 0 && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }
    }

    private string PlaceFor(MarketScopeKind kind)
    {
        var home = HomeWorld();
        if (kind == MarketScopeKind.World)
        {
            return home.Length > 0 ? home : "Zalera";
        }

        var center = DataCenterName(home);
        if (kind == MarketScopeKind.DataCenter)
        {
            return center;
        }

        foreach (var row in market.DataCenters)
        {
            if (string.Equals(row.Name, center, StringComparison.OrdinalIgnoreCase) && row.Region.Length > 0)
            {
                return row.Region;
            }
        }

        return "North-America";
    }

    private string DataCenterName(string home)
    {
        if (home.Length == 0)
        {
            return "Crystal";
        }

        foreach (var center in market.DataCenters)
        {
            foreach (var worldId in center.WorldIds)
            {
                foreach (var world in market.Worlds)
                {
                    if (world.Id == worldId &&
                        string.Equals(world.Name, home, StringComparison.OrdinalIgnoreCase))
                    {
                        return center.Name;
                    }
                }
            }
        }

        return home;
    }

    private string HomeWorld() => game.Character.WorldName.Trim();

    private bool IsHome(string world) =>
        world.Length > 0 && string.Equals(world, HomeWorld(), StringComparison.OrdinalIgnoreCase);

    private bool Watching(int id) => id > 0 && watchIds.Contains(id);

    private void FlipWatch(GameMarketItem item)
    {
        if (item.Id <= 0)
        {
            return;
        }

        if (!watchIds.Remove(item.Id))
        {
            watchIds.Insert(0, item.Id);
            while (watchIds.Count > 24)
            {
                watchIds.RemoveAt(watchIds.Count - 1);
            }
        }

        RebuildWatches();
        SaveBook();
    }

    private void RebuildWatches()
    {
        watches.Clear();
        for (var index = 0; index < watchIds.Count; index++)
        {
            var found = items.Find(watchIds[index]);
            if (found is not null)
            {
                watches.Add(found.Value);
            }
        }
    }

    private List<MarketListing> ShownListings(MarketBoard board)
    {
        var shown = new List<MarketListing>(board.Listings.Count);
        for (var index = 0; index < board.Listings.Count; index++)
        {
            if (Fits(board.Listings[index].Hq))
            {
                shown.Add(board.Listings[index]);
            }
        }

        return shown;
    }

    private List<MarketSale> ShownSales(MarketBoard board)
    {
        var shown = new List<MarketSale>(board.Sales.Count);
        for (var index = 0; index < board.Sales.Count; index++)
        {
            if (Fits(board.Sales[index].Hq))
            {
                shown.Add(board.Sales[index]);
            }
        }

        return shown;
    }

    private bool Fits(bool hq) => quality == 0 || (quality == 2 ? hq : !hq);

    private int Cheapest(MarketBoard board, List<MarketListing> shown)
    {
        var floor = 0;
        for (var index = 0; index < shown.Count; index++)
        {
            var price = shown[index].Price;
            if (price > 0 && (floor == 0 || price < floor))
            {
                floor = price;
            }
        }

        if (floor > 0)
        {
            return floor;
        }

        if (quality == 2)
        {
            return board.MinHq;
        }

        if (quality == 1)
        {
            return board.MinNq;
        }

        if (board.MinNq > 0 && board.MinHq > 0)
        {
            return Math.Min(board.MinNq, board.MinHq);
        }

        return board.MinNq > 0 ? board.MinNq : board.MinHq;
    }

    private void LoadBook()
    {
        if (!AtomicJson.TryRead(bookPath, null, out Book? book, ref corrupt, log) || book is null)
        {
            return;
        }

        recents.Clear();
        FillItems(recents, book.RecentIds);
        watchIds.Clear();
        if (book.WatchIds is { Count: > 0 })
        {
            for (var index = 0; index < book.WatchIds.Count && watchIds.Count < 24; index++)
            {
                var id = book.WatchIds[index];
                if (id > 0 && !watchIds.Contains(id))
                {
                    watchIds.Add(id);
                }
            }
        }

        RebuildWatches();
    }

    private void FillItems(List<GameMarketItem> dest, List<int>? ids)
    {
        if (ids is not { Count: > 0 })
        {
            return;
        }

        for (var index = 0; index < ids.Count; index++)
        {
            var found = items.Find(ids[index]);
            if (found is not null)
            {
                dest.Add(found.Value);
            }
        }
    }

    private void SaveBook()
    {
        AtomicJson.TrySave(bookPath, new Book
        {
            RecentIds = recents.ConvertAll(row => row.Id),
            WatchIds = [.. watchIds],
        }, null, ref corrupt, log);
    }

    private static string GilAverage(double value) =>
        value <= 0 ? "—" : ((int)Math.Round(value)).ToString("N0", CultureInfo.InvariantCulture) + " gil";

    private static string CountMark(int value) =>
        value <= 0 ? "—" : value.ToString("N0", CultureInfo.InvariantCulture);

    private sealed class Book
    {
        public List<int>? RecentIds { get; set; }

        public List<int>? WatchIds { get; set; }
    }
}
