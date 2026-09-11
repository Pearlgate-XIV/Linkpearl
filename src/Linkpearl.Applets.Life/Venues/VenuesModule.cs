using Linkpearl.Applets;
using Linkpearl.Applets.Life.Venues;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class VenuesModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.venues", "Venues", order: 124);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<VenuesBook>();
        services.AddSingleton<VenuesDiary>();
        services.AddSingleton<VenuesApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<VenuesApplet>());
    }
}
