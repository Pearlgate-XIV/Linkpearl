namespace Linkpearl.Time;

public interface IClock
{
    DateTimeOffset Now { get; }

    DateTimeOffset UtcNow { get; }
}

public interface IFrameLoop
{
    event Action<float>? Tick;

    float DeltaSeconds { get; }

    double ElapsedSeconds { get; }

    void Post(Action work);
}
