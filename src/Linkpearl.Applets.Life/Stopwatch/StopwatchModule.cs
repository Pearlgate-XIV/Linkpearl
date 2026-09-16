using Linkpearl.Applets.Life.Stopwatch;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class StopwatchModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.stopwatch", "Stopwatch", order: 130);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<StopwatchApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<StopwatchApplet>());
    }
}