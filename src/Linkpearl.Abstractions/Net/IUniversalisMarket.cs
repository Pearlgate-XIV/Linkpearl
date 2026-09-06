namespace Linkpearl.Net;

public enum MarketScopeKind : byte
{
    World = 0,
    DataCenter = 1,
    Region = 2,
}

public readonly record struct MarketWorld(int Id, string Name);

public readonly record struct MarketDataCenter(string Name, string Region, IReadOnlyList<int> WorldIds);

public readonly record struct MarketHit(int ItemId, string Name, uint IconId = 0);

public readonly record struct MarketListing(
    int Price,
    int Quantity,
    int Total,
    int Tax,
    bool Hq,
    string World,
    string Retainer,
    long ReviewedUnix);

public readonly record struct MarketSale(
    int Price,
    int Quantity,
    int Total,
    bool Hq,
    string World,
    string Buyer,
    long SoldUnix);

public readonly record struct MarketTaxRate(string City, int Percent);

public sealed class MarketBoard
{
    public int ItemId { get; init; }

    public uint IconId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public bool HasData { get; init; }

    public int MinNq { get; init; }

    public int MinHq { get; init; }

    public int MaxNq { get; init; }

    public int MaxHq { get; init; }

    public double Average { get; init; }

    public double CurrentAverage { get; init; }

    public int UnitsForSale { get; init; }

    public int ListingsCount { get; init; }

    public int UnitsSold { get; init; }

    public long LastUploadUnixMs { get; init; }

    public string Notice { get; init; } = string.Empty;

    public IReadOnlyList<MarketListing> Listings { get; init; } = [];

    public IReadOnlyList<MarketSale> Sales { get; init; } = [];

    public IReadOnlyList<MarketTaxRate> Taxes { get; init; } = [];
}

public interface IUniversalisMarket : IDisposable
{
    IReadOnlyList<MarketWorld> Worlds { get; }

    IReadOnlyList<MarketDataCenter> DataCenters { get; }

    IReadOnlyList<MarketHit> Hits { get; }

    MarketBoard Board { get; }

    IReadOnlyDictionary<int, int> Floors { get; }

    bool Busy { get; }

    string Notice { get; }

    void RefreshMaps();

    void Search(string query);

    void Peek(IReadOnlyList<int> itemIds, string scope);

    void OpenItem(int itemId, string name, MarketScopeKind kind, string scope);
}
