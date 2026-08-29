using Linkpearl.Applets;
using Linkpearl.Applets.Life.Timer;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class TimerModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.timer", "Timer", order: 120);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<TimerApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<TimerApplet>());
    }
}