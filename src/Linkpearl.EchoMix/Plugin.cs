using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using EchoMix.Plugin.Audio;
using EchoMix.Plugin.Ipc;
using EchoMix.Plugin.UI;
using EchoMix.Shared;

namespace EchoMix.Plugin;

// Hosted inside Linkpearl. Not a second Dalamud plugin: no /echomix commands, no UiBuilder
// takeover, and no EchoMix IPC (that would clash if the standalone plugin is also installed).
public sealed class Plugin : IDisposable
{
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    internal static IPluginLog Log { get; private set; } = null!;
    internal static IGameConfig GameConfig { get; private set; } = null!;
    internal static IFramework Framework { get; private set; } = null!;
    internal static IObjectTable ObjectTable { get; private set; } = null!;
    internal static ITextureProvider TextureProvider { get; private set; } = null!;
    internal static IClientState ClientState { get; private set; } = null!;

    public Configuration Configuration { get; }
    public AudioHostClient AudioHostClient { get; } = new();
    public Fonts Fonts { get; }

    public readonly WindowSystem WindowSystem = new("LinkpearlEchoMix");
    private readonly DjDeckWindow djDeckWindow;
    public DjDeckWindow DjDeckWindow => djDeckWindow;
    public HostLobbyWindow HostLobbyWindow { get; }
    public SongRequestWindow SongRequestWindow { get; }
    public FollowNotificationToast FollowNotificationToast { get; }
    public ServerNoticeToast ServerNoticeToast { get; }
    public ListenerJoinedToast ListenerJoinedToast { get; }
    public BroadcastReconnectToast BroadcastReconnectToast { get; }
    public AutoJoinedShowToast AutoJoinedShowToast { get; }
    public AutoLeftShowToast AutoLeftShowToast { get; }
    private readonly GameSoundMuteController gameSoundMuteController;
    private readonly ProximityTracker proximityTracker;
    public AutoJoinTracker AutoJoinTracker { get; }
    private int connectingFlag;
    private DateTime nextConnectUtc = DateTime.MinValue;
    private bool settingsRestored;
    private bool? lastSentMuteState;
    private float lastSentListenVolume = -1f;
    private bool wasListening;
    private bool wasMultiHost;
    private bool wasLive;
    private bool wasHostReconnecting;
    private readonly HashSet<Guid> knownListenerIds = new();
    private System.Threading.Timer? focusMuteTimer;
    private string? lastSentCharacterName;

    private float autoJoinPollAccumulator;
    private bool manualDisconnectRequested;
    private string? lastKnownAutoJoinedRoomCode;
    private string? pendingAutoJoinToastRoomCode;
    private string? pendingAutoLeaveToastDjName;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        IGameConfig gameConfig,
        IFramework framework,
        IObjectTable objectTable,
        ITextureProvider textureProvider,
        IClientState clientState)
    {
        PluginInterface = pluginInterface;
        Log = log;
        GameConfig = gameConfig;
        Framework = framework;
        ObjectTable = objectTable;
        TextureProvider = textureProvider;
        ClientState = clientState;

        Configuration = Configuration.LoadOrNew();
        Configuration.ShowWelcomeOnEnable = false;
        Configuration.LastChosenRole ??= UserRole.Dj;
        Theme.ApplyCustomColors(
            new Vector4(Configuration.DeckAAccentR, Configuration.DeckAAccentG, Configuration.DeckAAccentB, 1f),
            new Vector4(Configuration.DeckBAccentR, Configuration.DeckBAccentG, Configuration.DeckBAccentB, 1f),
            new Vector4(Configuration.BlendAccentR, Configuration.BlendAccentG, Configuration.BlendAccentB, 1f));
        Fonts = new Fonts(PluginInterface);
        gameSoundMuteController = new GameSoundMuteController(GameConfig);
        proximityTracker = new ProximityTracker(ObjectTable);
        AutoJoinTracker = new AutoJoinTracker(proximityTracker);

        djDeckWindow = new DjDeckWindow(this);
        WindowSystem.AddWindow(djDeckWindow);
        djDeckWindow.IsOpen = false;

        HostLobbyWindow = new HostLobbyWindow(this);
        WindowSystem.AddWindow(HostLobbyWindow);

        SongRequestWindow = new SongRequestWindow(this);
        WindowSystem.AddWindow(SongRequestWindow);

        FollowNotificationToast = new FollowNotificationToast(this);
        WindowSystem.AddWindow(FollowNotificationToast);

        ServerNoticeToast = new ServerNoticeToast(this);
        WindowSystem.AddWindow(ServerNoticeToast);

        ListenerJoinedToast = new ListenerJoinedToast(this);
        WindowSystem.AddWindow(ListenerJoinedToast);

        BroadcastReconnectToast = new BroadcastReconnectToast(this);
        WindowSystem.AddWindow(BroadcastReconnectToast);

        AutoJoinedShowToast = new AutoJoinedShowToast(this);
        WindowSystem.AddWindow(AutoJoinedShowToast);

        AutoLeftShowToast = new AutoLeftShowToast(this);
        WindowSystem.AddWindow(AutoLeftShowToast);

        Framework.Update += OnFrameworkUpdate;
        focusMuteTimer = new System.Threading.Timer(CheckFocusMute, null, 0, 15);
    }

    public bool IsDeckOpen => djDeckWindow.IsOpen;

    public bool IsLive => AudioHostClient.LatestStatus.Broadcast.IsLive;

    public bool Mixing
    {
        get
        {
            var status = AudioHostClient.LatestStatus;
            return status.DeckA.IsPlaying || status.DeckB.IsPlaying || status.Broadcast.IsLive
                || status.SpotifyMode.IsActive || status.ExternalInputMode.IsActive;
        }
    }

    public void OpenDeck()
    {
        djDeckWindow.ShowDeckImmediately();
        djDeckWindow.IsOpen = true;
        nextConnectUtc = DateTime.MinValue;
        _ = EnsureAudioHostConnectedAsync();
    }

    public void CloseDeck() => djDeckWindow.IsOpen = false;

    public void Draw()
    {
        if (!ClientState.IsLoggedIn)
        {
            return;
        }

        WindowSystem.Draw();
    }

    public void Dispose()
    {
        var status = AudioHostClient.LatestStatus;
        Configuration.CrossfaderPosition = status.CrossfaderPosition;
        Configuration.MasterVolume = status.MasterVolume;
        Configuration.DeckAGain = status.DeckA.Gain;
        Configuration.DeckBGain = status.DeckB.Gain;
        Configuration.IsDjWindowOpen = djDeckWindow.IsOpen;
        Configuration.Save();

        Framework.Update -= OnFrameworkUpdate;
        focusMuteTimer?.Dispose();
        gameSoundMuteController.SetShouldMute(false);

        WindowSystem.RemoveAllWindows();
        djDeckWindow.Dispose();

        if (AudioHostClient.HostProcess != null)
        {
            AudioHostClient.Send(MessageType.Shutdown, new object());
        }

        AudioHostClient.Dispose();
    }

    public void NotifyManualDisconnectRequested() => manualDisconnectRequested = true;

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!AudioHostClient.IsConnected)
        {
            if (djDeckWindow.IsOpen && DateTime.UtcNow >= nextConnectUtc)
            {
                _ = EnsureAudioHostConnectedAsync();
            }
        }
        else if (AudioHostClient.IsStale)
        {
            AudioHostClient.ForceDisconnect();
            nextConnectUtc = DateTime.UtcNow.AddSeconds(30);
        }

        var characterName = ObjectTable.LocalPlayer?.Name.TextValue;
        if (!string.IsNullOrEmpty(characterName) && characterName != lastSentCharacterName && AudioHostClient.IsConnected)
        {
            lastSentCharacterName = characterName;
            AudioHostClient.Send(MessageType.SetLocalIdentity, new SetLocalIdentityCommand { CharacterName = characterName });
        }

        var status = AudioHostClient.LatestStatus;
        var isPlayingAnyAudio = status.DeckA.IsPlaying || status.DeckB.IsPlaying || status.Broadcast.IsListening
            || status.SpotifyMode.IsActive || status.ExternalInputMode.IsActive;
        gameSoundMuteController.SetShouldMute(isPlayingAnyAudio);
        UpdateListenVolume(status.Broadcast);
        UpdateAutoJoin(status.Broadcast, framework.UpdateDelta);
        UpdateHostLobbyVisibility(status.Broadcast);

        SongRequestWindow.IsOpen = status.Broadcast.IsListening;
        if (!status.Broadcast.IsListening)
        {
            SongRequestWindow.IsExpanded = false;
        }

        var wentLive = AudioHostClient.ConsumeFollowedDjWentLive();
        if (wentLive != null)
        {
            FollowNotificationToast.Show(wentLive.DjName, wentLive.RoomCode);
        }

        ServerNoticeToast.ShowIfNew(status.Broadcast.ServerNotice);

        if (status.Broadcast.IsHostReconnecting && !wasHostReconnecting)
        {
            BroadcastReconnectToast.ShowDropped(status.Broadcast.RoomCode);
        }
        else if (!status.Broadcast.IsHostReconnecting && wasHostReconnecting)
        {
            if (status.Broadcast.IsLive)
            {
                BroadcastReconnectToast.ShowRecovered(status.Broadcast.RoomCode);
            }
            else
            {
                BroadcastReconnectToast.ShowGaveUp(status.Broadcast.BroadcastError);
            }
        }

        wasHostReconnecting = status.Broadcast.IsHostReconnecting;

        if (status.Broadcast.IsLive)
        {
            foreach (var listener in status.Broadcast.ListenerRoster)
            {
                if (knownListenerIds.Add(listener.ListenerId) && wasLive && Configuration.NotifyOnListenerJoin)
                {
                    ListenerJoinedToast.Show(listener.CharacterName);
                }
            }
        }
        else
        {
            knownListenerIds.Clear();
        }

        wasLive = status.Broadcast.IsLive;
    }

    private void UpdateHostLobbyVisibility(BroadcastStatusMessage broadcast)
    {
        HostLobbyWindow.IsOpen = broadcast.IsLive;

        var isMultiHost = broadcast.IsLive && broadcast.HostRoster.Count > 1;
        if (isMultiHost && !wasMultiHost)
        {
            HostLobbyWindow.IsExpanded = true;
        }

        wasMultiHost = isMultiHost;

        if (!broadcast.IsLive)
        {
            HostLobbyWindow.IsExpanded = false;
        }
    }

    private void UpdateListenVolume(BroadcastStatusMessage broadcast)
    {
        if (!broadcast.IsListening)
        {
            wasListening = false;
            return;
        }

        if (!wasListening)
        {
            lastSentListenVolume = -1f;
        }

        wasListening = true;

        var proximityFactor = broadcast.IsProximityAudio
            ? proximityTracker.ComputeVolume(broadcast.HostCharacterName, broadcast.ProximityRange)
            : 1f;
        var volume = Math.Clamp(proximityFactor * Configuration.ListenerVolume, 0f, 1.5f);
        if (MathF.Abs(volume - lastSentListenVolume) < 0.01f)
        {
            return;
        }

        lastSentListenVolume = volume;
        AudioHostClient.Send(MessageType.SetListenVolume, new SetListenVolumeCommand { Volume = volume });
    }

    private void UpdateAutoJoin(BroadcastStatusMessage broadcast, TimeSpan updateDelta)
    {
        if (lastKnownAutoJoinedRoomCode != null && !broadcast.IsListening && manualDisconnectRequested)
        {
            AutoJoinTracker.NotifyManualLeave(lastKnownAutoJoinedRoomCode);
        }

        manualDisconnectRequested = false;

        autoJoinPollAccumulator += (float)updateDelta.TotalSeconds;
        if (Configuration.ListenerAutoJoinNearbyShows && autoJoinPollAccumulator >= AutoJoinTuning.ListPollIntervalSeconds)
        {
            autoJoinPollAccumulator = 0f;
            AudioHostClient.Send(MessageType.RequestPublicShows, new object());
        }

        var decision = AutoJoinTracker.Tick(Configuration, broadcast, AudioHostClient.LatestPublicShows,
            (float)updateDelta.TotalSeconds);
        switch (decision.Kind)
        {
            case AutoJoinDecisionKind.Join:
                pendingAutoJoinToastRoomCode = decision.RoomCode;
                AudioHostClient.Send(MessageType.ConnectToRemote, new ConnectToRemoteCommand
                {
                    RoomCode = decision.RoomCode!,
                    Password = string.Empty,
                    CharacterName = ObjectTable.LocalPlayer?.Name.TextValue ?? string.Empty,
                });
                break;

            case AutoJoinDecisionKind.Leave:
                pendingAutoLeaveToastDjName = broadcast.HostDjName;
                AudioHostClient.Send(MessageType.DisconnectFromRemote, new object());
                break;

            case AutoJoinDecisionKind.SwitchTo:
                AudioHostClient.Send(MessageType.DisconnectFromRemote, new object());
                break;
        }

        if (pendingAutoJoinToastRoomCode != null && broadcast.IsListening &&
            broadcast.RoomCode == pendingAutoJoinToastRoomCode)
        {
            if (Configuration.ListenerAutoJoinNotifications)
            {
                AutoJoinedShowToast.Show(broadcast.HostDjName ?? "Unknown DJ");
            }

            pendingAutoJoinToastRoomCode = null;
        }

        if (pendingAutoLeaveToastDjName != null && !broadcast.IsListening)
        {
            if (Configuration.ListenerAutoJoinNotifications)
            {
                AutoLeftShowToast.Show(pendingAutoLeaveToastDjName);
            }

            pendingAutoLeaveToastDjName = null;
        }

        lastKnownAutoJoinedRoomCode = AutoJoinTracker.AutoJoinedRoomCode;
    }

    private void CheckFocusMute(object? state)
    {
        var shouldMute = Configuration.MuteWhenUnfocused && !GameWindowFocus.IsGameFocused;
        if (lastSentMuteState != shouldMute)
        {
            lastSentMuteState = shouldMute;
            Log.Debug($"[EchoMix] Sending SetOutputMuted={shouldMute} (gameFocused={GameWindowFocus.IsGameFocused})");
            AudioHostClient.Send(MessageType.SetOutputMuted, new SetOutputMutedCommand { Muted = shouldMute });
        }
    }

    private async Task EnsureAudioHostConnectedAsync()
    {
        if (AudioHostClient.IsConnected)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref connectingFlag, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var assemblyDirectory = PluginInterface.AssemblyLocation.DirectoryName
                ?? throw new InvalidOperationException("Could not determine plugin assembly directory.");
            var echoDir = Path.Combine(PluginInterface.GetPluginConfigDirectory(), "echomix");
            Directory.CreateDirectory(echoDir);

            await AudioHostLauncher.ConnectOrLaunchAsync(AudioHostClient, assemblyDirectory, echoDir, Log);
            if (!AudioHostClient.IsConnected)
            {
                nextConnectUtc = DateTime.UtcNow.AddSeconds(20);
            }

            if (AudioHostClient.IsConnected && !settingsRestored)
            {
                settingsRestored = true;
                AudioHostClient.Send(MessageType.SetCrossfader,
                    new SetCrossfaderCommand { Position = Configuration.CrossfaderPosition });
                AudioHostClient.Send(MessageType.SetCrossfaderCurve,
                    new SetCrossfaderCurveCommand { Curve = Configuration.CrossfaderCurve });
                AudioHostClient.Send(MessageType.SetAutoDj,
                    new SetAutoDjCommand { Enabled = Configuration.AutoDjEnabled, FadeSeconds = Configuration.AutoDjFadeSeconds });
                AudioHostClient.Send(MessageType.SetMasterVolume,
                    new SetMasterVolumeCommand { Volume = Configuration.MasterVolume });
                AudioHostClient.Send(MessageType.SetGain,
                    new SetGainCommand { Deck = DeckId.A, Gain = Configuration.DeckAGain });
                AudioHostClient.Send(MessageType.SetGain,
                    new SetGainCommand { Deck = DeckId.B, Gain = Configuration.DeckBGain });
                AudioHostClient.Send(MessageType.SetPreviewVolume,
                    new SetPreviewVolumeCommand { Volume = Configuration.PreviewVolume });
                AudioHostClient.Send(MessageType.SetPreviewDampenVolume,
                    new SetPreviewDampenVolumeCommand { Volume = Configuration.PreviewDampenVolume });
                AudioHostClient.Send(MessageType.SetSongRequestAccessControl, new SetSongRequestAccessControlCommand
                {
                    Mode = Configuration.SongRequestAccessMode,
                    Whitelist = Configuration.SongRequestWhitelist,
                    Blacklist = Configuration.SongRequestBlacklist,
                });

                lastSentMuteState = null;
            }
        }
        catch (Exception ex)
        {
            nextConnectUtc = DateTime.UtcNow.AddSeconds(20);
            Log.Warning("[EchoMix] AudioHost connect failed ({0}). Retrying in 20s.", ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref connectingFlag, 0);
        }
    }
}
