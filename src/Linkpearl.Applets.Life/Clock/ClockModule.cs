using Linkpearl.Applets.Life.Clock;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class ClockModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.clock", "Clock", order: 100);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<ClockApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<ClockApplet>());
    }
}
