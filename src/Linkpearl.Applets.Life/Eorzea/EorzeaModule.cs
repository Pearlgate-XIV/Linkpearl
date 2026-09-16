using Linkpearl.Applets.Life.Eorzea;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class EorzeaModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.eorzea", "Eorzea", order: 145);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<EorzeaApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<EorzeaApplet>());
    }
}
