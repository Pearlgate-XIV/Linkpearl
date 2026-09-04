namespace Linkpearl.Audio;

public readonly record struct AudioPort(string Id, string Label);

public interface IAudioPorts
{
    IReadOnlyList<AudioPort> Speakers { get; }

    IReadOnlyList<AudioPort> Microphones { get; }

    string DefaultSpeakerId { get; }

    string DefaultMicrophoneId { get; }

    string Status { get; }

    void Refresh();
}
