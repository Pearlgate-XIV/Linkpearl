using Linkpearl.Applets;
using Linkpearl.Applets.Life.Notes;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class NotesModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.notes", "Notes", order: 105);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<NotesApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<NotesApplet>());
    }
}