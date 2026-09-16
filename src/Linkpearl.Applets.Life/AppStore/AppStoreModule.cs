using Linkpearl.Applets.Life.AppStore;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class AppStoreModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.appstore", "App Store", order: 123);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<AppStoreApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<AppStoreApplet>());
    }
}
