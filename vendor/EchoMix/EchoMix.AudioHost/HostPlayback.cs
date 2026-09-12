using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace EchoMix.AudioHost;

internal static class HostPlayback
{
    public static IWavePlayer Create(int bufferMs)
    {
        if (WineProbe.IsWine)
        {
            Console.WriteLine("[EchoMix.AudioHost] Wine detected — using WaveOut instead of WASAPI.");
            return new WaveOutEvent { DesiredLatency = Math.Max(bufferMs, 80) };
        }

        return new WasapiOut(AudioClientShareMode.Shared, bufferMs);
    }

    public static void Init(IWavePlayer player, ISampleProvider sample)
    {
        if (player is WasapiOut wasapi)
        {
            wasapi.Init(sample);
            return;
        }

        player.Init(new SampleToWaveProvider(sample));
    }
}
