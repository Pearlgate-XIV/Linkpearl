using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Linkpearl.Host.Composition;

namespace Linkpearl.Host;

public sealed class Plugin : IDalamudPlugin
{
    private static readonly string[] OpenCommands = ["/linkpearl", "/lp", "/pearl", "/phone"];
    private readonly List<string> boundCommands = [];

    [PluginService]
    private static IDalamudPluginInterface PluginInterface { get; set; } = null!;

    [PluginService]
    private static ICommandManager Commands { get; set; } = null!;

    [PluginService]
    private static IFramework Framework { get; set; } = null!;

    [PluginService]
    private static IClientState ClientState { get; set; } = null!;

    [PluginService]
    private static IObjectTable ObjectTable { get; set; } = null!;

    [PluginService]
    private static ICondition Condition { get; set; } = null!;

    [PluginService]
    private static IDutyState DutyState { get; set; } = null!;

    [PluginService]
    private static IPluginLog Log { get; set; } = null!;

    [PluginService]
    private static ITextureProvider TextureProvider { get; set; } = null!;

    [PluginService]
    private static IDataManager DataManager { get; set; } = null!;

    [PluginService]
    private static IChatGui ChatGui { get; set; } = null!;

    [PluginService]
    private static IPartyList PartyList { get; set; } = null!;

    [PluginService]
    private static IKeyState KeyState { get; set; } = null!;

    [PluginService]
    private static ITargetManager TargetManager { get; set; } = null!;

    [PluginService]
    private static IGameConfig GameConfig { get; set; } = null!;

    private readonly HandsetHost host;

    public Plugin()
    {
        host = new HandsetHost(PluginInterface, Framework, ClientState, ObjectTable, Condition, DutyState, Log,
            TextureProvider, DataManager, ChatGui, PartyList, KeyState, Commands, TargetManager, GameConfig);
        BindOpenCommands();
    }

    public void Dispose()
    {
        for (var index = 0; index < boundCommands.Count; index++)
        {
            Commands.RemoveHandler(boundCommands[index]);
        }

        host.Dispose();
    }

    private void BindOpenCommands()
    {
        var help = "Open the Linkpearl phone, or wake it if it is minimized.";
        for (var index = 0; index < OpenCommands.Length; index++)
        {
            var name = OpenCommands[index];
            if (Commands.Commands.ContainsKey(name))
            {
                Log.Warning("Could not bind {Command}; another plugin already uses it.", name);
                continue;
            }

            Commands.AddHandler(name, new CommandInfo(OnCommand) { HelpMessage = help });
            boundCommands.Add(name);
        }
    }

    private void OnCommand(string command, string arguments) => host.OpenHandset();
}
