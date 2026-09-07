namespace Linkpearl.Audio;

public static class WindowsAudioIds
{
    public static bool Native(string id) =>
        id.StartsWith("wave", StringComparison.Ordinal) ||
        id.StartsWith("asio:", StringComparison.Ordinal) ||
        id.StartsWith("dsout:", StringComparison.Ordinal) ||
        id.StartsWith("dsin:", StringComparison.Ordinal) ||
        id.StartsWith("out:", StringComparison.Ordinal) ||
        id.StartsWith("in:", StringComparison.Ordinal);

    public static bool Playback(string id) =>
        id.StartsWith("waveout:", StringComparison.Ordinal) ||
        id.StartsWith("asio:", StringComparison.Ordinal) ||
        id.StartsWith("dsout:", StringComparison.Ordinal) ||
        id.StartsWith("out:", StringComparison.Ordinal);

    public static bool Capture(string id) =>
        id.StartsWith("wavein:", StringComparison.Ordinal) ||
        id.StartsWith("dsin:", StringComparison.Ordinal) ||
        id.StartsWith("in:", StringComparison.Ordinal) ||
        string.Equals(id, "talk", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(id, "mic", StringComparison.OrdinalIgnoreCase);
}
