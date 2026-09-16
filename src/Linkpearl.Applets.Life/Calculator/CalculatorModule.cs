using Linkpearl.Applets.Life.Calculator;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class CalculatorModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.calculator", "Calculator", order: 110);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<CalculatorApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<CalculatorApplet>());
    }
}
