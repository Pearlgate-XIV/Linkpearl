using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Linkpearl.Audio;

public sealed class WindowsAudioPorts : IAudioPorts
{
    private AudioPort[] speakers = [];
    private AudioPort[] mics = [];
    private string defaultSpeakerId = string.Empty;
    private string defaultMicId = string.Empty;
    private string status = "No devices scanned yet.";

    public WindowsAudioPorts() => Refresh();

    public IReadOnlyList<AudioPort> Speakers => speakers;

    public IReadOnlyList<AudioPort> Microphones => mics;

    public string DefaultSpeakerId => defaultSpeakerId;

    public string DefaultMicrophoneId => defaultMicId;

    public string Status => status;

    public void Refresh()
    {
        var outs = new List<AudioPort>();
        CollectWasapi(outs, DataFlow.Render, DeviceState.Active);
        if (outs.Count == 0)
        {
            CollectWasapi(outs, DataFlow.Render, DeviceState.All);
        }

        CollectWaveOut(outs);

        var ins = new List<AudioPort>();
        CollectWasapi(ins, DataFlow.Capture, DeviceState.Active);
        if (ins.Count == 0)
        {
            CollectWasapi(ins, DataFlow.Capture, DeviceState.All);
        }

        CollectWaveIn(ins);

        speakers = outs.ToArray();
        mics = ins.ToArray();
        defaultSpeakerId = WasapiEndpoint.DefaultId(DataFlow.Render);
        defaultMicId = WasapiEndpoint.DefaultId(DataFlow.Capture);
        status = speakers.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            " playback · " +
            mics.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + " capture";
    }

    private static void CollectWasapi(List<AudioPort> list, DataFlow flow, DeviceState state)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var found = enumerator.EnumerateAudioEndPoints(flow, state);
            var count = found.Count;
            for (var index = 0; index < count; index++)
            {
                try
                {
                    var device = found[index];
                    var id = device.ID ?? string.Empty;
                    var name = (device.FriendlyName ?? string.Empty).Trim();
                    if (name.Length == 0)
                    {
                        name = flow == DataFlow.Render ? "Playback " : "Input ";
                        name += (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }

                    if (id.Length == 0)
                    {
                        continue;
                    }

                    if (Named(list, name) || Ided(list, id))
                    {
                        continue;
                    }

                    list.Add(new AudioPort(id, name));
                }
                catch (Exception)
                {
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private static void CollectWaveOut(List<AudioPort> list)
    {
        try
        {
            var count = WaveOut.DeviceCount;
            for (var index = 0; index < count; index++)
            {
                var name = WaveOut.GetCapabilities(index).ProductName.Trim();
                if (name.Length == 0 || Named(list, name))
                {
                    continue;
                }

                list.Add(new AudioPort("waveout:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    name));
            }
        }
        catch (Exception)
        {
        }
    }

    private static void CollectWaveIn(List<AudioPort> list)
    {
        try
        {
            var count = WaveIn.DeviceCount;
            for (var index = 0; index < count; index++)
            {
                var name = WaveIn.GetCapabilities(index).ProductName.Trim();
                if (name.Length == 0 || Named(list, name))
                {
                    continue;
                }

                list.Add(new AudioPort("wavein:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    name));
            }
        }
        catch (Exception)
        {
        }
    }

    private static bool Ided(List<AudioPort> list, string id)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (string.Equals(list[index].Id, id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Named(List<AudioPort> list, string name)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (SameName(list[index].Label, name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameName(string left, string right) =>
        left.StartsWith(right, StringComparison.OrdinalIgnoreCase) ||
        right.StartsWith(left, StringComparison.OrdinalIgnoreCase);
}
