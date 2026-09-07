namespace Linkpearl.Platform;

public interface ILifestream
{
    bool Ready { get; }

    uint NearestAetheryte(uint territoryId);

    bool TryTeleport(uint aetheryteId);
}
