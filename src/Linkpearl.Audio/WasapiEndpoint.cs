using System.Globalization;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Linkpearl.Audio;

internal static class WasapiEndpoint
{
    public static IWavePlayer OpenPlayback(string deviceId, int latencyMs = 200)
    {
        if (TryWaveIndex(deviceId, "waveout:", out var waveIndex))
        {
            return new WaveOutEvent { DeviceNumber = waveIndex, DesiredLatency = latencyMs };
        }

        if (deviceId.Length > 0)
        {
            var device = FindExact(deviceId);
            if (device is not null)
            {
                var name = device.FriendlyName ?? string.Empty;
                try
                {
                    return OpenWasapi(device, latencyMs);
                }
                catch (Exception)
                {
                    TryDispose(device);
                }

                var wave = OpenWaveOut(name, latencyMs);
                if (wave is not null)
                {
                    return wave;
                }
            }
        }

        try
        {
            return new WasapiOut(AudioClientShareMode.Shared, latencyMs);
        }
        catch (Exception)
        {
            return new WaveOutEvent { DesiredLatency = latencyMs };
        }
    }

    public static MMDevice? FindExact(string deviceId)
    {
        if (deviceId.Length == 0 ||
            deviceId.StartsWith("waveout:", StringComparison.Ordinal) ||
            deviceId.StartsWith("wavein:", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDevice(deviceId);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string DefaultId(DataFlow flow)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            return device.ID ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static IWavePlayer OpenWasapi(MMDevice device, int latencyMs)
    {
        try
        {
            return new WasapiOut(device, AudioClientShareMode.Shared, false, latencyMs);
        }
        catch (Exception)
        {
            return new WasapiOut(device, AudioClientShareMode.Shared, true, latencyMs);
        }
    }

    private static IWavePlayer? OpenWaveOut(string friendlyName, int latencyMs)
    {
        if (friendlyName.Length == 0)
        {
            return null;
        }

        try
        {
            var count = WaveOut.DeviceCount;
            for (var index = 0; index < count; index++)
            {
                var product = WaveOut.GetCapabilities(index).ProductName;
                if (product.Length == 0)
                {
                    continue;
                }

                if (!friendlyName.StartsWith(product, StringComparison.OrdinalIgnoreCase) &&
                    !product.StartsWith(friendlyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return new WaveOutEvent { DeviceNumber = index, DesiredLatency = latencyMs };
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    private static bool TryWaveIndex(string deviceId, string prefix, out int index)
    {
        index = 0;
        if (!deviceId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(deviceId[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out index) &&
            index >= 0;
    }

    private static void TryDispose(MMDevice device)
    {
        try
        {
            device.Dispose();
        }
        catch (Exception)
        {
        }
    }
}
