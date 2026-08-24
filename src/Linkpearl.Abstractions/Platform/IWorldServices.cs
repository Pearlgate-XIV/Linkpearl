using Linkpearl.Painting;

namespace Linkpearl.Platform;

public readonly struct WeatherWindow
{
    public readonly string Name;
    public readonly uint IconId;
    public readonly DateTimeOffset Starts;
    public readonly DateTimeOffset Ends;

    public WeatherWindow(string name, uint iconId, DateTimeOffset starts, DateTimeOffset ends)
    {
        Name = name;
        IconId = iconId;
        Starts = starts;
        Ends = ends;
    }
}

public interface IWeatherOracle
{
    WeatherWindow Current(ushort territoryId);

    IReadOnlyList<WeatherWindow> Forecast(ushort territoryId, int windows);
}

public interface IEorzeaClock
{
    TimeSpan BellTime { get; }

    int SunCount { get; }
}

public interface IMapPilot
{
    string ZoneName(ushort territoryId);

    bool TryPlaceFlag(ushort territoryId, Vector2 mapCoordinates);

    bool TryTeleport(uint aetheryteId);

    IReadOnlyList<AetheryteEntry> Aetherytes { get; }
}

public readonly struct AetheryteEntry
{
    public readonly uint Id;
    public readonly string Name;
    public readonly ushort TerritoryId;
    public readonly uint GilCost;

    public AetheryteEntry(uint id, string name, ushort territoryId, uint gilCost)
    {
        Id = id;
        Name = name;
        TerritoryId = territoryId;
        GilCost = gilCost;
    }
}

public interface ITextureSource
{
    ITextureHandle? GameIcon(uint iconId, bool highResolution = true);

    ITextureHandle? FromFile(string path);

    ITextureHandle? FromBytes(ReadOnlySpan<byte> data, string cacheKey);
}
