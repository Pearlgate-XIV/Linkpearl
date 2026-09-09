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

public readonly record struct GameRetainer(
    int Slot,
    string Name,
    uint Gil,
    int ItemsOnSale,
    long VentureUntilUnix);

public readonly record struct GameCurrency(
    string Group,
    uint ItemId,
    string Name,
    uint IconId,
    uint Held,
    uint Cap,
    uint WeeklyHeld,
    uint WeeklyCap);

public interface IGameSession
{
    bool IsLoggedIn { get; }

    CharacterIdentity Character { get; }

    uint TerritoryId { get; }

    uint JobId { get; }

    uint JobIconId { get; }

    string JobName { get; }

    string RaceName { get; }

    byte RaceId { get; }

    int PhoneCountry { get; }

    string ZoneName { get; }

    string MapPlace { get; }

    Vector2 MapCoords { get; }

    string WeatherName { get; }

    uint Gil { get; }

    IReadOnlyList<GameCurrency> Currencies { get; }

    int PartySize { get; }

    bool IsInCombat { get; }

    bool IsInDuty { get; }

    bool IsInParty { get; }

    bool IsInCutscene { get; }

    bool IsInGpose { get; }

    bool IsOccupied { get; }

    IReadOnlyList<GameRetainer> Retainers { get; }

    bool RetainersReady { get; }

    string ItemName(uint itemId);

    void OpenGroupPose();

    void CuePocket();

    bool TakePocketCue();

    event Action? LoggedIn;

    event Action? LoggedOut;

    event Action<CharacterIdentity>? CharacterChanged;

    event Action<uint>? TerritoryChanged;
}
