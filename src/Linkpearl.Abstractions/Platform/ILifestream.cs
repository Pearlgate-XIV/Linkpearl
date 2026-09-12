namespace Linkpearl.Platform;

public interface ILifestream
{
    bool Ready { get; }

    uint NearestAetheryte(uint territoryId);

    bool TryTeleport(uint aetheryteId);

    bool TryGoPlace(string place);

    bool TryGoHome(string world, string district, int ward, int plot, int apartment, bool subdivision);
}
