using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Linkpearl.Audio;

public sealed class WasapiStreamPlayer : IHandsetAudio
{
    private readonly object gate = new();
    private readonly Action<float>? persistVolume;
    private IWavePlayer? output;
    private WaveStream? reader;
    private HttpClient? streamHttp;
    private Stream? streamBody;
    private VolumeSampleProvider? gain;
    private float volume = 0.7f;
    private HandsetTune now = new(string.Empty, string.Empty, string.Empty, string.Empty, false);
    private HandsetAudioPhase phase = HandsetAudioPhase.Idle;
    private string notice = string.Empty;
    private string speakerId = string.Empty;
    private int generation;

    public WasapiStreamPlayer(float volume, Action<float>? persistVolume = null)
    {
        this.volume = Math.Clamp(volume, 0f, 1f);
        this.persistVolume = persistVolume;
    }

    public HandsetAudioPhase Phase
    {
        get
        {
            lock (gate)
            {
                return phase;
            }
        }
    }

    public HandsetTune Now
    {
        get
        {
            lock (gate)
            {
                return now;
            }
        }
    }

    public float Volume
    {
        get
        {
            lock (gate)
            {
                return volume;
            }
        }
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            lock (gate)
            {
                volume = clamped;
                if (gain is not null)
                {
                    gain.Volume = clamped;
                }
            }

            persistVolume?.Invoke(clamped);
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

    public string SpeakerId
    {
        get
        {
            lock (gate)
            {
                return speakerId;
            }
        }
    }

    public void UseSpeaker(string deviceId)
    {
        var next = deviceId ?? string.Empty;
        HandsetTune replay;
        var playing = false;
        lock (gate)
        {
            if (string.Equals(speakerId, next, StringComparison.Ordinal))
            {
                return;
            }

            speakerId = next;
            replay = now;
            playing = phase is HandsetAudioPhase.Playing or HandsetAudioPhase.Buffering or HandsetAudioPhase.Paused
                && replay.StreamUrl.Length > 0;
        }

        if (playing)
        {
            Play(replay);
        }
    }

    public void Cue()
    {
        string speaker;
        lock (gate)
        {
            speaker = speakerId;
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var tone = new SignalGenerator(44100, 1)
                {
                    Type = SignalGeneratorType.Sin,
                    Frequency = 880,
                    Gain = 0.22,
                }.Take(TimeSpan.FromMilliseconds(280));
                using var output = WasapiEndpoint.OpenPlayback(speaker);
                output.Init(tone);
                output.Play();
                while (output.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(16);
                }
            }
            catch (Exception)
            {
            }
        });
    }

    public void Play(HandsetTune tune)
    {
        if (tune.StreamUrl.Length == 0)
        {
            lock (gate)
            {
                notice = "This station has no stream yet.";
                phase = HandsetAudioPhase.Failed;
                now = tune;
            }

            return;
        }

        StopUnlocked();
        var ticket = Interlocked.Increment(ref generation);
        lock (gate)
        {
            now = tune;
            phase = HandsetAudioPhase.Buffering;
            notice = string.Empty;
        }

        ThreadPool.QueueUserWorkItem(_ => Open(tune, ticket));
    }

    public void PlayLocal(HandsetTune tune)
    {
        lock (gate)
        {
            StopUnlocked();
            now = tune;
            phase = HandsetAudioPhase.Playing;
            notice = "Local monitor — this is your capture, not the relay.";
        }
    }

    public void Pause()
    {
        lock (gate)
        {
            output?.Pause();
            if (phase == HandsetAudioPhase.Playing)
            {
                phase = HandsetAudioPhase.Paused;
            }
        }
    }

    public void Resume()
    {
        lock (gate)
        {
            if (phase != HandsetAudioPhase.Paused)
            {
                return;
            }

            output?.Play();
            phase = HandsetAudioPhase.Playing;
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            StopUnlocked();
            phase = HandsetAudioPhase.Idle;
            notice = string.Empty;
        }
    }

    public void Toggle()
    {
        if (Phase == HandsetAudioPhase.Playing)
        {
            Pause();
            return;
        }

        if (Phase == HandsetAudioPhase.Paused)
        {
            Resume();
            return;
        }

        var tune = Now;
        if (tune.StreamUrl.Length > 0)
        {
            Play(tune);
            return;
        }

        if (tune.Id.Length > 0)
        {
            PlayLocal(tune);
        }
    }

    public void Dispose() => Stop();

    private void Open(HandsetTune tune, int ticket)
    {
        Exception? last = null;
        var tries = tune.Live ? 4 : 1;
        for (var attempt = 0; attempt < tries; attempt++)
        {
            if (attempt > 0)
            {
                Thread.Sleep(450);
            }

            foreach (var url in IcecastListen.Candidates(tune.StreamUrl))
            {
                if (ticket != Volatile.Read(ref generation))
                {
                    return;
                }

                if (TryStart(url, ticket, out last))
                {
                    return;
                }
            }
        }

        lock (gate)
        {
            if (ticket != generation)
            {
                return;
            }

            phase = HandsetAudioPhase.Failed;
            notice = last?.Message is { Length: > 0 } text
                ? text
                : "Could not start the stream.";
        }
    }

    private bool TryStart(string url, int ticket, out Exception? error)
    {
        error = null;
        WaveStream? nextReader = null;
        HttpClient? http = null;
        Stream? body = null;
        IWavePlayer? nextOut = null;
        try
        {
            try
            {
                nextReader = new MediaFoundationReader(url);
            }
            catch (Exception first)
            {
                http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
                http.DefaultRequestHeaders.TryAddWithoutValidation("Icy-MetaData", "0");
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Linkpearl/0.1");
                body = http.GetStreamAsync(url).GetAwaiter().GetResult();
                try
                {
                    nextReader = new StreamMediaFoundationReader(body);
                }
                catch (Exception)
                {
                    nextReader = new Mp3FileReader(body);
                }

                _ = first;
            }

            var nextGain = new VolumeSampleProvider(nextReader.ToSampleProvider()) { Volume = Volume };
            nextOut = WasapiEndpoint.OpenPlayback(SpeakerId);
            nextOut.Init(nextGain);
            lock (gate)
            {
                if (ticket != generation)
                {
                    nextOut.Dispose();
                    nextReader.Dispose();
                    body?.Dispose();
                    http?.Dispose();
                    return false;
                }

                output = nextOut;
                reader = nextReader;
                streamHttp = http;
                streamBody = body;
                gain = nextGain;
                phase = HandsetAudioPhase.Playing;
                notice = string.Empty;
            }

            nextOut.Play();
            return true;
        }
        catch (Exception caught)
        {
            error = caught;
            nextOut?.Dispose();
            nextReader?.Dispose();
            body?.Dispose();
            http?.Dispose();
            return false;
        }
    }

    private void StopUnlocked()
    {
        Interlocked.Increment(ref generation);
        try
        {
            output?.Stop();
        }
        catch (Exception)
        {
        }

        output?.Dispose();
        reader?.Dispose();
        streamBody?.Dispose();
        streamHttp?.Dispose();
        output = null;
        reader = null;
        streamBody = null;
        streamHttp = null;
        gain = null;
        now = default;
    }
}
