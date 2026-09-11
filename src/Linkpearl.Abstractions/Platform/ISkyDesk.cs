using Linkpearl.Time;

namespace Linkpearl.Platform;

public readonly record struct SkyChoice(byte Id, string Name, uint IconId);

public readonly record struct SkyLook(EorzeaTime Bells, string Name, uint IconId, byte WeatherId);

public interface ISkyDesk
{
    bool Ready { get; }

    bool TimeLocked { get; }

    bool WeatherLocked { get; }

    int LockedMinute { get; }

    byte LockedWeather { get; }

    bool CompanionLoaded { get; }

    SkyLook Look(ushort territoryId);

    IReadOnlyList<SkyChoice> ZoneChoices(ushort territoryId);

    bool TryLockTime(int minuteOfDay);

    bool TryLockWeather(byte weatherId);

    void UnlockWeather();

    void UnlockTime();

    void Reset();
}
