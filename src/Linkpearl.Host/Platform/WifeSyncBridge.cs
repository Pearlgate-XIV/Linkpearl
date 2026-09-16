using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Linkpearl.Platform;

namespace Linkpearl.Host.Platform;

public sealed class WifeSyncBridge : IWifeSync
{
    private static readonly (string Plugin, string[] Commands)[] Families =
    [
        ("MareSempiterne", ["/psync", "/playersync", "/ps", "/semp", "/ms", "/mare"]),
        ("PlayerSync", ["/psync", "/playersync", "/ps", "/mare"]),
        ("LightlessSync", ["/light", "/lightless"]),
        ("MareSynchronos", ["/mare"]),
        ("Snowcloak", ["/snowcloak", "/sc", "/mare"]),
    ];

    private readonly IDalamudPluginInterface plugins;
    private readonly ICommandManager commands;
    private bool? lastWanted;

    public WifeSyncBridge(IDalamudPluginInterface plugins, ICommandManager commands)
    {
        this.plugins = plugins;
        this.commands = commands;
    }

    public bool IsPresent => Resolve() is not null;

    public bool IsOn
    {
        get
        {
            var resolved = Resolve();
            if (resolved is null)
            {
                return false;
            }

            if (TryIpcConnected(resolved.Value.Plugin) is { } connected)
            {
                return connected;
            }

            if (TryReadFullPause(resolved.Value.Plugin) is { } paused)
            {
                return !paused;
            }

            return lastWanted ?? false;
        }
    }

    public string PluginName => Resolve()?.Plugin ?? string.Empty;

    public void SetOn(bool enabled)
    {
        var resolved = Resolve();
        if (resolved is null)
        {
            return;
        }

        lastWanted = enabled;
        var paused = TryReadFullPause(resolved.Value.Plugin);
        var command = resolved.Value.Command;

        if (enabled)
        {
            if (paused == true)
            {
                commands.ProcessCommand(command + " toggle");
                return;
            }

            commands.ProcessCommand(command + " toggle on");
            if (TryReadFullPause(resolved.Value.Plugin) == true)
            {
                commands.ProcessCommand(command + " toggle");
            }

            return;
        }

        if (paused == false || paused is null)
        {
            commands.ProcessCommand(command + " toggle off");
            return;
        }

        commands.ProcessCommand(command + " toggle");
    }

    private (string Plugin, string Command)? Resolve()
    {
        foreach (var (plugin, aliases) in Families)
        {
            if (!IsLoaded(plugin))
            {
                continue;
            }

            foreach (var alias in aliases)
            {
                if (HasCommand(alias))
                {
                    return (plugin, alias);
                }
            }

            return (plugin, aliases[0]);
        }

        foreach (var (plugin, aliases) in Families)
        {
            foreach (var alias in aliases)
            {
                if (HasCommand(alias))
                {
                    return (plugin, alias);
                }
            }
        }

        return null;
    }

    private bool IsLoaded(string internalName)
    {
        foreach (var plugin in plugins.InstalledPlugins)
        {
            if (plugin.IsLoaded &&
                string.Equals(plugin.InternalName, internalName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasCommand(string command)
    {
        foreach (var registered in commands.Commands.Keys)
        {
            if (string.Equals(registered, command, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool? TryIpcConnected(string plugin)
    {
        if (plugin.Length == 0)
        {
            return null;
        }

        foreach (var name in new[] { plugin, "PlayerSync", "MareSempiterne", "LightlessSync", "MareSynchronos" })
        {
            try
            {
                var gate = plugins.GetIpcSubscriber<bool>(name + ".GetIsConnected");
                return gate.InvokeFunc();
            }
            catch (IpcNotReadyError)
            {
            }
            catch (IpcError)
            {
            }
        }

        return null;
    }

    private bool? TryReadFullPause(string plugin)
    {
        var folder = plugins.ConfigDirectory.Parent;
        if (folder is null)
        {
            return null;
        }

        foreach (var name in PauseFiles(plugin))
        {
            var paused = ReadFullPause(Path.Combine(folder.FullName, name));
            if (paused is not null)
            {
                return paused;
            }
        }

        return null;
    }

    private static IEnumerable<string> PauseFiles(string plugin)
    {
        if (plugin.Length == 0)
        {
            yield break;
        }

        yield return Path.Combine(plugin, "server.json");
        yield return plugin + ".json";
    }

    private bool? ReadFullPause(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            return FindFullPause(doc.RootElement);
        }
        catch (IOException)
        {
            return lastWanted is { } wanted ? !wanted : null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool? FindFullPause(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("FullPause", out var pause) &&
                pause.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return pause.GetBoolean();
            }

            foreach (var property in node.EnumerateObject())
            {
                var nested = FindFullPause(property.Value);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in node.EnumerateArray())
            {
                var nested = FindFullPause(item);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }
}
