using NAudio.Wave;
using NLayer.NAudioSupport;

namespace Linkpearl.Audio;

// Forward-only MP3 for Icecast/Shoutcast. Mp3FileReader seeks, which HTTP bodies refuse.
internal sealed class ForwardMp3Stream : WaveStream
{
    private readonly Stream source;
    private readonly byte[] pcm = new byte[32_768];
    private Mp3FrameDecompressor? decoder;
    private WaveFormat format = new WaveFormat(44100, 16, 2);
    private int pcmCount;
    private int pcmOffset;
    private bool done;

    public ForwardMp3Stream(Stream source)
    {
        this.source = source;
        Prime();
    }

    public override bool CanSeek => false;

    public override WaveFormat WaveFormat => format;

    public override long Length => 0;

    public override long Position
    {
        get => 0;
        set { }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var written = 0;
        while (written < count && !done)
        {
            if (pcmOffset >= pcmCount && !Fill())
            {
                break;
            }

            var take = Math.Min(count - written, pcmCount - pcmOffset);
            Buffer.BlockCopy(pcm, pcmOffset, buffer, offset + written, take);
            pcmOffset += take;
            written += take;
        }

        return written;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            decoder?.Dispose();
            decoder = null;
        }

        base.Dispose(disposing);
    }

    private void Prime()
    {
        Fill();
    }

    private bool Fill()
    {
        pcmOffset = 0;
        pcmCount = 0;
        while (!done)
        {
            Mp3Frame? frame;
            try
            {
                frame = Mp3Frame.LoadFromStream(source);
            }
            catch (EndOfStreamException)
            {
                done = true;
                return false;
            }
            catch (IOException)
            {
                done = true;
                return false;
            }

            if (frame is null)
            {
                done = true;
                return false;
            }

            decoder ??= new Mp3FrameDecompressor(new Mp3WaveFormat(
                frame.SampleRate,
                frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                frame.FrameLength,
                frame.BitRate));
            format = decoder.OutputFormat;
            pcmCount = decoder.DecompressFrame(frame, pcm, 0);
            if (pcmCount > 0)
            {
                return true;
            }
        }

        return false;
    }
}
