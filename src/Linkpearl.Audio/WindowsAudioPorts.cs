using System.Globalization;
using NAudio.CoreAudioApi;

namespace Linkpearl.Audio;

public sealed class WindowsAudioPorts : IAudioPorts
{
    private AudioPort[] speakers = [];
    private AudioPort[] mics = [];
    private string defaultSpeakerId = string.Empty;
    private string defaultMicId = string.Empty;
    private string status = "No devices scanned yet.";
    private long lastRefresh;

    public WindowsAudioPorts() => Refresh();

    public IReadOnlyList<AudioPort> Speakers => speakers;

    public IReadOnlyList<AudioPort> Microphones => mics;

    public string DefaultSpeakerId => defaultSpeakerId;

    public string DefaultMicrophoneId => defaultMicId;

    public string Status => status;

    public void Refresh()
    {
        var now = Environment.TickCount64;
        if (lastRefresh != 0 && now - lastRefresh < 750)
        {
            return;
        }

        lastRefresh = now;
        try
        {
            speakers = Map(WasapiDeviceScan.Render());
            mics = Map(WasapiDeviceScan.Capture());
            defaultSpeakerId = WasapiEndpoint.DefaultId(DataFlow.Render);
            defaultMicId = WasapiEndpoint.DefaultId(DataFlow.Capture);
            status = speakers.Length.ToString(CultureInfo.InvariantCulture) +
                " playback · " +
                mics.Length.ToString(CultureInfo.InvariantCulture) +
                " capture · WASAPI/MME/DirectSound/ASIO";
        }
        catch (Exception)
        {
            status = "Could not rescan this PC's audio devices.";
        }
    }

    private static AudioPort[] Map(ScannedPort[] found)
    {
        var ports = new List<AudioPort>();
        for (var index = 0; index < found.Length; index++)
        {
            var port = new AudioPort(found[index].Id, found[index].Label, found[index].Note);
            var existing = ports.FindIndex(row => SameName(row.Label, port.Label));
            if (existing < 0)
            {
                ports.Add(port);
                continue;
            }

            if (Rank(port.Id) < Rank(ports[existing].Id))
            {
                ports[existing] = port;
            }
        }

        return ports.ToArray();
    }

    private static bool SameName(string left, string right) =>
        WasapiDevices.NamesMatch(left, right);

    private static int Rank(string id)
    {
        if (id.StartsWith("ds", StringComparison.Ordinal))
        {
            return 4;
        }

        if (id.StartsWith("wave", StringComparison.Ordinal))
        {
            return 3;
        }

        if (id.StartsWith("asio:", StringComparison.Ordinal))
        {
            return 2;
        }

        return 0;
    }
}
