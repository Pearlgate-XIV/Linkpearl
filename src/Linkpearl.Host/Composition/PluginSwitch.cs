using System.Collections;
using System.Reflection;
using Dalamud.Plugin;

namespace Linkpearl.Host.Composition;

internal static class PluginSwitch
{
    public static void TurnOff(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            TurnOffCore(pluginInterface);
        }
        catch
        {
            // Power-off must not stall the game if Dalamud refuses the unload.
        }
    }

    private static void TurnOffCore(IDalamudPluginInterface pluginInterface)
    {
        var dalamud = typeof(IDalamudPluginInterface).Assembly;
        var plugin = FindLocal(dalamud, pluginInterface.InternalName);
        if (plugin is null)
        {
            return;
        }

        var id = (Guid)plugin.GetType().GetProperty("EffectiveWorkingPluginId")!.GetValue(plugin)!;
        if (TryProfileOff(dalamud, id, pluginInterface.InternalName))
        {
            return;
        }

        Unload(dalamud, plugin);
    }

    private static object? FindLocal(Assembly dalamud, string internalName)
    {
        var manager = Service(dalamud, "Dalamud.Plugin.Internal.PluginManager");
        if (manager is null)
        {
            return null;
        }

        if (manager.GetType().GetProperty("InstalledPlugins")?.GetValue(manager) is not IEnumerable list)
        {
            return null;
        }

        foreach (var plugin in list)
        {
            var name = plugin.GetType().GetProperty("InternalName")?.GetValue(plugin) as string;
            if (string.Equals(name, internalName, StringComparison.OrdinalIgnoreCase))
            {
                return plugin;
            }
        }

        return null;
    }

    private static bool TryProfileOff(Assembly dalamud, Guid id, string internalName)
    {
        try
        {
            var profiles = Service(dalamud, "Dalamud.Plugin.Internal.Profiles.ProfileManager");
            var profile = profiles?.GetType().GetProperty("DefaultProfile")?.GetValue(profiles);
            var method = profile?.GetType().GetMethod("AddOrUpdateAsync",
                [typeof(Guid), typeof(string), typeof(bool), typeof(bool)]);
            if (method is null)
            {
                return false;
            }

            return Finish(method.Invoke(profile, [id, internalName, false, true]));
        }
        catch
        {
            return false;
        }
    }

    private static void Unload(Assembly dalamud, object plugin)
    {
        var modeType = dalamud.GetType("Dalamud.Plugin.Internal.Types.PluginLoaderDisposalMode");
        if (modeType is null)
        {
            return;
        }

        var mode = Enum.GetValues(modeType).GetValue(0);
        Finish(plugin.GetType().GetMethod("UnloadAsync", [modeType])?.Invoke(plugin, [mode]));
    }

    private static bool Finish(object? result)
    {
        if (result is not Task task)
        {
            return result is not null;
        }

        task.ConfigureAwait(false).GetAwaiter().GetResult();
        return !task.IsFaulted;
    }

    private static object? Service(Assembly dalamud, string typeName)
    {
        var raw = dalamud.GetType(typeName);
        var service = dalamud.GetType("Dalamud.Service`1");
        if (raw is null || service is null)
        {
            return null;
        }

        return service.MakeGenericType(raw).GetMethod("Get", Type.EmptyTypes)?.Invoke(null, null);
    }
}
