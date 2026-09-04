namespace Linkpearl.Audio;

public interface IBroadcastPush : IDisposable
{
    bool Sending { get; }

    string Notice { get; }

    void Start(string ingestUrl, string stationName, string genre);

    void Stop();

    void WritePcm(byte[] pcm16Stereo, int bytes, int sampleRate);
}
