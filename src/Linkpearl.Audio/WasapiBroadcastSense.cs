using System.Globalization;
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
    private float monitorGain = 1f;
    private float streamGain = 1f;
    private byte[]? cueScratch;
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

    public float MonitorGain
    {
        get
        {
            lock (gate)
            {
                return monitorGain;
            }
        }
        set
        {
            lock (gate)
            {
                monitorGain = Math.Clamp(value, 0f, 2f);
            }
        }
    }

    public float StreamGain
    {
        get
        {
            lock (gate)
            {
                return streamGain;
            }
        }
        set
        {
            lock (gate)
            {
                streamGain = Math.Clamp(value, 0f, 2f);
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
            Choose(tapId);
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
        Add(list, WasapiDeviceScan.Render(), "out:", "Capture everything playing on this output.");
        Add(list, WasapiDeviceScan.Capture(), "in:", "Capture this microphone or line in.");
        return list.ToArray();
    }

    private static void Add(List<AudioPoint> list, ScannedPort[] found, string prefix, string fallbackHint)
    {
        for (var index = 0; index < found.Length; index++)
        {
            var port = found[index];
            var id = WindowsAudioIds.Native(port.Id) ? port.Id : prefix + port.Id;
            if (list.Exists(row => row.Id == id))
            {
                continue;
            }

            var name = prefix == "in:" && CableName.IsOutput(port.Label)
                ? "VB-Cable · " + port.Label
                : port.Label;
            var hint = prefix == "in:" && CableName.IsOutput(port.Label)
                ? "Play Rekordbox into CABLE Input. The phone captures CABLE Output."
                : prefix == "out:" && CableName.IsInput(port.Label)
                    ? "Playback sink. The phone captures the matching CABLE Output instead."
                    : port.Note.Length > 0 ? port.Note : fallbackHint;
            list.Add(new AudioPoint(id, name, hint));
        }
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

    public void Choose(string id)
    {
        if (id.Length == 0)
        {
            id = DefaultMixId;
        }

        var restart = false;
        lock (gate)
        {
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
            Halt();
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
            var opened = OpenTap(id) ?? OpenCableOutput(name);
            if (opened is null)
            {
                throw new InvalidOperationException("No capture device opened.");
            }

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
            WaveInTap? fallback = null;
            try
            {
                fallback = WaveInTap.OpenNamed(CableName.SourceHint(name)) ??
                           WaveInTap.OpenNamed("CABLE Output") ??
                           WaveInTap.OpenNamed(name);
                if (fallback is not null)
                {
                    fallback.DataAvailable += OnData;
                    fallback.RecordingStopped += OnStopped;
                    fallback.Start();
                    lock (gate)
                    {
                        tap = fallback;
                        listening = true;
                        notice = "Hearing CABLE Output after " + name + " failed to open.";
                    }

                    return;
                }
            }
            catch (Exception)
            {
                fallback?.Dispose();
            }

            lock (gate)
            {
                tap = null;
                listening = false;
                notice = "Could not open " + name +
                         " (" + error.GetType().Name +
                         (error.Message.Length > 0 ? ": " + error.Message : "") + ").";
            }
        }
    }

    public void Halt()
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

    public void Dispose() => Halt();

    private string ResolveCableId(string id)
    {
        var named = points.FirstOrDefault(row => row.Id == id);
        var label = named.Name.Length > 0 ? named.Name : selectedName;
        if (!CableName.IsInput(label))
        {
            return id;
        }

        var output = points.FirstOrDefault(row =>
            WindowsAudioIds.Capture(row.Id) && CableName.IsOutput(row.Name));
        return output.Id.Length > 0 ? output.Id : id;
    }

    private IDeviceTap OpenTap(string id)
    {
        ReleaseHeldDevice();
        heldEnum = new MMDeviceEnumerator();
        var label = WasapiDevices.Bare(NameOf(id));
        id = ResolveCableId(id);
        if (label.Length == 0)
        {
            label = WasapiDevices.Bare(NameOf(id));
        }

        if (CableName.IsInput(label) || CableName.IsOutput(label) || CableName.IsFamily(label))
        {
            var cable = OpenCableOutput(label);
            if (cable is not null)
            {
                return cable;
            }
        }

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

        if (id.StartsWith("dsout:", StringComparison.Ordinal) || id.StartsWith("dsin:", StringComparison.Ordinal))
        {
            var capture = id.StartsWith("dsin:", StringComparison.Ordinal);
            return OpenByName(capture ? DataFlow.Capture : DataFlow.Render, label, capture);
        }

        if (id.StartsWith("asio:", StringComparison.Ordinal))
        {
            return OpenByName(DataFlow.Render, id[5..], capture: false);
        }

        if (id.StartsWith("out:", StringComparison.Ordinal))
        {
            return OpenById(DataFlow.Render, id[4..], label, capture: false);
        }

        if (id.StartsWith("in:", StringComparison.Ordinal))
        {
            return OpenById(DataFlow.Capture, id[3..], label, capture: true);
        }

        if (id.StartsWith("waveout:", StringComparison.Ordinal) ||
            id.StartsWith("wavein:", StringComparison.Ordinal))
        {
            var want = id.StartsWith("wavein:", StringComparison.Ordinal);
            if (!int.TryParse(want ? id[7..] : id[8..], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var deviceNumber))
            {
                deviceNumber = -1;
            }

            if (label.Length > 0)
            {
                return OpenByName(want ? DataFlow.Capture : DataFlow.Render, label, want);
            }

            if (want)
            {
                return WaveInTap.OpenNumber(deviceNumber);
            }
        }

        heldDevice = heldEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        return OpenRender(heldDevice);
    }

    private string NameOf(string tapId)
    {
        lock (gate)
        {
            var named = points.FirstOrDefault(row => row.Id == tapId);
            return named.Name.Length > 0 ? named.Name : selectedName;
        }
    }

    private IDeviceTap OpenById(DataFlow flow, string deviceId, string name, bool capture)
    {
        if (heldEnum is not null && deviceId.Length > 0)
        {
            try
            {
                heldDevice = heldEnum.GetDevice(deviceId);
                return capture ? OpenCapture(heldDevice) : OpenRender(heldDevice);
            }
            catch (Exception)
            {
                heldDevice = null;
            }
        }

        return OpenByName(flow, name, capture);
    }

    private IDeviceTap OpenByName(DataFlow flow, string name, bool capture)
    {
        if (heldEnum is not null && name.Length > 0)
        {
            var device = WasapiDevices.Find(heldEnum, flow, name);
            if (device is not null)
            {
                heldDevice = device;
                return capture ? OpenCapture(device) : OpenRender(device);
            }

            if (!capture)
            {
                var source = WasapiDevices.Find(heldEnum, DataFlow.Capture, CableName.SourceHint(name));
                if (source is not null)
                {
                    heldDevice = source;
                    return OpenCapture(source);
                }
            }
        }

        var wave = WaveInTap.OpenNamed(name) ??
                   WaveInTap.OpenNamed(CableName.SourceHint(name)) ??
                   WaveInTap.OpenNamed("CABLE Output");
        if (wave is not null)
        {
            return wave;
        }

        heldDevice = heldEnum!.GetDefaultAudioEndpoint(flow, Role.Multimedia);
        return capture ? OpenCapture(heldDevice) : OpenRender(heldDevice);
    }

    private IDeviceTap OpenRender(MMDevice render)
    {
        var name = render.FriendlyName ?? string.Empty;
        if (CableName.IsInput(name) || CableName.IsFamily(name))
        {
            var cable = OpenCableOutput(name);
            if (cable is not null)
            {
                return cable;
            }
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
            return MixClientTap.Open(render, loopback: true);
        }
        catch (Exception error)
        {
            last = error;
        }

        var wave = WaveInTap.OpenNamed(name);
        if (wave is not null)
        {
            return wave;
        }

        try
        {
            return new NaudioDeviceTap(new WasapiLoopbackCapture());
        }
        catch (Exception error)
        {
            throw last ?? error;
        }
    }

    private IDeviceTap? OpenCableOutput(string inputName)
    {
        using var extra = new MMDeviceEnumerator();
        var search = heldEnum ?? extra;
        var output = CableName.FindOutput(search, inputName);
        if (output is not null)
        {
            var unused = heldDevice;
            heldDevice = output;
            try
            {
                var capture = OpenCapture(output);
                unused?.Dispose();
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

        return WaveInTap.OpenNamed(CableName.SourceHint(inputName)) ??
               WaveInTap.OpenNamed("CABLE Output") ??
               WaveInTap.OpenNamed("CABLE Out");
    }

    private static IDeviceTap OpenCapture(MMDevice capture)
    {
        var name = capture.FriendlyName ?? string.Empty;
        if (CableName.IsOutput(name) || CableName.IsFamily(name))
        {
            var waveFirst = WaveInTap.OpenNamed(name) ??
                            WaveInTap.OpenNamed(CableName.SourceHint(name)) ??
                            WaveInTap.OpenNamed("CABLE Output");
            if (waveFirst is not null)
            {
                return waveFirst;
            }
        }

        try
        {
            return new NaudioDeviceTap(new WasapiCapture(capture));
        }
        catch (Exception)
        {
        }

        try
        {
            return MixClientTap.Open(capture, loopback: false);
        }
        catch (Exception)
        {
        }

        var wave = WaveInTap.OpenNamed(name) ??
                   WaveInTap.OpenNamed(CableName.SourceHint(name)) ??
                   WaveInTap.OpenNamed("CABLE Output");
        if (wave is not null)
        {
            return wave;
        }

        return MixClientTap.Open(capture, loopback: false);
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

        float capture;
        float cue;
        float stream;
        lock (gate)
        {
            capture = micGain;
            cue = monitorGain;
            stream = streamGain;
        }

        var peak = AudioMix.Peak(args.Buffer, args.BytesRecorded, tap.Format) * capture;
        var mixed = AudioMix.ToStereoFloat(args.Buffer, args.BytesRecorded, tap.Format);
        AudioMix.Scale(mixed, capture);
        var rate = tap.Format.SampleRate;
        lock (gate)
        {
            if (mixed.Length > 0 && monitorBuffer is not null && cue > 0.001f)
            {
                if (Math.Abs(cue - 1f) < 0.001f)
                {
                    monitorBuffer.AddSamples(mixed, 0, mixed.Length);
                }
                else
                {
                    if (cueScratch is null || cueScratch.Length < mixed.Length)
                    {
                        cueScratch = new byte[mixed.Length];
                    }

                    Buffer.BlockCopy(mixed, 0, cueScratch, 0, mixed.Length);
                    AudioMix.Scale(cueScratch, cue);
                    monitorBuffer.AddSamples(cueScratch, 0, mixed.Length);
                }
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

        AudioMix.Scale(mixed, stream);
        var pcm = AudioMix.ToStereoPcm16(mixed);
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
