using Linkpearl.Applets;
using Linkpearl.Applets.Core.Settings;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Core;

public sealed class SettingsModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.core.settings", "Settings", order: 10);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<SettingsApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<SettingsApplet>());
    }
}
