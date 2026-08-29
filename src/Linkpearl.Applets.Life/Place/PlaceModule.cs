using Linkpearl.Applets;
using Linkpearl.Applets.Life.Place;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class PlaceModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.place", "Place", order: 140);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<PlaceApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<PlaceApplet>());
    }
}