using Linkpearl.Applets.Life.Vybe;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class VybeModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.vybe", "VYBE", order: 119);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<VybeApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<VybeApplet>());
    }
}
