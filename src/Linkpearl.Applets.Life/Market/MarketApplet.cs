using System.Globalization;
using System.Text.Json;
using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Applets.Life.Market;

public sealed class MarketApplet : IApplet
{
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
    private readonly string bookPath;
    private readonly List<GameMarketItem> recents = [];
    private IReadOnlyList<GameMarketItem> hits = [];
    private string query = string.Empty;
    private string lastQuery = string.Empty;
    private string peeked = string.Empty;
    private bool itemOpen;
    private uint itemIcon;
    private float scroll;

    public MarketApplet(IUniversalisMarket market, IGameItems items, IGameSession game, HostPaths paths)
    {
        this.market = market;
        this.items = items;
        this.game = game;
        bookPath = paths.State("market.json");
        LoadBook();
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public string Place => itemOpen ? "item" : "browse";

    public void Enter(AppletEntry entry)
    {
        market.RefreshMaps();
        scroll = 0f;
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
        }

        if (hits.Count > 0 && peeked.Length == 0)
        {
            peeked = string.Join(',', hits.Select(row => row.Id));
            market.Peek(hits.Select(row => row.Id).ToArray(), PlaceName());
        }

        var body = frame.Content.Inset(new Edges(frame.Units(14f), frame.Units(10f), frame.Units(14f),
            frame.Units(10f)));
        frame.Paint.PushClip(body);
        var shifted = body.Translate(new Vector2(0f, -scroll));
        var height = itemOpen ? DrawItem(frame, shifted) : DrawBrowse(frame, shifted);
        frame.Paint.PopClip();
        if (frame.Input.IsHovering(body) && frame.Input.ScrollDelta != 0f)
        {
            scroll = Math.Clamp(scroll - frame.Input.ScrollDelta * frame.Units(22f), 0f,
                MathF.Max(0f, height - body.Height));
        }
    }

    private float DrawBrowse(in AppletFrame frame, Rect area)
    {
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        frame.Text.DrawIn(stack.Take(frame.Units(26f)), "Market",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        var search = stack.Take(frame.Units(40f));
        CardChrome.Draw(frame, search);
        query = frame.TextField.Draw("market-query", search.Inset(frame.Units(10f)), query, "Search items");

        var list = hits.Count > 0 ? hits : recents;
        if (query.Trim().Length == 0 && recents.Count > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Recent",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }
        else if (hits.Count == 0 && query.Trim().Length > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(28f)), "No market item matches that name.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        var floors = market.Floors;
        for (var index = 0; index < list.Count; index++)
        {
            DrawHit(frame, stack.Take(frame.Units(56f)), list[index],
                floors.TryGetValue(list[index].Id, out var price) ? price : 0);
        }

        if (query.Trim().Length == 0 && recents.Count == 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(36f)),
                "Search any market item. Prices are for your data center.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        return area.Height - stack.Remaining.Height;
    }

    private float DrawItem(in AppletFrame frame, Rect area)
    {
        var board = market.Board;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        var back = stack.Take(frame.Units(24f));
        frame.Text.DrawIn(back, "‹ Search",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
        if (frame.Input.ConsumeClick(back))
        {
            itemOpen = false;
            scroll = 0f;
            return area.Height;
        }

        var head = stack.Take(frame.Units(72f));
        DrawIcon(frame, head.LeftSlice(frame.Units(64f)), board.IconId > 0 ? board.IconId : itemIcon);
        var copy = head.Inset(new Edges(frame.Units(72f), frame.Units(6f), 0f, 0f));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(22f)),
            board.Name.Length > 0 ? board.Name : "Item",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var current = board.MinNq > 0 ? board.MinNq : board.MinHq;
        frame.Text.DrawIn(copy.BottomSlice(frame.Units(20f)),
            market.Busy && current == 0
                ? "Loading price…"
                : "Current · " + Gil(current),
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));

        frame.Text.DrawIn(stack.Take(frame.Units(16f)),
            "Selling on " + (board.Scope.Length > 0 ? board.Scope : PlaceName()),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        if (!market.Busy && board.Listings.Count == 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(32f)), "Nobody is selling this there right now.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        for (var index = 0; index < board.Listings.Count; index++)
        {
            DrawSale(frame, stack.Take(frame.Units(48f)), board.Listings[index], board.Scope);
        }

        return area.Height - stack.Remaining.Height;
    }

    private void DrawHit(in AppletFrame frame, Rect row, GameMarketItem item, int price)
    {
        CardChrome.Draw(frame, row);
        var inset = row.Inset(frame.Units(6f));
        DrawIcon(frame, inset.LeftSlice(frame.Units(44f)), item.IconId);
        var body = inset.Inset(new Edges(frame.Units(52f), frame.Units(6f), 0f, frame.Units(6f)));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), item.Name,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(body.BottomSlice(frame.Units(16f)),
            price > 0 ? "Current · " + Gil(price) : "Tap for listings",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        if (frame.Input.ConsumeClick(row))
        {
            OpenItem(item);
        }
    }

    private static void DrawSale(in AppletFrame frame, Rect row, MarketListing listing, string scope)
    {
        CardChrome.Draw(frame, row);
        var inset = row.Inset(frame.Units(10f));
        var world = listing.World.Length > 0 ? listing.World : scope;
        frame.Text.DrawEllipsized(inset.TopSlice(frame.Units(18f)),
            Gil(listing.Price) + (listing.Hq ? " HQ" : string.Empty) +
            (listing.Quantity > 1 ? " ×" + listing.Quantity : string.Empty),
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(inset.BottomSlice(frame.Units(14f)),
            (world.Length > 0 ? world : "Unknown world") +
            (listing.Retainer.Length > 0 ? " · " + listing.Retainer : string.Empty),
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
        itemOpen = true;
        itemIcon = item.IconId;
        scroll = 0f;
        market.OpenItem(item.Id, item.Name, MarketScopeKind.DataCenter, PlaceName());
    }

    private string PlaceName()
    {
        var home = game.Character.WorldName.Trim();
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

    private void LoadBook()
    {
        if (!File.Exists(bookPath))
        {
            return;
        }

        try
        {
            var book = JsonSerializer.Deserialize<Book>(File.ReadAllText(bookPath));
            if (book?.RecentIds is not { Count: > 0 })
            {
                return;
            }

            recents.Clear();
            for (var index = 0; index < book.RecentIds.Count; index++)
            {
                var found = items.Find(book.RecentIds[index]);
                if (found is not null)
                {
                    recents.Add(found.Value);
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private void SaveBook()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(bookPath) ?? ".");
            File.WriteAllText(bookPath, JsonSerializer.Serialize(new Book
            {
                RecentIds = recents.ConvertAll(row => row.Id),
            }));
        }
        catch (Exception)
        {
        }
    }

    private static string Gil(int value) =>
        value <= 0 ? "—" : value.ToString("N0", CultureInfo.InvariantCulture) + " gil";

    private sealed class Book
    {
        public List<int>? RecentIds { get; set; }
    }
}
