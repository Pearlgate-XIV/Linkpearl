using Linkpearl.Media;
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
        Name = name ?? string.Empty;
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

public readonly struct JobFace
{
    public readonly uint Id;
    public readonly string Name;
    public readonly string Abbreviation;
    public readonly uint IconId;
    public readonly byte Role;

    public JobFace(uint id, string name, string abbreviation, uint iconId, byte role)
    {
        Id = id;
        Name = name ?? string.Empty;
        Abbreviation = abbreviation ?? string.Empty;
        IconId = iconId;
        Role = role;
    }
}

public interface IJobCatalog
{
    IReadOnlyList<JobFace> All { get; }

    bool TryGet(uint id, out JobFace job);

    bool TryGetByName(string name, out JobFace job);

    uint IconFor(uint jobId);
}

public static class JobIconIds
{
    public const uint SheetBase = 62000;

    public static uint FromJob(uint jobId) => jobId == 0 ? 0u : SheetBase + jobId;
}

public interface ITextureSource
{
    ITextureHandle? GameIcon(uint iconId, bool highResolution = true);

    ITextureHandle? FromFile(string path);

    ITextureHandle? FromFile(string path, Vector2 destPixels);

    ITextureHandle? FromBytes(ReadOnlySpan<byte> data, string cacheKey);

    CoverUv FileOpaqueUv(string path) => CoverUv.Full;

    void ForgetFile(string path)
    {
    }
}

public interface IFilePicker
{
    bool Picking { get; }

    void BeginImagePick();

    bool TryTakeImages(out IReadOnlyList<string> paths);
}
