using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Linkpearl.Audio;

internal interface IDeviceTap : IDisposable
{
    WaveFormat Format { get; }

    event EventHandler<WaveInEventArgs>? DataAvailable;

    event EventHandler<StoppedEventArgs>? RecordingStopped;

    void Start();

    void Stop();
}

internal sealed class NaudioDeviceTap : IDeviceTap
{
    private readonly WasapiCapture inner;

    public NaudioDeviceTap(WasapiCapture inner)
    {
        this.inner = inner;
        inner.DataAvailable += (_, args) => DataAvailable?.Invoke(this, args);
        inner.RecordingStopped += (_, args) => RecordingStopped?.Invoke(this, args);
    }

    public WaveFormat Format => inner.WaveFormat;

    public event EventHandler<WaveInEventArgs>? DataAvailable;

    public event EventHandler<StoppedEventArgs>? RecordingStopped;

    public void Start() => inner.StartRecording();

    public void Stop()
    {
        try
        {
            inner.StopRecording();
        }
        catch (Exception)
        {
        }
    }

    public void Dispose() => inner.Dispose();
}

internal sealed class WaveInTap : IDeviceTap
{
    private readonly WaveInEvent wave;

    private WaveInTap(WaveInEvent wave)
    {
        this.wave = wave;
        wave.DataAvailable += (_, args) => DataAvailable?.Invoke(this, args);
        wave.RecordingStopped += (_, args) => RecordingStopped?.Invoke(this, args);
    }

    public static WaveInTap? OpenNamed(string friendlyName)
    {
        for (var index = 0; index < WaveIn.DeviceCount; index++)
        {
            var name = WaveIn.GetCapabilities(index).ProductName;
            if (name.Length == 0 ||
                !friendlyName.StartsWith(name, StringComparison.OrdinalIgnoreCase) &&
                !name.StartsWith(friendlyName, StringComparison.OrdinalIgnoreCase) &&
                !Overlap(friendlyName, name))
            {
                continue;
            }

            var wave = new WaveInEvent
            {
                DeviceNumber = index,
                WaveFormat = new WaveFormat(44100, 16, 2),
                BufferMilliseconds = 50,
            };
            return new WaveInTap(wave);
        }

        return null;
    }

    public WaveFormat Format => wave.WaveFormat;

    public event EventHandler<WaveInEventArgs>? DataAvailable;

    public event EventHandler<StoppedEventArgs>? RecordingStopped;

    public void Start() => wave.StartRecording();

    public void Stop()
    {
        try
        {
            wave.StopRecording();
        }
        catch (Exception)
        {
        }
    }

    public void Dispose() => wave.Dispose();

    private static bool Overlap(string left, string right)
    {
        var a = left.ToUpperInvariant();
        var b = right.ToUpperInvariant();
        return a.Contains("CABLE", StringComparison.Ordinal) && b.Contains("CABLE", StringComparison.Ordinal) &&
               (CableName.IsOutput(left) && CableName.IsOutput(right) ||
                CableName.IsInput(left) && CableName.IsInput(right) ||
                a.Contains("OUTPUT", StringComparison.Ordinal) && b.Contains("OUTPUT", StringComparison.Ordinal) ||
                a.Contains("OUT", StringComparison.Ordinal) && b.Contains("OUT", StringComparison.Ordinal));
    }
}

internal sealed class MixClientTap : IDeviceTap
{
    private readonly AudioClient client;
    private readonly AudioCaptureClient capture;
    private readonly int bytesPerFrame;
    private Thread? pump;
    private volatile bool run;

    private MixClientTap(AudioClient client, AudioCaptureClient capture, WaveFormat format)
    {
        this.client = client;
        this.capture = capture;
        Format = format;
        bytesPerFrame = Math.Max(1, format.BlockAlign);
    }

    public static MixClientTap Open(MMDevice device, bool loopback)
    {
        var convert = AudioClientStreamFlags.AutoConvertPcm | AudioClientStreamFlags.SrcDefaultQuality;
        var listen = loopback ? AudioClientStreamFlags.Loopback : AudioClientStreamFlags.None;
        var stereo = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
        try
        {
            return Bind(device, stereo, listen | convert);
        }
        catch (Exception first)
        {
            try
            {
                return Bind(device, ReadMix(device), listen);
            }
            catch (Exception)
            {
                try
                {
                    return Bind(device, ReadMix(device), listen | convert);
                }
                catch (Exception)
                {
                    throw first;
                }
            }
        }
    }

    private static WaveFormat ReadMix(MMDevice device)
    {
        var probe = device.AudioClient;
        try
        {
            return probe.MixFormat;
        }
        finally
        {
            probe.Dispose();
        }
    }

    private static MixClientTap Bind(MMDevice device, WaveFormat format, AudioClientStreamFlags flags)
    {
        var client = device.AudioClient;
        try
        {
            client.Initialize(AudioClientShareMode.Shared, flags, 10_000_000, 0, format, Guid.Empty);
            return new MixClientTap(client, client.AudioCaptureClient, format);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public WaveFormat Format { get; }

    public event EventHandler<WaveInEventArgs>? DataAvailable;

    public event EventHandler<StoppedEventArgs>? RecordingStopped;

    public void Start()
    {
        run = true;
        client.Start();
        pump = new Thread(Pump)
        {
            IsBackground = true,
            Name = "Linkpearl-device-tap",
        };
        pump.Start();
    }

    public void Stop()
    {
        run = false;
        try
        {
            client.Stop();
        }
        catch (Exception)
        {
        }

        pump?.Join(500);
        pump = null;
    }

    public void Dispose()
    {
        Stop();
        client.Dispose();
        RecordingStopped?.Invoke(this, new StoppedEventArgs());
    }

    private void Pump()
    {
        var scratch = new byte[bytesPerFrame * 2048];
        try
        {
            while (run)
            {
                var packet = capture.GetNextPacketSize();
                if (packet == 0)
                {
                    Thread.Sleep(6);
                    continue;
                }

                var pointer = capture.GetBuffer(out var frames, out var flags);
                var bytes = frames * bytesPerFrame;
                if (bytes > scratch.Length)
                {
                    scratch = new byte[bytes];
                }

                if (frames > 0 && (flags & AudioClientBufferFlags.Silent) == 0)
                {
                    Marshal.Copy(pointer, scratch, 0, bytes);
                }
                else
                {
                    Array.Clear(scratch, 0, bytes);
                }

                capture.ReleaseBuffer(frames);
                DataAvailable?.Invoke(this, new WaveInEventArgs(scratch, bytes));
            }
        }
        catch (Exception error)
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs(error));
        }
    }
}

internal static class CableName
{
    public static bool IsInput(string name)
    {
        var text = name.ToUpperInvariant();
        return text.Contains("CABLE", StringComparison.Ordinal) &&
               !IsOutput(name) &&
               (text.Contains("INPUT", StringComparison.Ordinal) ||
                text.Contains(" IN ", StringComparison.Ordinal) ||
                text.Contains("IN 16", StringComparison.Ordinal));
    }

    public static bool IsOutput(string name)
    {
        var text = name.ToUpperInvariant();
        return text.Contains("CABLE", StringComparison.Ordinal) &&
               (text.Contains("OUTPUT", StringComparison.Ordinal) ||
                text.Contains("OUT ", StringComparison.Ordinal) ||
                text.Contains("OUT 16", StringComparison.Ordinal));
    }

    public static MMDevice? FindOutput(MMDeviceEnumerator enumerator, string inputName)
    {
        var wantWide = inputName.Contains("16", StringComparison.OrdinalIgnoreCase);
        MMDevice? fallback = null;
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            var text = device.FriendlyName.ToUpperInvariant();
            if (!text.Contains("CABLE", StringComparison.Ordinal) ||
                !text.Contains("OUT", StringComparison.Ordinal))
            {
                device.Dispose();
                continue;
            }

            var wide = text.Contains("16", StringComparison.Ordinal);
            if (wantWide == wide)
            {
                fallback?.Dispose();
                return device;
            }

            fallback ??= device;
        }

        return fallback;
    }
}

internal static class AudioMix
{
    public static WaveFormat StereoFloat(int sampleRate) =>
        WaveFormat.CreateIeeeFloatWaveFormat(sampleRate > 0 ? sampleRate : 48000, 2);

    public static byte[] ToStereoFloat(byte[] buffer, int bytes, WaveFormat format)
    {
        var channels = Math.Max(1, format.Channels);
        var sampleBytes = Math.Max(1, format.BitsPerSample / 8);
        var block = format.BlockAlign > 0 ? format.BlockAlign : channels * sampleBytes;
        var frames = bytes / block;
        if (frames <= 0)
        {
            return [];
        }

        var dest = new byte[frames * 8];
        var floating = IsFloat(format);
        for (var frame = 0; frame < frames; frame++)
        {
            var origin = frame * block;
            var left = Sample(buffer, origin, sampleBytes, floating);
            var right = channels > 1
                ? Sample(buffer, origin + sampleBytes, sampleBytes, floating)
                : left;
            if (channels > 2)
            {
                var extra = 0f;
                for (var channel = 2; channel < channels; channel++)
                {
                    extra += Sample(buffer, origin + (channel * sampleBytes), sampleBytes, floating);
                }

                extra /= channels - 2;
                left = Math.Clamp((left + extra) * 0.7f, -1f, 1f);
                right = Math.Clamp((right + extra) * 0.7f, -1f, 1f);
            }

            Buffer.BlockCopy(BitConverter.GetBytes(left), 0, dest, frame * 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(right), 0, dest, frame * 8 + 4, 4);
        }

        return dest;
    }

    public static void Scale(byte[] floats, float gain)
    {
        if (Math.Abs(gain - 1f) < 0.001f)
        {
            return;
        }

        for (var index = 0; index + 4 <= floats.Length; index += 4)
        {
            var sample = Math.Clamp(BitConverter.ToSingle(floats, index) * gain, -1f, 1f);
            Buffer.BlockCopy(BitConverter.GetBytes(sample), 0, floats, index, 4);
        }
    }

    public static byte[] ToStereoPcm16(byte[] floats)
    {
        if (floats.Length == 0)
        {
            return [];
        }

        var pcm = new byte[floats.Length / 2];
        for (var index = 0; index + 4 <= floats.Length; index += 4)
        {
            var sample = Math.Clamp(BitConverter.ToSingle(floats, index), -1f, 1f);
            var quantized = (short)Math.Clamp((int)(sample * 32767f), short.MinValue, short.MaxValue);
            var dest = index / 2;
            pcm[dest] = (byte)quantized;
            pcm[dest + 1] = (byte)(quantized >> 8);
        }

        return pcm;
    }

    public static byte[] ToStereoPcm16(byte[] buffer, int bytes, WaveFormat format) =>
        ToStereoPcm16(ToStereoFloat(buffer, bytes, format));

    public static float Peak(byte[] buffer, int bytes, WaveFormat format)
    {
        var mixed = ToStereoFloat(buffer, bytes, format);
        var peak = 0f;
        for (var index = 0; index + 4 <= mixed.Length; index += 4)
        {
            peak = MathF.Max(peak, MathF.Abs(BitConverter.ToSingle(mixed, index)));
        }

        return Math.Clamp(peak, 0f, 1f);
    }

    private static bool IsFloat(WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            return true;
        }

        return format is WaveFormatExtensible extensible &&
               extensible.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
    }

    private static float Sample(byte[] buffer, int offset, int width, bool floating)
    {
        if (offset + width > buffer.Length)
        {
            return 0f;
        }

        if (floating && width >= 4)
        {
            return BitConverter.ToSingle(buffer, offset);
        }

        if (width >= 4)
        {
            return BitConverter.ToInt32(buffer, offset) / 2147483648f;
        }

        if (width == 3)
        {
            var value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
            if ((value & 0x800000) != 0)
            {
                value |= unchecked((int)0xFF000000);
            }

            return value / 8388608f;
        }

        if (width == 2)
        {
            return BitConverter.ToInt16(buffer, offset) / 32768f;
        }

        return buffer[offset] / 128f - 1f;
    }
}
