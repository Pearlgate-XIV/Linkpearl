using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Linkpearl.Host.Composition;

namespace Linkpearl.Host;

public sealed class Plugin : IDalamudPlugin
{
    private const string PrimaryCommand = "/linkpearl";

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

    private readonly HandsetHost host;

    public Plugin()
    {
        host = new HandsetHost(PluginInterface, Framework, ClientState, ObjectTable, Condition, DutyState, Log);
        Commands.AddHandler(PrimaryCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Linkpearl handset.",
        });
    }

    public void Dispose()
    {
        Commands.RemoveHandler(PrimaryCommand);
        host.Dispose();
    }

    private void OnCommand(string command, string arguments) => host.OpenHandset();
}
