using Linkpearl.Applets;
using Linkpearl.Applets.Life.Wallet;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class WalletModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.wallet", "Wallet", order: 160);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<WalletApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<WalletApplet>());
    }
}
