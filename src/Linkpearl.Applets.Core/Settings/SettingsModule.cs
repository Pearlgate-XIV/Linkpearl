using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Core;

public sealed class SettingsModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.core.settings", "Settings", order: 10);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        // Settings is a destination (SettingsDestination). The old SettingsApplet stub
        // is kept in the tree but not registered, so RouteTrail cannot open it.
        _ = services;
        _ = context;
    }
}
