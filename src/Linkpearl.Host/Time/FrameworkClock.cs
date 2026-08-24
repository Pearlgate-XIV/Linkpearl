using Dalamud.Plugin.Services;
using Linkpearl.Time;

namespace Linkpearl.Host.Time;

public sealed class FrameworkClock : IClock, IFrameLoop, IDisposable
{
    private readonly IFramework framework;

    public FrameworkClock(IFramework framework)
    {
        this.framework = framework;
        framework.Update += HandleUpdate;
    }

    public event Action<float>? Tick;

    public DateTimeOffset Now => DateTimeOffset.Now;

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public float DeltaSeconds { get; private set; }

    public double ElapsedSeconds { get; private set; }

    public void Post(Action work) => _ = framework.RunOnFrameworkThread(work);

    public void Dispose() => framework.Update -= HandleUpdate;

    private void HandleUpdate(IFramework runningFramework)
    {
        DeltaSeconds = (float)runningFramework.UpdateDelta.TotalSeconds;
        ElapsedSeconds += DeltaSeconds;
        Tick?.Invoke(DeltaSeconds);
    }
}
