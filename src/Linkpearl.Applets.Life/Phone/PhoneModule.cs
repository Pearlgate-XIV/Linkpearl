using Linkpearl.Applets;
using Linkpearl.Applets.Life.Phone;
using Linkpearl.Modules;
using Linkpearl.Phone;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class PhoneModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.phone", "Phone", order: 102);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<IHandsetLine, HandsetLine>();
        services.AddSingleton<PhoneApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<PhoneApplet>());
    }
}
