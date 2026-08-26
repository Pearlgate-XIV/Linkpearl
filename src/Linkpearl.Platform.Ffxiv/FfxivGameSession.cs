using Dalamud.Plugin.Services;
using Linkpearl.Platform;

namespace Linkpearl.Platform.Ffxiv;

public sealed class FfxivGameSession : IGameSession, IDisposable
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly ICondition condition;
    private readonly IDutyState dutyState;
    private readonly IFramework framework;
    private CharacterIdentity character = CharacterIdentity.Unknown;

    public FfxivGameSession(IClientState clientState, IObjectTable objectTable, ICondition condition,
        IDutyState dutyState, IFramework framework)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.condition = condition;
        this.dutyState = dutyState;
        this.framework = framework;
        clientState.Login += HandleLogin;
        clientState.Logout += HandleLogout;
        clientState.TerritoryChanged += HandleTerritoryChanged;

        // Dalamud constructs plugins off the main thread, but IObjectTable.LocalPlayer (like
        // most game-state reads) is only safe to touch from it. A player already logged in when
        // the plugin loads has no Login event left to catch, so the initial read is deferred to
        // the first framework tick — guaranteed main-thread — instead of done here.
        framework.Update += HandleFirstUpdate;
    }

    public event Action? LoggedIn;

    public event Action? LoggedOut;

    public event Action<CharacterIdentity>? CharacterChanged;

    public event Action<uint>? TerritoryChanged;

    public bool IsLoggedIn => clientState.IsLoggedIn;

    public CharacterIdentity Character => character;

    public uint TerritoryId => clientState.TerritoryType;

    public bool IsInCombat => condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat];

    public bool IsInDuty => dutyState.IsDutyStarted;

    public bool IsInCutscene =>
        condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene] ||
        condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene78] ||
        condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInCutSceneEvent];

    public bool IsInGpose => condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene78];

    public bool IsOccupied => condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied] ||
        condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied30] ||
        condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied33] ||
        condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied38] ||
        condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied39];

    public void Dispose()
    {
        clientState.Login -= HandleLogin;
        clientState.Logout -= HandleLogout;
        clientState.TerritoryChanged -= HandleTerritoryChanged;
        framework.Update -= HandleFirstUpdate;
    }

    private void HandleFirstUpdate(IFramework runningFramework)
    {
        framework.Update -= HandleFirstUpdate;
        if (clientState.IsLoggedIn)
        {
            character = ReadCharacter();
        }
    }

    private void HandleLogin()
    {
        character = ReadCharacter();
        LoggedIn?.Invoke();
        CharacterChanged?.Invoke(character);
    }

    private void HandleLogout(int type, int code)
    {
        character = CharacterIdentity.Unknown;
        LoggedOut?.Invoke();
    }

    private void HandleTerritoryChanged(uint territoryId) => TerritoryChanged?.Invoke(territoryId);

    private CharacterIdentity ReadCharacter()
    {
        var localPlayer = objectTable.LocalPlayer;
        if (localPlayer is null)
        {
            return CharacterIdentity.Unknown;
        }

        return new CharacterIdentity(localPlayer.GameObjectId, localPlayer.Name.TextValue,
            localPlayer.HomeWorld.RowId, localPlayer.CurrentWorld.RowId);
    }
}
