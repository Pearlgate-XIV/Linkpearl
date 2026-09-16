using Linkpearl.Applets.Life.Weather;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class WeatherModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.weather", "Weather", order: 130);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<WeatherApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<WeatherApplet>());
    }
}
