using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Linkpearl.Net;

namespace Linkpearl.Net.Market;

public sealed class UniversalisMarket : IUniversalisMarket
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient http = new()
    {
        Timeout = TimeSpan.FromSeconds(18),
    };

    private readonly object gate = new();
    private MarketWorld[] worlds = [];
    private MarketDataCenter[] centers = [];
    private MarketHit[] hits = [];
    private MarketBoard board = new();
    private readonly Dictionary<int, int> floors = new();
    private bool busy;
    private string notice = "Search an item. Prices come from Universalis.";
    private int maps;

    public UniversalisMarket()
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Linkpearl/0.1.0");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        RefreshMaps();
    }

    public IReadOnlyList<MarketWorld> Worlds
    {
        get
        {
            lock (gate)
            {
                return worlds;
            }
        }
    }

    public IReadOnlyList<MarketDataCenter> DataCenters
    {
        get
        {
            lock (gate)
            {
                return centers;
            }
        }
    }

    public IReadOnlyList<MarketHit> Hits
    {
        get
        {
            lock (gate)
            {
                return hits;
            }
        }
    }

    public MarketBoard Board
    {
        get
        {
            lock (gate)
            {
                return board;
            }
        }
    }

    public IReadOnlyDictionary<int, int> Floors
    {
        get
        {
            lock (gate)
            {
                return new Dictionary<int, int>(floors);
            }
        }
    }

    public bool Busy
    {
        get
        {
            lock (gate)
            {
                return busy;
            }
        }
    }

    public string Notice
    {
        get
        {
            lock (gate)
            {
                return notice;
            }
        }
    }

    public void RefreshMaps() => _ = Task.Run(LoadMapsAsync);

    public void Search(string query) => _ = Task.Run(() => SearchAsync(query));

    public void Peek(IReadOnlyList<int> itemIds, string scope) =>
        _ = Task.Run(() => PeekAsync(itemIds, scope));

    public void OpenItem(int itemId, string name, MarketScopeKind kind, string scope) =>
        _ = Task.Run(() => OpenItemAsync(itemId, name, kind, scope));

    public void Dispose() => http.Dispose();

    private async Task LoadMapsAsync()
    {
        if (Interlocked.Exchange(ref maps, 1) == 1 && worlds.Length > 0)
        {
            return;
        }

        try
        {
            var worldBody = await GetJson<List<WorldDto>>("https://universalis.app/api/v2/worlds")
                .ConfigureAwait(false);
            var dcBody = await GetJson<List<CenterDto>>("https://universalis.app/api/v2/data-centers")
                .ConfigureAwait(false);
            lock (gate)
            {
                worlds = (worldBody ?? [])
                    .Where(row => row.Name is { Length: > 0 } && row.Name[0] < 128 && !row.Name.StartsWith("Cloud",
                        StringComparison.OrdinalIgnoreCase))
                    .Select(row => new MarketWorld(row.Id, row.Name ?? string.Empty))
                    .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                centers = (dcBody ?? [])
                    .Where(row => row.Name is { Length: > 0 } && row.Name[0] < 128 &&
                                  !row.Name.Contains("Cloud", StringComparison.OrdinalIgnoreCase))
                    .Select(row => new MarketDataCenter(row.Name ?? string.Empty, row.Region ?? string.Empty, row.Worlds ?? []))
                    .ToArray();
                if (notice.StartsWith("Search", StringComparison.Ordinal))
                {
                    notice = worlds.Length + " worlds · " + centers.Length + " data centers from Universalis.";
                }
            }
        }
        catch (Exception)
        {
            Interlocked.Exchange(ref maps, 0);
            lock (gate)
            {
                notice = "Could not load Universalis worlds. Try again in a moment.";
            }
        }
    }

    private async Task SearchAsync(string query)
    {
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            lock (gate)
            {
                hits = [];
                notice = "Type an item name or item ID.";
            }

            return;
        }

        lock (gate)
        {
            busy = true;
            notice = "Searching XIVAPI…";
        }

        try
        {
            if (int.TryParse(text, out var id) && id > 0)
            {
                var named = await ItemNameAsync(id).ConfigureAwait(false);
                lock (gate)
                {
                    hits = [new MarketHit(id, named.Length > 0 ? named : "Item " + id)];
                    notice = "Opened item ID " + id + ".";
                    busy = false;
                }

                return;
            }

            var escaped = text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", string.Empty,
                StringComparison.Ordinal);
            var url = "https://v2.xivapi.com/api/search?sheets=Item&limit=24&query=" +
                      Uri.EscapeDataString("Name~\"" + escaped + "\"");
            var body = await GetJson<SearchDto>(url).ConfigureAwait(false);
            var found = new List<MarketHit>();
            var rows = body?.Results ?? [];
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var name = row.Fields?.Name ?? string.Empty;
                if (row.RowId <= 0 || name.Length == 0)
                {
                    continue;
                }

                found.Add(new MarketHit(row.RowId, name));
            }

            lock (gate)
            {
                hits = found.ToArray();
                notice = found.Count == 0
                    ? "No items named like that."
                    : found.Count + " items. Tap one for Universalis prices.";
                busy = false;
            }
        }
        catch (Exception)
        {
            lock (gate)
            {
                hits = [];
                notice = "Item search failed. Check the network and try again.";
                busy = false;
            }
        }
    }

    private async Task PeekAsync(IReadOnlyList<int> itemIds, string scope)
    {
        if (itemIds.Count == 0)
        {
            return;
        }

        var place = (scope ?? string.Empty).Trim();
        if (place.Length == 0)
        {
            place = "Crystal";
        }

        var ids = string.Join(',', itemIds.Take(40));
        MarketWorld[] worldSnap;
        MarketDataCenter[] centerSnap;
        lock (gate)
        {
            worldSnap = worlds;
            centerSnap = centers;
        }

        try
        {
            var body = await GetJson<AggDto>("https://universalis.app/api/v2/aggregated/" +
                                             Uri.EscapeDataString(place) + "/" + ids)
                .ConfigureAwait(false);
            lock (gate)
            {
                foreach (var row in body?.Results ?? [])
                {
                    var price = FloorOf(row.Nq, place, worldSnap, centerSnap);
                    if (price <= 0)
                    {
                        price = FloorOf(row.Hq, place, worldSnap, centerSnap);
                    }
                    if (price > 0)
                    {
                        floors[row.ItemId] = price;
                    }
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private static int FloorOf(AggQuality? quality, string place, IReadOnlyList<MarketWorld> worlds,
        IReadOnlyList<MarketDataCenter> centers)
    {
        var listing = quality?.MinListing;
        if (listing is null)
        {
            return 0;
        }

        var world = listing.World?.Price ?? 0;
        var dc = listing.Dc?.Price ?? 0;
        var region = listing.Region?.Price ?? 0;
        if (world > 0 && worlds.Any(row => string.Equals(row.Name, place, StringComparison.OrdinalIgnoreCase)))
        {
            return world;
        }

        if (dc > 0 && centers.Any(row => string.Equals(row.Name, place, StringComparison.OrdinalIgnoreCase)))
        {
            return dc;
        }

        if (region > 0 &&
            centers.Any(row => string.Equals(row.Region, place, StringComparison.OrdinalIgnoreCase)))
        {
            return region;
        }

        if (world > 0)
        {
            return world;
        }

        if (dc > 0)
        {
            return dc;
        }

        return region;
    }

    private async Task OpenItemAsync(int itemId, string name, MarketScopeKind kind, string scope)
    {
        var place = ScopeName(kind, scope);
        lock (gate)
        {
            busy = true;
            notice = "Loading Universalis for " + place + "…";
        }

        try
        {
            if (name.Length == 0)
            {
                name = await ItemNameAsync(itemId).ConfigureAwait(false);
            }

            var currentUrl = "https://universalis.app/api/v2/" + Uri.EscapeDataString(place) + "/" + itemId +
                             "?listings=48&entries=20";
            var shown = await GetJson<ShownDto>(currentUrl).ConfigureAwait(false);
            MarketTaxRate[] taxes = [];
            if (kind == MarketScopeKind.World && place.Length > 0)
            {
                var tax = await GetJson<Dictionary<string, int>>(
                        "https://universalis.app/api/v2/tax-rates?world=" + Uri.EscapeDataString(place))
                    .ConfigureAwait(false);
                if (tax is { Count: > 0 })
                {
                    taxes = tax.Select(row => new MarketTaxRate(row.Key, row.Value)).ToArray();
                }
            }

            lock (gate)
            {
                board = MapBoard(itemId, name.Length > 0 ? name : "Item " + itemId, place, shown, taxes);
                notice = board.HasData
                    ? "Cheapest " + Gil(board.MinNq > 0 ? board.MinNq : board.MinHq) + " on " + place + "."
                    : "Universalis has no listings for this item on " + place + ".";
                busy = false;
            }
        }
        catch (Exception)
        {
            lock (gate)
            {
                board = new MarketBoard
                {
                    ItemId = itemId,
                    Name = name.Length > 0 ? name : "Item " + itemId,
                    Scope = place,
                    Notice = "Universalis did not return this item.",
                };
                notice = "Could not reach Universalis.";
                busy = false;
            }
        }
    }

    private async Task<string> ItemNameAsync(int itemId)
    {
        try
        {
            var body = await GetJson<SheetDto>("https://v2.xivapi.com/api/sheet/Item/" + itemId + "?fields=Name")
                .ConfigureAwait(false);
            return body?.Fields?.Name ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private async Task<T?> GetJson<T>(string url)
    {
        using var response = await http.GetAsync(url).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return default;
        }

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, Json).ConfigureAwait(false);
    }

    private static MarketBoard MapBoard(int itemId, string name, string scope, ShownDto? shown,
        IReadOnlyList<MarketTaxRate> taxes)
    {
        if (shown is null)
        {
            return new MarketBoard { ItemId = itemId, Name = name, Scope = scope };
        }

        var listings = (shown.Listings ?? [])
            .Select(row => new MarketListing(
                row.PricePerUnit,
                row.Quantity,
                row.Total > 0 ? row.Total : row.PricePerUnit * row.Quantity,
                row.Tax,
                row.Hq,
                row.WorldName ?? string.Empty,
                row.RetainerName ?? string.Empty,
                row.LastReviewTime))
            .ToArray();
        var sales = (shown.RecentHistory ?? [])
            .Select(row => new MarketSale(
                row.PricePerUnit,
                row.Quantity,
                row.Total > 0 ? row.Total : row.PricePerUnit * row.Quantity,
                row.Hq,
                row.WorldName ?? string.Empty,
                row.BuyerName ?? string.Empty,
                row.Timestamp))
            .ToArray();
        return new MarketBoard
        {
            ItemId = itemId,
            Name = name,
            Scope = scope,
            HasData = shown.HasData || listings.Length > 0,
            MinNq = shown.MinPriceNq,
            MinHq = shown.MinPriceHq,
            MaxNq = shown.MaxPriceNq,
            MaxHq = shown.MaxPriceHq,
            Average = shown.AveragePrice,
            CurrentAverage = shown.CurrentAveragePrice,
            UnitsForSale = shown.UnitsForSale,
            ListingsCount = shown.ListingsCount > 0 ? shown.ListingsCount : listings.Length,
            UnitsSold = shown.UnitsSold,
            LastUploadUnixMs = shown.LastUploadTime,
            Listings = listings,
            Sales = sales,
            Taxes = taxes,
        };
    }

    private static string ScopeName(MarketScopeKind kind, string scope)
    {
        var text = (scope ?? string.Empty).Trim();
        if (text.Length > 0)
        {
            return text;
        }

        return kind switch
        {
            MarketScopeKind.DataCenter => "Crystal",
            MarketScopeKind.Region => "North-America",
            _ => "Zalera",
        };
    }

    private static string Gil(int value) => value.ToString("N0", CultureInfo.InvariantCulture) + "g";

    private sealed class WorldDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    private sealed class CenterDto
    {
        public string? Name { get; set; }

        public string? Region { get; set; }

        public int[]? Worlds { get; set; }
    }

    private sealed class SearchDto
    {
        public List<SearchRowDto>? Results { get; set; }
    }

    private sealed class SearchRowDto
    {
        [JsonPropertyName("row_id")]
        public int RowId { get; set; }

        public SearchFieldsDto? Fields { get; set; }
    }

    private sealed class SearchFieldsDto
    {
        public string? Name { get; set; }
    }

    private sealed class SheetDto
    {
        public SearchFieldsDto? Fields { get; set; }
    }

    private sealed class ShownDto
    {
        public bool HasData { get; set; }

        public long LastUploadTime { get; set; }

        [JsonPropertyName("minPriceNQ")]
        public int MinPriceNq { get; set; }

        [JsonPropertyName("minPriceHQ")]
        public int MinPriceHq { get; set; }

        [JsonPropertyName("maxPriceNQ")]
        public int MaxPriceNq { get; set; }

        [JsonPropertyName("maxPriceHQ")]
        public int MaxPriceHq { get; set; }

        public double AveragePrice { get; set; }

        public double CurrentAveragePrice { get; set; }

        public int UnitsForSale { get; set; }

        public int ListingsCount { get; set; }

        public int UnitsSold { get; set; }

        public List<ListingDto>? Listings { get; set; }

        public List<SaleDto>? RecentHistory { get; set; }
    }

    private sealed class ListingDto
    {
        public int PricePerUnit { get; set; }

        public int Quantity { get; set; }

        public int Total { get; set; }

        public int Tax { get; set; }

        public bool Hq { get; set; }

        public string? WorldName { get; set; }

        public string? RetainerName { get; set; }

        public long LastReviewTime { get; set; }
    }

    private sealed class SaleDto
    {
        public int PricePerUnit { get; set; }

        public int Quantity { get; set; }

        public int Total { get; set; }

        public bool Hq { get; set; }

        public string? WorldName { get; set; }

        public string? BuyerName { get; set; }

        public long Timestamp { get; set; }
    }

    private sealed class AggDto
    {
        public List<AggRowDto>? Results { get; set; }
    }

    private sealed class AggRowDto
    {
        public int ItemId { get; set; }

        public AggQuality? Nq { get; set; }

        public AggQuality? Hq { get; set; }
    }

    private sealed class AggQuality
    {
        public AggMin? MinListing { get; set; }
    }

    private sealed class AggMin
    {
        public AggPrice? World { get; set; }

        public AggPrice? Dc { get; set; }

        public AggPrice? Region { get; set; }
    }

    private sealed class AggPrice
    {
        public int Price { get; set; }
    }
}
