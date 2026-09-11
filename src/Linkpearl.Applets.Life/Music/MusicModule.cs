using Linkpearl.Applets;
using Linkpearl.Applets.Life.Music;
using Linkpearl.Audio;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class MusicModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.music", "Music", order: 118);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<MusicApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<MusicApplet>());
        services.AddSingleton<IStationMarks>(provider => provider.GetRequiredService<MusicApplet>());
        services.AddSingleton<IEchoMixStation>(provider => provider.GetRequiredService<MusicApplet>());
    }
}
