namespace Linkpearl.Audio;

public enum HandsetAudioPhase : byte
{
    Idle = 0,
    Buffering = 1,
    Playing = 2,
    Paused = 3,
    Failed = 4,
}

public readonly record struct HandsetTune(
    string Id,
    string Title,
    string Detail,
    string StreamUrl,
    bool Live,
    string ArtPath = "");

public interface IHandsetAudio : IDisposable
{
    HandsetAudioPhase Phase { get; }

    HandsetTune Now { get; }

    float Volume { get; set; }

    string Notice { get; }

    string SpeakerId { get; }

    void UseSpeaker(string deviceId);

    void Cue();

    void Play(HandsetTune tune);

    void PlayLocal(HandsetTune tune);

    void Pause();

    void Resume();

    void Stop();

    void Toggle();
}
