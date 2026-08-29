using Linkpearl.Applets;
using Linkpearl.Applets.Life.Calendar;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class CalendarModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.calendar", "Calendar", order: 108);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<CalendarApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<CalendarApplet>());
    }
}
