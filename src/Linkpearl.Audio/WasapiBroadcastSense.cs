using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Linkpearl.Audio;

// Taps a chosen Windows playback device or microphone.
// and can play that tap back as a local monitor. Remote listeners still need
// a Pearlgate listen URL. The DJ does not.
public sealed class WasapiBroadcastSense : IBroadcastSense
{
    public const string DefaultMixId = "mix";

    private readonly object gate = new();
    private AudioPoint[] points = [new(DefaultMixId, "Default playback", "Whatever this PC is playing right now.")];
    private string selectedId = DefaultMixId;
    private string selectedName = "Windows default mix";
    private IDeviceTap? tap;
    private MMDeviceEnumerator? heldEnum;
    private MMDevice? heldDevice;
    private BufferedWaveProvider? monitorBuffer;
    private WaveFormat? monitorFormat;
    private IWavePlayer? monitor;
    private float level;
    private bool listening;
    private bool monitoring;
    private string phoneSpeakerId = string.Empty;
    private string phoneMicId = string.Empty;
    private float micGain = 0.80f;
    private string notice = "Pick Sound or Mic, then the device to capture.";

    public IReadOnlyList<AudioPoint> Points
    {
        get
        {
            lock (gate)
            {
                return points;
            }
        }
    }

    public string SelectedId
    {
        get
        {
            lock (gate)
            {
                return selectedId;
            }
        }
    }

    public string SelectedName
    {
        get
        {
            lock (gate)
            {
                return selectedName;
            }
        }
    }

    public bool Listening
    {
        get
        {
            lock (gate)
            {
                return listening;
            }
        }
    }

    public bool Monitoring
    {
        get
        {
            lock (gate)
            {
                return monitoring;
            }
        }
    }

    public float Level
    {
        get
        {
            lock (gate)
            {
                level *= 0.82f;
                return level;
            }
        }
    }

    public string Notice
    {
        get
        {
            lock (gate)
            {
                return notice;
            }
        }
    }

    public event Action<byte[], int, int>? CapturedPcm;

    public float MicGain
    {
        get
        {
            lock (gate)
            {
                return micGain;
            }
        }
        set
        {
            lock (gate)
            {
                micGain = Math.Clamp(value, 0f, 2f);
            }
        }
    }

    public WasapiBroadcastSense() => RescanPoints();

    public void RoutePhone(string speakerId, string microphoneId)
    {
        var nextSpeaker = speakerId ?? string.Empty;
        var nextMic = microphoneId ?? string.Empty;
        var remonitor = false;
        var recapture = false;
        lock (gate)
        {
            remonitor = monitoring && !string.Equals(phoneSpeakerId, nextSpeaker, StringComparison.Ordinal);
            recapture = listening && selectedId.StartsWith("in:", StringComparison.Ordinal) &&
                nextMic.Length > 0 && !string.Equals(phoneMicId, nextMic, StringComparison.Ordinal);
            phoneSpeakerId = nextSpeaker;
            phoneMicId = nextMic;
        }

        if (recapture)
        {
            var tapId = nextMic.StartsWith("wavein:", StringComparison.Ordinal) ||
                nextMic.StartsWith("in:", StringComparison.Ordinal)
                ? nextMic
                : "in:" + nextMic;
            Select(tapId);
        }

        if (remonitor)
        {
            StopMonitor();
            StartMonitor();
        }
    }

    public void RefreshPoints()
    {
        lock (gate)
        {
            if (points.Length > 1)
            {
                return;
            }
        }

        RescanPoints();
    }

    public void RescanPoints()
    {
        try
        {
            var found = ReadEndpoints();
            lock (gate)
            {
                ApplyPoints(found);
            }
        }
        catch (Exception)
        {
        }
    }

    private void ApplyPoints(AudioPoint[] found)
    {
        if (found.Length > 0)
        {
            points = found;
        }

        var named = points.FirstOrDefault(row => row.Id == selectedId);
        if (named.Name.Length > 0)
        {
            selectedName = named.Name;
        }

        if (!listening)
        {
            var outs = points.Count(row => row.Id.StartsWith("out:", StringComparison.Ordinal));
            var ins = points.Count(row => row.Id.StartsWith("in:", StringComparison.Ordinal));
            notice = outs + " playback devices · " + ins + " inputs on this PC.";
        }
    }

    private static AudioPoint[] ReadEndpoints()
    {
        var list = new List<AudioPoint>
        {
            new(DefaultMixId, "Default playback", "Whatever this PC is playing right now."),
        };
        Collect(list, DataFlow.Render, DeviceState.Active, "out:", "Capture everything playing on this output.");
        Collect(list, DataFlow.Capture, DeviceState.Active, "in:", "Capture this microphone or line in.");
        if (list.Count <= 1)
        {
            Collect(list, DataFlow.Render, DeviceState.All, "out:", "Capture everything playing on this output.");
            Collect(list, DataFlow.Capture, DeviceState.All, "in:", "Capture this microphone or line in.");
        }

        MergeWaveNames(list);
        return list.ToArray();
    }

    private static void Collect(List<AudioPoint> list, DataFlow flow, DeviceState state, string prefix, string hint)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var collection = enumerator.EnumerateAudioEndPoints(flow, state);
            var count = collection.Count;
            for (var index = 0; index < count; index++)
            {
                try
                {
                    var device = collection[index];
                    var id = device.ID;
                    var name = device.FriendlyName;
                    if (id.Length == 0 || name.Length == 0)
                    {
                        continue;
                    }

                    var key = prefix + id;
                    if (list.Exists(row => row.Id == key))
                    {
                        continue;
                    }

                    if (prefix == "out:" && CableName.IsInput(name))
                    {
                        continue;
                    }

                    var shown = prefix == "in:" && CableName.IsOutput(name)
                        ? "VB-Cable · " + name
                        : name;
                    var detail = prefix == "in:" && CableName.IsOutput(name)
                        ? "Play Rekordbox into CABLE Input. The phone captures CABLE Output."
                        : hint;
                    list.Add(new AudioPoint(key, shown, detail));
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

    private static void MergeWaveNames(List<AudioPoint> list)
    {
        try
        {
            for (var index = 0; index < WaveOut.DeviceCount; index++)
            {
                var name = WaveOut.GetCapabilities(index).ProductName;
                if (name.Length == 0 || list.Exists(row => row.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                list.Add(new AudioPoint("waveout:" + index, name, "Windows playback device."));
            }

            for (var index = 0; index < WaveIn.DeviceCount; index++)
            {
                var name = WaveIn.GetCapabilities(index).ProductName;
                if (name.Length == 0 || list.Exists(row => row.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                list.Add(new AudioPoint("wavein:" + index, name, "Windows input device."));
            }
        }
        catch (Exception)
        {
        }
    }

    public void Select(string id)
    {
        if (id.Length == 0)
        {
            id = DefaultMixId;
        }

        var restart = false;
        lock (gate)
        {
            id = ResolveCableId(id);
            if (string.Equals(selectedId, id, StringComparison.Ordinal))
            {
                var named = points.FirstOrDefault(row => row.Id == id);
                if (named.Name.Length > 0)
                {
                    selectedName = named.Name;
                }

                return;
            }

            selectedId = id;
            selectedName = points.FirstOrDefault(row => row.Id == id).Name;
            if (selectedName.Length == 0)
            {
                selectedName = id == DefaultMixId
                    ? "Windows default mix"
                    : id == IBroadcastSense.DefaultMicId
                        ? "Windows default microphone"
                        : "Audio device";
            }

            restart = listening;
        }

        if (restart)
        {
            var keepMonitor = Monitoring;
            Stop();
            Start();
            if (keepMonitor)
            {
                StartMonitor();
            }
        }
    }

    public void Start()
    {
        lock (gate)
        {
            if (listening)
            {
                return;
            }
        }

        string id;
        string name;
        lock (gate)
        {
            id = selectedId;
            name = selectedName;
        }

        try
        {
            var opened = OpenTap(id);
            opened.DataAvailable += OnData;
            opened.RecordingStopped += OnStopped;
            opened.Start();
            lock (gate)
            {
                tap = opened;
                listening = true;
                notice = "Hearing " + selectedName + ". Play Rekordbox, Serato, or VLC on that device — the meter should jump.";
            }
        }
        catch (Exception error)
        {
            ReleaseHeldDevice();
            lock (gate)
            {
                tap = null;
                listening = false;
                notice = "Could not open " + name +
                         (error.Message.Length > 0 ? " (" + error.Message + ")" : ".") +
                         " For a virtual cable, pick CABLE Output. For speakers, pick that playback device.";
            }
        }
    }

    public void Stop()
    {
        StopMonitor();
        IDeviceTap? capture;
        lock (gate)
        {
            capture = tap;
            tap = null;
            listening = false;
            level = 0f;
            notice = "Off air. The phone is not listening to this PC.";
        }

        if (capture is null)
        {
            return;
        }

        try
        {
            capture.Stop();
        }
        catch (Exception)
        {
        }

        capture.Dispose();
        ReleaseHeldDevice();
    }

    public void StartMonitor()
    {
        if (!Listening)
        {
            Start();
        }

        lock (gate)
        {
            if (monitoring)
            {
                return;
            }

            if (tap is null)
            {
                return;
            }

            try
            {
                monitorFormat = AudioMix.StereoFloat(tap.Format.SampleRate);
                monitorBuffer = new BufferedWaveProvider(monitorFormat)
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromMilliseconds(800),
                };
                var output = OpenMonitor(monitorBuffer, phoneSpeakerId);
                output.Play();
                monitor = output;
                monitoring = true;
                notice = selectedId is DefaultMixId or { Length: 0 }
                    ? "Hearing this PC's default mix. Use headphones if you are also capturing speakers."
                    : "Hearing " + selectedName + " on this phone.";
            }
            catch (Exception error)
            {
                monitorBuffer = null;
                monitoring = false;
                notice = "Could not start playback" +
                         (error.Message.Length > 0 ? " (" + error.Message + ")" : ".");
            }
        }
    }

    public void StopMonitor()
    {
        IWavePlayer? output;
        lock (gate)
        {
            output = monitor;
            monitor = null;
            monitorBuffer = null;
            monitorFormat = null;
            monitoring = false;
        }

        if (output is null)
        {
            return;
        }

        try
        {
            output.Stop();
        }
        catch (Exception)
        {
        }

        output.Dispose();
    }

    public void Beep()
    {
        string speaker;
        lock (gate)
        {
            speaker = phoneSpeakerId;
        }

        _ = Task.Run(() =>
        {
            try
            {
                var tone = new SignalGenerator(44100, 1)
                {
                    Type = SignalGeneratorType.Sin,
                    Frequency = 880,
                    Gain = 0.28,
                }.Take(TimeSpan.FromMilliseconds(420));
                using var output = WasapiEndpoint.OpenPlayback(speaker);
                output.Init(tone);
                output.Play();
                while (output.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(20);
                }
            }
            catch (Exception)
            {
            }
        });
    }

    public void Dispose() => Stop();

    private string ResolveCableId(string id)
    {
        var named = points.FirstOrDefault(row => row.Id == id);
        if (named.Name.Length == 0 || !CableName.IsInput(named.Name))
        {
            return id;
        }

        var output = points.FirstOrDefault(row =>
            (row.Id.StartsWith("in:", StringComparison.Ordinal) ||
             row.Id.StartsWith("wavein:", StringComparison.Ordinal)) &&
            CableName.IsOutput(row.Name));
        return output.Id.Length > 0 ? output.Id : id;
    }

    private IDeviceTap OpenTap(string id)
    {
        ReleaseHeldDevice();
        heldEnum = new MMDeviceEnumerator();
        id = ResolveCableId(id);
        if (id.Length == 0 || id == DefaultMixId)
        {
            heldDevice = heldEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return OpenRender(heldDevice);
        }

        if (string.Equals(id, IBroadcastSense.DefaultMicId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(id, "mic", StringComparison.OrdinalIgnoreCase))
        {
            heldDevice = heldEnum.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            return OpenCapture(heldDevice);
        }

        if (id.StartsWith("out:", StringComparison.Ordinal))
        {
            heldDevice = heldEnum.GetDevice(id[4..]);
            return OpenRender(heldDevice);
        }

        if (id.StartsWith("in:", StringComparison.Ordinal))
        {
            heldDevice = heldEnum.GetDevice(id[3..]);
            return OpenCapture(heldDevice);
        }

        if (id.StartsWith("waveout:", StringComparison.Ordinal) ||
            id.StartsWith("wavein:", StringComparison.Ordinal))
        {
            var want = id.StartsWith("wavein:", StringComparison.Ordinal);
            var name = want
                ? WaveIn.GetCapabilities(int.Parse(id[7..], System.Globalization.CultureInfo.InvariantCulture)).ProductName
                : WaveOut.GetCapabilities(int.Parse(id[8..], System.Globalization.CultureInfo.InvariantCulture)).ProductName;
            var flow = want ? DataFlow.Capture : DataFlow.Render;
            foreach (var device in heldEnum.EnumerateAudioEndPoints(flow, DeviceState.Active))
            {
                if (!device.FriendlyName.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                heldDevice = device;
                return want ? OpenCapture(device) : OpenRender(device);
            }
        }

        heldDevice = heldEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        return OpenRender(heldDevice);
    }

    private IDeviceTap OpenRender(MMDevice render)
    {
        if (CableName.IsInput(render.FriendlyName))
        {
            var cable = OpenCableOutput(render.FriendlyName);
            if (cable is not null)
            {
                return cable;
            }

            throw new InvalidOperationException(
                "CABLE Input is a playback sink. Pick VB-Cable · CABLE Output.");
        }

        Exception? last = null;
        try
        {
            return new NaudioDeviceTap(new WasapiLoopbackCapture(render));
        }
        catch (Exception error)
        {
            last = error;
        }

        try
        {
            return new NaudioDeviceTap(new WasapiLoopbackCapture());
        }
        catch (Exception error)
        {
            last = error;
        }

        try
        {
            return MixClientTap.Open(render, loopback: true);
        }
        catch (Exception error)
        {
            throw last ?? error;
        }
    }

    private IDeviceTap? OpenCableOutput(string inputName)
    {
        if (heldEnum is not null)
        {
            var output = CableName.FindOutput(heldEnum, inputName);
            if (output is not null)
            {
                var unused = heldDevice;
                heldDevice = output;
                try
                {
                    var capture = OpenCapture(output);
                    unused?.Dispose();
                    selectedName = output.FriendlyName;
                    return capture;
                }
                catch (Exception)
                {
                    if (!ReferenceEquals(heldDevice, unused))
                    {
                        output.Dispose();
                        heldDevice = unused;
                    }
                }
            }
        }

        return WaveInTap.OpenNamed("CABLE Output");
    }

    private static IDeviceTap OpenCapture(MMDevice capture)
    {
        var wave = WaveInTap.OpenNamed(capture.FriendlyName);
        if (wave is not null && CableName.IsOutput(capture.FriendlyName))
        {
            return wave;
        }

        try
        {
            return new NaudioDeviceTap(new WasapiCapture(capture));
        }
        catch (Exception)
        {
            if (wave is not null)
            {
                return wave;
            }

            IDeviceTap? cable = WaveInTap.OpenNamed("CABLE Output");
            return cable ?? MixClientTap.Open(capture, loopback: false);
        }
    }

    private void ReleaseHeldDevice()
    {
        heldDevice?.Dispose();
        heldDevice = null;
        heldEnum?.Dispose();
        heldEnum = null;
    }

    private static IWavePlayer OpenMonitor(IWaveProvider source, string speakerId)
    {
        var samples = source.ToSampleProvider();
        var output = WasapiEndpoint.OpenPlayback(speakerId, 200);
        output.Init(samples);
        return output;
    }

    private void OnData(object? sender, WaveInEventArgs args)
    {
        if (args.BytesRecorded <= 0 || tap is null)
        {
            return;
        }

        float gain;
        lock (gate)
        {
            gain = micGain;
        }

        var peak = AudioMix.Peak(args.Buffer, args.BytesRecorded, tap.Format) * gain;
        var mixed = AudioMix.ToStereoFloat(args.Buffer, args.BytesRecorded, tap.Format);
        AudioMix.Scale(mixed, gain);
        var pcm = AudioMix.ToStereoPcm16(mixed);
        var rate = tap.Format.SampleRate;
        lock (gate)
        {
            if (mixed.Length > 0)
            {
                monitorBuffer?.AddSamples(mixed, 0, mixed.Length);
            }

            if (peak > level)
            {
                level = peak;
            }

            if (!monitoring)
            {
                notice = peak > 0.02f
                    ? "The phone hears " + selectedName + "."
                    : "Tapped " + selectedName + ", but it is quiet. Play something on that device.";
            }
        }

        if (pcm.Length > 0)
        {
            CapturedPcm?.Invoke(pcm, pcm.Length, rate);
        }
    }

    private void OnStopped(object? sender, StoppedEventArgs args)
    {
        lock (gate)
        {
            listening = false;
            if (args.Exception is not null)
            {
                notice = "Capture stopped. Try Go live again.";
            }
        }
    }

}
