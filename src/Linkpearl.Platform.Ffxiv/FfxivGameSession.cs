using System.Globalization;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using NativeObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Linkpearl.Platform;
using ClassJobSheet = Lumina.Excel.Sheets.ClassJob;
using ItemSheet = Lumina.Excel.Sheets.Item;
using TerritorySheet = Lumina.Excel.Sheets.TerritoryType;
using WeatherSheet = Lumina.Excel.Sheets.Weather;
using RaceSheet = Lumina.Excel.Sheets.Race;
using TribeSheet = Lumina.Excel.Sheets.Tribe;
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
    private readonly IJobCatalog jobs;
    private CharacterIdentity character = CharacterIdentity.Unknown;
    private uint cachedJobId;
    private uint cachedTerritoryId;
    private byte cachedWeatherId;
    private uint jobIconId;
    private string jobName = string.Empty;
    private string raceName = string.Empty;
    private byte raceId;
    private string tribeName = string.Empty;
    private byte tribeId;
    private int phoneCountry;
    private string zoneName = string.Empty;
    private string weatherName = string.Empty;
    private readonly object retainerGate = new();
    private GameRetainer[] retainers = [];
    private bool retainersReady;
    private bool pocketCue;
    private bool openGpose;

    public FfxivGameSession(IClientState clientState, IObjectTable objectTable, ICondition condition,
        IDutyState dutyState, IPartyList party, IFramework framework, IDataManager data, IJobCatalog jobs)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.condition = condition;
        this.dutyState = dutyState;
        this.party = party;
        this.framework = framework;
        this.data = data;
        this.jobs = jobs;
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

    public uint JobId => cachedJobId;

    public uint JobIconId => jobIconId;

    public string JobName => jobName;

    public string RaceName => raceName;

    public byte RaceId => raceId;

    public string TribeName => tribeName;

    public byte TribeId => tribeId;

    public int PhoneCountry => phoneCountry;

    public string ZoneName => zoneName;

    public string MapPlace => ReadMapPlace();

    public Vector2 MapCoords => ReadMapCoords();

    public string WeatherName => weatherName;

    public uint Gil => ReadGil();

    public IReadOnlyList<GameCurrency> Currencies => ReadCurrencies();

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

    public IReadOnlyList<GameRetainer> Retainers
    {
        get
        {
            lock (retainerGate)
            {
                return retainers;
            }
        }
    }

    public bool RetainersReady
    {
        get
        {
            lock (retainerGate)
            {
                return retainersReady;
            }
        }
    }

    public void OpenGroupPose() => openGpose = true;

    public void CuePocket() => pocketCue = true;

    public bool TakePocketCue()
    {
        var ready = pocketCue;
        pocketCue = false;
        return ready;
    }

    public string ItemName(uint itemId)
    {
        if (itemId != 0 && data.GetExcelSheet<ItemSheet>().TryGetRow(itemId, out var item))
        {
            return item.Name.ExtractText();
        }

        return string.Empty;
    }

    private static unsafe void RunGroupPoseCommand()
    {
        var ui = UIModule.Instance();
        if (ui is null)
        {
            return;
        }

        var text = Utf8String.FromString("/gpose");
        if (text is null || text->Length == 0)
        {
            if (text is not null)
            {
                text->Dtor(true);
            }

            return;
        }

        try
        {
            ui->ProcessChatBoxEntry(text);
        }
        finally
        {
            text->Dtor(true);
        }
    }

    private static unsafe uint ReadGil()
    {
        var inventory = InventoryManager.Instance();
        return inventory is null ? 0u : inventory->GetGil();
    }

    private unsafe IReadOnlyList<GameCurrency> ReadCurrencies()
    {
        if (!clientState.IsLoggedIn)
        {
            return [];
        }

        var inventory = InventoryManager.Instance();
        var purse = CurrencyManager.Instance();
        var state = PlayerState.Instance();
        var rows = new List<GameCurrency>(20);
        Add(rows, inventory, purse, string.Empty, 1u, inventory is null ? 0u : inventory->GetGil(), 0u);
        Add(rows, inventory, purse, string.Empty, 29u, inventory is null ? 0u : inventory->GetGoldSaucerCoin(),
            CapOf(purse, 29u, 9_999_999u));
        Add(rows, inventory, purse, string.Empty, 21072u, CountOf(inventory, purse, 21072u),
            CapOf(purse, 21072u, 65_000u));
        var company = state is null ? (byte)0 : state->GrandCompany;
        if (company is 1 or 2 or 3 && inventory is not null)
        {
            var seal = company == 1 ? 20u : company == 2 ? 21u : 22u;
            Add(rows, inventory, purse, string.Empty, seal, inventory->GetCompanySeals(company),
                inventory->GetMaxCompanySeals(company));
        }

        Add(rows, inventory, purse, "The Hunt", 27u, inventory is null ? 0u : inventory->GetAlliedSeals(),
            CapOf(purse, 27u, 4_000u));
        Add(rows, inventory, purse, "The Hunt", 10307u, CountOf(inventory, purse, 10307u),
            CapOf(purse, 10307u, 4_000u));
        Add(rows, inventory, purse, "The Hunt", 26533u, CountOf(inventory, purse, 26533u),
            CapOf(purse, 26533u, 4_000u));

        var weekly = inventory is null ? 0 : inventory->GetWeeklyAcquiredTomestoneCount();
        var weeklyCap = InventoryManager.GetLimitedTomestoneWeeklyLimit();
        AddTomestone(rows, inventory, purse, 49u, weekly, weeklyCap);
        AddTomestone(rows, inventory, purse, 48u, 0, 0);
        AddTomestone(rows, inventory, purse, 28u, 0, 0);

        Add(rows, inventory, purse, "PvP", 25u, inventory is null ? 0u : inventory->GetWolfMarks(),
            CapOf(purse, 25u, 20_000u));
        Add(rows, inventory, purse, "PvP", 36656u, CountOf(inventory, purse, 36656u),
            CapOf(purse, 36656u, 20_000u));

        Add(rows, inventory, purse, "Crafting & Gathering", 33913u, CountOf(inventory, purse, 33913u),
            CapOf(purse, 33913u, 4_000u));
        Add(rows, inventory, purse, "Crafting & Gathering", 33914u, CountOf(inventory, purse, 33914u),
            CapOf(purse, 33914u, 4_000u));
        Add(rows, inventory, purse, "Crafting & Gathering", 41784u, CountOf(inventory, purse, 41784u),
            CapOf(purse, 41784u, 4_000u));
        Add(rows, inventory, purse, "Crafting & Gathering", 41785u, CountOf(inventory, purse, 41785u),
            CapOf(purse, 41785u, 4_000u));
        Add(rows, inventory, purse, "Crafting & Gathering", 28063u, CountOf(inventory, purse, 28063u),
            CapOf(purse, 28063u, 10_000u));

        Add(rows, inventory, purse, "Miscellaneous", 4868u, CountOf(inventory, purse, 4868u),
            CapOf(purse, 4868u, 999u));
        Add(rows, inventory, purse, "Miscellaneous", 26807u, CountOf(inventory, purse, 26807u),
            CapOf(purse, 26807u, 1_500u));
        return rows;
    }

    private unsafe void AddTomestone(List<GameCurrency> rows, InventoryManager* inventory, CurrencyManager* purse,
        uint itemId, int weekly, int weeklyCap)
    {
        var held = inventory is null ? 0u : inventory->GetTomestoneCount(itemId);
        Add(rows, inventory, purse, "Tomestones", itemId, held, CapOf(purse, itemId, 2_000u),
            weeklyCap > 0 ? (uint)Math.Max(0, weekly) : 0u, weeklyCap > 0 ? (uint)weeklyCap : 0u);
    }

    private unsafe void Add(List<GameCurrency> rows, InventoryManager* inventory, CurrencyManager* purse, string group,
        uint itemId, uint held, uint cap, uint weeklyHeld = 0, uint weeklyCap = 0)
    {
        _ = inventory;
        var name = ItemName(itemId);
        if (name.Length == 0)
        {
            return;
        }

        rows.Add(new GameCurrency(group, itemId, name, IconOf(itemId), held, cap, weeklyHeld, weeklyCap));
        _ = purse;
    }

    private uint IconOf(uint itemId)
    {
        if (itemId != 0 && data.GetExcelSheet<ItemSheet>().TryGetRow(itemId, out var item))
        {
            return item.Icon;
        }

        return 0u;
    }

    private static unsafe uint CountOf(InventoryManager* inventory, CurrencyManager* purse, uint itemId)
    {
        if (purse is not null && purse->HasItem(itemId))
        {
            return purse->GetItemCount(itemId);
        }

        return inventory is null ? 0u : (uint)Math.Max(0, inventory->GetInventoryItemCount(itemId));
    }

    private static unsafe uint CapOf(CurrencyManager* purse, uint itemId, uint fallback)
    {
        if (purse is not null && purse->HasItem(itemId))
        {
            var max = purse->GetItemMaxCount(itemId);
            if (max > 0)
            {
                return max;
            }
        }

        return fallback;
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
        if (openGpose)
        {
            openGpose = false;
            RunGroupPoseCommand();
        }

        if (!clientState.IsLoggedIn)
        {
            cachedJobId = 0;
            cachedTerritoryId = 0;
            jobIconId = 0;
            jobName = string.Empty;
            raceName = string.Empty;
            raceId = 0;
            tribeName = string.Empty;
            tribeId = 0;
            phoneCountry = 0;
            zoneName = string.Empty;
            weatherName = string.Empty;
            cachedWeatherId = 0;
            lock (retainerGate)
            {
                retainers = [];
                retainersReady = false;
            }
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
        RefreshRetainers();
    }

    private void HandleLogin()
    {
        character = ReadCharacter();
        RefreshLabels();
        RefreshRetainers();
        LoggedIn?.Invoke();
        CharacterChanged?.Invoke(character);
    }

    private void HandleLogout(int type, int code)
    {
        character = CharacterIdentity.Unknown;
        cachedJobId = 0;
        cachedTerritoryId = 0;
        jobIconId = 0;
        jobName = string.Empty;
        raceName = string.Empty;
        raceId = 0;
        tribeName = string.Empty;
        tribeId = 0;
        phoneCountry = 0;
        zoneName = string.Empty;
        weatherName = string.Empty;
        cachedWeatherId = 0;
        lock (retainerGate)
        {
            retainers = [];
            retainersReady = false;
        }
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
                jobIconId = jobs.IconFor(jobId);
            }

            var race = localPlayer.Customize[(int)CustomizeIndex.Race];
            var tribe = localPlayer.Customize[(int)CustomizeIndex.Tribe];
            var gender = localPlayer.Customize[(int)CustomizeIndex.Gender];
            AbsorbDrawnLook(localPlayer.Address, ref race, ref tribe);
            raceId = race;
            tribeId = tribe;
            raceName = RaceTitle(race, gender);
            tribeName = TribeTitle(tribe, gender);
        }
        else
        {
            raceId = 0;
            raceName = string.Empty;
            tribeId = 0;
            tribeName = string.Empty;
        }

        var territoryId = clientState.TerritoryType;
        if (territoryId != cachedTerritoryId)
        {
            RememberZone(territoryId);
        }

        RememberWeather(territoryId);
    }

    private unsafe void RefreshRetainers()
    {
        var manager = RetainerManager.Instance();
        if (manager is null || !manager->IsReady)
        {
            return;
        }

        var next = new List<GameRetainer>(10);
        for (byte index = 0; index < 10; index++)
        {
            var row = manager->GetRetainerBySortedIndex(index);
            if (row is null || row->RetainerId == 0)
            {
                continue;
            }

            var name = row->NameString;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            next.Add(new GameRetainer(index, name.Trim(), row->Gil, row->MarketItemCount, row->VentureComplete));
        }

        lock (retainerGate)
        {
            retainers = next.ToArray();
            retainersReady = true;
        }
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

        phoneCountry = CountryOf(homeWorld);
        return new CharacterIdentity(contentId, name, WorldName(homeWorld), homeWorld, currentWorld);
    }

    private static unsafe ulong ReadContentId()
    {
        var info = InfoModule.Instance();
        return info is null ? 0UL : info->GetLocalContentId();
    }

    private int CountryOf(uint worldId)
    {
        if (worldId != 0 && data.GetExcelSheet<WorldSheet>().TryGetRow(worldId, out var world) &&
            world.DataCenter.IsValid)
        {
            var center = world.DataCenter.Value.Name.ExtractText();
            if (center is "Aether" or "Primal" or "Crystal" or "Dynamis")
            {
                return 1;
            }

            if (center is "Chaos" or "Light")
            {
                return 2;
            }

            if (center is "Elemental" or "Gaia" or "Mana" or "Meteor")
            {
                return 3;
            }

            if (center is "Materia")
            {
                return 4;
            }
        }

        return 0;
    }

    private string WorldName(uint rowId)
    {
        if (rowId != 0 && data.GetExcelSheet<WorldSheet>().TryGetRow(rowId, out var world))
        {
            return world.Name.ExtractText();
        }

        return string.Empty;
    }

    private static unsafe void AbsorbDrawnLook(nint address, ref byte race, ref byte tribe)
    {
        if (address == 0)
        {
            return;
        }

        var draw = ((NativeObject*)address)->DrawObject;
        if (draw == null)
        {
            return;
        }

        var human = (Human*)draw;
        var drawnRace = human->Customize.Race;
        var drawnTribe = human->Customize.Tribe;
        if (drawnRace is >= 1 and <= 8)
        {
            race = drawnRace;
        }

        if (drawnTribe is >= 1 and <= 16)
        {
            tribe = drawnTribe;
        }

        var family = human->RaceSexId / 100;
        if (family is 11 or 12 or 13 or 14)
        {
            race = 3;
            tribe = family is 11 or 12 ? (byte)5 : (byte)6;
        }
    }

    private string RaceTitle(byte raceId, byte gender)
    {
        if (raceId != 0 && data.GetExcelSheet<RaceSheet>().TryGetRow(raceId, out var race))
        {
            var title = gender == 1 ? race.Feminine.ExtractText() : race.Masculine.ExtractText();
            if (title.Length > 0)
            {
                return title;
            }
        }

        return string.Empty;
    }

    private string TribeTitle(byte tribeRow, byte gender)
    {
        if (tribeRow != 0 && data.GetExcelSheet<TribeSheet>().TryGetRow(tribeRow, out var tribe))
        {
            var title = gender == 1 ? tribe.Feminine.ExtractText() : tribe.Masculine.ExtractText();
            if (title.Length > 0)
            {
                return title;
            }
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

    private string ReadMapPlace()
    {
        var map = ReadMapCoords();
        return map == Vector2.Zero
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"X: {map.X:0.0}  Y: {map.Y:0.0}");
    }

    private Vector2 ReadMapCoords()
    {
        var player = objectTable.LocalPlayer;
        if (player is null || !clientState.IsLoggedIn)
        {
            return Vector2.Zero;
        }

        try
        {
            var map = player.GetMapCoordinates(true);
            return new Vector2(map.X, map.Y);
        }
        catch
        {
            return Vector2.Zero;
        }
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
        var weatherId = territoryId == 0 ? (byte)0 : FfxivWeatherSense.Live((ushort)territoryId);
        if (weatherId == cachedWeatherId)
        {
            return;
        }

        cachedWeatherId = weatherId;
        weatherName = WeatherTitle(weatherId);
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
