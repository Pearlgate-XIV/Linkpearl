using Linkpearl.Applets.Life.Feedback;
using Linkpearl.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Applets.Life;

public sealed class FeedbackModule : ILinkpearlModule
{
    public ModuleIdentity Identity => new("linkpearl.applets.life.feedback", "Feedback", order: 118);

    public void Configure(IServiceCollection services, ModuleContext context)
    {
        services.AddSingleton<FeedbackApplet>();
        services.AddSingleton<IApplet>(provider => provider.GetRequiredService<FeedbackApplet>());
    }
}
