using Linkpearl.Applets.Life.Camera;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class CameraModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.camera", "Camera", order: 150);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<CameraApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<CameraApplet>());
    }
}
