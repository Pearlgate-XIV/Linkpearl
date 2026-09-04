using Linkpearl.Applets;
using Linkpearl.Applets.Life.AfterDark;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class AfterDarkModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.afterdark", "Daylight", order: 119);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<AfterDarkApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<AfterDarkApplet>());
    }
}
