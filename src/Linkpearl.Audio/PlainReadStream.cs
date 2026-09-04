namespace Linkpearl.Audio;

// Network bodies throw on Length, Seek, and ReadTimeout. NAudio will poke all three.
internal sealed class PlainReadStream : Stream
{
    private readonly Stream inner;
    private long read;

    public PlainReadStream(Stream inner) => this.inner = inner;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override bool CanTimeout => false;

    public override long Length => read;

    public override long Position
    {
        get => read;
        set { }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = inner.Read(buffer, offset, count);
        if (n > 0)
        {
            read += n;
        }

        return n;
    }

    public override int Read(Span<byte> buffer)
    {
        var n = inner.Read(buffer);
        if (n > 0)
        {
            read += n;
        }

        return n;
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => read;

    public override void SetLength(long value) { }

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
