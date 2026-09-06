using System.Net.Http;
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
    private HttpResponseMessage? streamResponse;
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
        if (string.IsNullOrEmpty(tune.StreamUrl))
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
            now = default;
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

                foreach (var playable in Unwrap(url))
                {
                    if (ticket != Volatile.Read(ref generation))
                    {
                        return;
                    }

                    if (TryStart(playable, ticket, out last))
                    {
                        return;
                    }
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
            notice = Explain(last);
        }
    }

    private bool TryStart(string url, int ticket, out Exception? error)
    {
        error = null;
        if (RadioPlaylist.LooksLikePlaylist(url))
        {
            error = new IOException("playlist");
            return false;
        }

        // Native Windows: Media Foundation (AAC, HLS, Icecast). Wine usually fails this.
        if (TryMediaFoundation(url, ticket, out error))
        {
            return true;
        }

        // Linux/Wine and MF misses: HTTP body decoded as MP3 without seeking.
        return TryHttpMp3(url, ticket, out error);
    }

    private bool TryMediaFoundation(string url, int ticket, out Exception? error)
    {
        error = null;
        WaveStream? nextReader = null;
        try
        {
            nextReader = new MediaFoundationReader(url);
            if (Arm(nextReader, ticket, null, null, null, out error))
            {
                return true;
            }

            nextReader.Dispose();
            nextReader = null;
            return false;
        }
        catch (Exception caught)
        {
            error = caught;
            nextReader?.Dispose();
            return false;
        }
    }

    private bool TryHttpMp3(string url, int ticket, out Exception? error)
    {
        error = null;
        HttpClient? http = null;
        HttpResponseMessage? response = null;
        Stream? body = null;
        WaveStream? nextReader = null;
        try
        {
            http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            http.DefaultRequestHeaders.TryAddWithoutValidation("Icy-MetaData", "0");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Linkpearl/0.1");
            response = HttpWire.Get(http, url, TimeSpan.FromSeconds(12));
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException("offline", null, response.StatusCode);
            }

            body = new PlainReadStream(HttpWire.Body(response));
            nextReader = new ForwardMp3Stream(body);
            if (Arm(nextReader, ticket, http, response, body, out error))
            {
                return true;
            }

            nextReader.Dispose();
            nextReader = null;
            body.Dispose();
            body = null;
            response.Dispose();
            response = null;
            http.Dispose();
            http = null;
            return false;
        }
        catch (Exception caught)
        {
            error = caught;
            nextReader?.Dispose();
            body?.Dispose();
            response?.Dispose();
            http?.Dispose();
            return false;
        }
    }

    private bool Arm(WaveStream nextReader, int ticket, HttpClient? http, HttpResponseMessage? response,
        Stream? body, out Exception? error)
    {
        error = null;
        IWavePlayer? nextOut = null;
        try
        {
            var nextGain = new VolumeSampleProvider(nextReader.ToSampleProvider()) { Volume = Volume };
            nextOut = WasapiEndpoint.OpenPlayback(SpeakerId);
            try
            {
                nextOut.Init(nextGain);
            }
            catch (Exception)
            {
                nextOut.Dispose();
                nextOut = WasapiEndpoint.OpenPlayback(SpeakerId);
                nextOut.Init(new SampleToWaveProvider16(nextGain));
            }

            lock (gate)
            {
                if (ticket != generation)
                {
                    nextOut.Dispose();
                    return false;
                }

                output = nextOut;
                reader = nextReader;
                streamHttp = http;
                streamResponse = response;
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
        streamResponse?.Dispose();
        streamHttp?.Dispose();
        output = null;
        reader = null;
        streamBody = null;
        streamResponse = null;
        streamHttp = null;
        gain = null;
    }

    private static IEnumerable<string> Unwrap(string url)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Linkpearl/0.1");
        foreach (var item in RadioPlaylist.Unwrap(url, http))
        {
            yield return item;
        }
    }

    private static string Explain(Exception? error)
    {
        if (error is HttpRequestException http &&
            (http.StatusCode == System.Net.HttpStatusCode.NotFound ||
             http.StatusCode == System.Net.HttpStatusCode.Gone))
        {
            return "This station is offline. Try another.";
        }

        var text = error?.Message ?? string.Empty;
        if (text.Contains("404", StringComparison.Ordinal) ||
            text.Contains("Not Available", StringComparison.OrdinalIgnoreCase))
        {
            return "This station is offline. Try another.";
        }

        if (text.Contains("403", StringComparison.Ordinal) ||
            text.Contains("401", StringComparison.Ordinal))
        {
            return "This station blocked the phone. Try another.";
        }

        return text.Length > 0 && text.Length < 90 ? text : "Could not start the stream.";
    }
}
