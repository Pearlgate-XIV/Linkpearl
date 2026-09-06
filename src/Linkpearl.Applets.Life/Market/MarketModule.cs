using Linkpearl.Applets;
using Linkpearl.Applets.Life.Market;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class MarketModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.market", "Market", order: 122);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<MarketApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<MarketApplet>());
    }
}
