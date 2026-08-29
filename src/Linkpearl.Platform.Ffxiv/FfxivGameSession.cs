using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Linkpearl.Platform;
using ClassJobSheet = Lumina.Excel.Sheets.ClassJob;
using TerritorySheet = Lumina.Excel.Sheets.TerritoryType;
using WeatherSheet = Lumina.Excel.Sheets.Weather;
using WorldSheet = Lumina.Excel.Sheets.World;

namespace Linkpearl.Platform.Ffxiv;

public sealed class FfxivGameSession : IGameSession, IDisposable
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly ICondition condition;
    private readonly IDutyState dutyState;
    private readonly IPartyList party;
    private readonly IFramework framework;
    private readonly IDataManager data;
    private CharacterIdentity character = CharacterIdentity.Unknown;
    private uint cachedJobId;
    private uint cachedTerritoryId;
    private byte cachedWeatherId;
    private string jobName = string.Empty;
    private string zoneName = string.Empty;
    private string weatherName = string.Empty;

    public FfxivGameSession(IClientState clientState, IObjectTable objectTable, ICondition condition,
        IDutyState dutyState, IPartyList party, IFramework framework, IDataManager data)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.condition = condition;
        this.dutyState = dutyState;
        this.party = party;
        this.framework = framework;
        this.data = data;
        clientState.Login += HandleLogin;
        clientState.Logout += HandleLogout;
        clientState.TerritoryChanged += HandleTerritoryChanged;

        // Dalamud constructs plugins off the main thread, but IObjectTable.LocalPlayer (like
        // most game-state reads) is only safe to touch from it. A player already logged in when
        // the plugin loads has no Login event left to catch, so the initial read is deferred to
        // the first framework tick — guaranteed main-thread — instead of done here.
        framework.Update += HandleUpdate;
    }

    public event Action? LoggedIn;

    public event Action? LoggedOut;

    public event Action<CharacterIdentity>? CharacterChanged;

    public event Action<uint>? TerritoryChanged;

    public bool IsLoggedIn => clientState.IsLoggedIn;

    public CharacterIdentity Character => character;

    public uint TerritoryId => clientState.TerritoryType;

    public string JobName => jobName;

    public string ZoneName => zoneName;

    public string WeatherName => weatherName;

    public uint Gil => ReadGil();

    public int PartySize => party.Length;

    public bool IsInCombat => condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat];

    public bool IsInDuty => dutyState.IsDutyStarted;

    public bool IsInParty => party.Length > 0;

    public bool IsInCutscene =>
        condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene] ||
        condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene78] ||
        condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInCutSceneEvent];

    public bool IsInGpose => clientState.IsGPosing;

    public bool IsOccupied => condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied] ||
        condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied30] ||
        condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied33] ||
        condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied38] ||
        condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied39];

    private static unsafe uint ReadGil()
    {
        var inventory = InventoryManager.Instance();
        return inventory is null ? 0u : inventory->GetGil();
    }

    public void Dispose()
    {
        clientState.Login -= HandleLogin;
        clientState.Logout -= HandleLogout;
        clientState.TerritoryChanged -= HandleTerritoryChanged;
        framework.Update -= HandleUpdate;
    }

    private void HandleUpdate(IFramework runningFramework)
    {
        if (!clientState.IsLoggedIn)
        {
            cachedJobId = 0;
            cachedTerritoryId = 0;
            jobName = string.Empty;
            zoneName = string.Empty;
            weatherName = string.Empty;
            cachedWeatherId = 0;
            return;
        }

        var next = ReadCharacter();
        if (next.ContentId != character.ContentId ||
            !string.Equals(next.Name, character.Name, StringComparison.Ordinal) ||
            next.HomeWorldId != character.HomeWorldId)
        {
            character = next;
            if (next.ContentId != 0UL)
            {
                CharacterChanged?.Invoke(character);
            }
        }
        else if (next.ContentId != 0UL)
        {
            character = next;
        }

        RefreshLabels();
    }

    private void HandleLogin()
    {
        character = ReadCharacter();
        RefreshLabels();
        LoggedIn?.Invoke();
        CharacterChanged?.Invoke(character);
    }

    private void HandleLogout(int type, int code)
    {
        character = CharacterIdentity.Unknown;
        cachedJobId = 0;
        cachedTerritoryId = 0;
        jobName = string.Empty;
        zoneName = string.Empty;
        weatherName = string.Empty;
        cachedWeatherId = 0;
        LoggedOut?.Invoke();
    }

    private void HandleTerritoryChanged(uint territoryId)
    {
        RememberZone(territoryId);
        TerritoryChanged?.Invoke(territoryId);
    }

    private void RefreshLabels()
    {
        var localPlayer = objectTable.LocalPlayer;
        if (localPlayer is not null)
        {
            var jobId = localPlayer.ClassJob.RowId;
            if (jobId != cachedJobId)
            {
                cachedJobId = jobId;
                jobName = JobTitle(jobId);
            }
        }

        var territoryId = clientState.TerritoryType;
        if (territoryId != cachedTerritoryId)
        {
            RememberZone(territoryId);
        }

        RememberWeather(territoryId);
    }

    private void RememberZone(uint territoryId)
    {
        cachedTerritoryId = territoryId;
        zoneName = PlaceName(territoryId);
    }

    private CharacterIdentity ReadCharacter()
    {
        var localPlayer = objectTable.LocalPlayer;
        var contentId = ReadContentId();
        if (contentId == 0UL)
        {
            contentId = character.ContentId;
        }

        if (contentId == 0UL && localPlayer is null)
        {
            return CharacterIdentity.Unknown;
        }

        var name = localPlayer is not null ? localPlayer.Name.TextValue : character.Name;
        var homeWorld = localPlayer is not null ? localPlayer.HomeWorld.RowId : character.HomeWorldId;
        var currentWorld = localPlayer is not null ? localPlayer.CurrentWorld.RowId : character.CurrentWorldId;
        if (contentId == 0UL)
        {
            return CharacterIdentity.Unknown;
        }

        return new CharacterIdentity(contentId, name, WorldName(homeWorld), homeWorld, currentWorld);
    }

    private static unsafe ulong ReadContentId()
    {
        var info = InfoModule.Instance();
        return info is null ? 0UL : info->GetLocalContentId();
    }

    private string WorldName(uint rowId)
    {
        if (rowId != 0 && data.GetExcelSheet<WorldSheet>().TryGetRow(rowId, out var world))
        {
            return world.Name.ExtractText();
        }

        return string.Empty;
    }

    private string JobTitle(uint rowId)
    {
        if (rowId != 0 && data.GetExcelSheet<ClassJobSheet>().TryGetRow(rowId, out var job))
        {
            return job.Name.ExtractText();
        }

        return string.Empty;
    }

    private string PlaceName(uint rowId)
    {
        if (rowId != 0 && data.GetExcelSheet<TerritorySheet>().TryGetRow(rowId, out var territory))
        {
            var place = territory.PlaceName.ValueNullable;
            if (place is { } named)
            {
                return named.Name.ExtractText();
            }
        }

        return string.Empty;
    }

    private void RememberWeather(uint territoryId)
    {
        var weatherId = ReadWeatherId(territoryId);
        if (weatherId == cachedWeatherId)
        {
            return;
        }

        cachedWeatherId = weatherId;
        weatherName = WeatherTitle(weatherId);
    }

    private static unsafe byte ReadWeatherId(uint territoryId)
    {
        if (territoryId == 0)
        {
            return 0;
        }

        var manager = WeatherManager.Instance();
        if (manager is null)
        {
            return 0;
        }

        return manager->GetWeatherForHour((ushort)territoryId, 0);
    }

    private string WeatherTitle(byte weatherId)
    {
        if (weatherId != 0 && data.GetExcelSheet<WeatherSheet>().TryGetRow(weatherId, out var weather))
        {
            return weather.Name.ExtractText();
        }

        return string.Empty;
    }
}
