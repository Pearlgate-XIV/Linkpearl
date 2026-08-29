using Linkpearl.Applets;
using Linkpearl.Applets.Life.Alarms;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class AlarmsModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.alarms", "Alarms", order: 102);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<AlarmsApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<AlarmsApplet>());
    }
}
