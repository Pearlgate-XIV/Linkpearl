namespace Linkpearl.Platform;

public readonly struct CharacterIdentity : IEquatable<CharacterIdentity>
{
    public readonly ulong ContentId;
    public readonly string Name;
    public readonly string WorldName;
    public readonly uint HomeWorldId;
    public readonly uint CurrentWorldId;

    public CharacterIdentity(ulong contentId, string name, string worldName, uint homeWorldId, uint currentWorldId)
    {
        ContentId = contentId;
        Name = name;
        WorldName = worldName;
        HomeWorldId = homeWorldId;
        CurrentWorldId = currentWorldId;
    }

    public static CharacterIdentity Unknown => new(0UL, string.Empty, string.Empty, 0u, 0u);

    public bool IsKnown => ContentId != 0UL;

    public bool Equals(CharacterIdentity other) => ContentId == other.ContentId;

    public override bool Equals(object? candidate) => candidate is CharacterIdentity other && Equals(other);

    public override int GetHashCode() => ContentId.GetHashCode();

    public static bool operator ==(CharacterIdentity left, CharacterIdentity right) => left.Equals(right);

    public static bool operator !=(CharacterIdentity left, CharacterIdentity right) => !left.Equals(right);
}

public interface IGameSession
{
    bool IsLoggedIn { get; }

    CharacterIdentity Character { get; }

    uint TerritoryId { get; }

    string JobName { get; }

    string ZoneName { get; }

    string WeatherName { get; }

    uint Gil { get; }

    int PartySize { get; }

    bool IsInCombat { get; }

    bool IsInDuty { get; }

    bool IsInParty { get; }

    bool IsInCutscene { get; }

    bool IsInGpose { get; }

    bool IsOccupied { get; }

    event Action? LoggedIn;

    event Action? LoggedOut;

    event Action<CharacterIdentity>? CharacterChanged;

    event Action<uint>? TerritoryChanged;
}
