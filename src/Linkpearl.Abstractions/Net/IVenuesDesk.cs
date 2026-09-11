namespace Linkpearl.Net;

public readonly record struct VenueHours(DateTimeOffset Start, DateTimeOffset End, bool OpenNow);

public sealed class VenueSpot
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string BannerUrl { get; init; } = string.Empty;

    public string DataCenter { get; init; } = string.Empty;

    public string World { get; init; } = string.Empty;

    public string District { get; init; } = string.Empty;

    public int Ward { get; init; }

    public int Plot { get; init; }

    public int Apartment { get; init; }

    public int Room { get; init; }

    public bool Subdivision { get; init; }

    public string Address { get; init; } = string.Empty;

    public bool CanTeleport =>
        World.Length > 0 && District.Length > 0 && Ward > 0 && (Plot > 0 || Apartment > 0);

    public string Description { get; init; } = string.Empty;

    public string Website { get; init; } = string.Empty;

    public string Discord { get; init; } = string.Empty;

    public string DirectoryUrl { get; init; } = string.Empty;

    public bool Sfw { get; init; }

    public bool OpenHouse { get; init; }

    public bool OpenNow { get; init; }

    public DateTimeOffset? OpenUntil { get; init; }

    public DateTimeOffset? NextOpen { get; init; }

    public string HoursLine { get; init; } = string.Empty;

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<VenueWeekMark> Week { get; init; } = [];
}

public readonly record struct VenueWeekMark(
    DayOfWeek Day,
    int StartHour,
    int StartMinute,
    bool StartNextDay,
    int EndHour,
    int EndMinute,
    bool EndNextDay,
    string TimeZone,
    int Interval,
    DateTimeOffset? Commencing);

public interface IVenuesDesk
{
    IReadOnlyList<VenueSpot> Spots { get; }

    IReadOnlyList<string> DataCenters { get; }

    bool Busy { get; }

    string Notice { get; }

    DateTimeOffset? FetchedAt { get; }

    int Revision { get; }

    void Refresh(bool force = false);

    VenueSpot? Find(string id);

    void PrefetchBanner(string url);

    string? BannerPath(string url);
}
