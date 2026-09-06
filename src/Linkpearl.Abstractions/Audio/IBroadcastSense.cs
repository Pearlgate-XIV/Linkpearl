namespace Linkpearl.Audio;

public readonly record struct AudioPoint(string Id, string Name, string Hint);

public interface IBroadcastSense : IDisposable
{
    const string DefaultMixId = "mix";

    const string DefaultMicId = "talk";

    IReadOnlyList<AudioPoint> Points { get; }

    string SelectedId { get; }

    string SelectedName { get; }

    bool Listening { get; }

    bool Monitoring { get; }

    float Level { get; }

    string Notice { get; }

    float MicGain { get; set; }

    void RoutePhone(string speakerId, string microphoneId);

    void RefreshPoints();

    void RescanPoints();

    void Select(string id);

    void Start();

    void Stop();

    void StartMonitor();

    void StopMonitor();

    void Beep();

    event Action<byte[], int, int>? CapturedPcm;
}
