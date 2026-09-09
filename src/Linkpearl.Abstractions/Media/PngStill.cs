using System.IO.Compression;

namespace Linkpearl.Media;

public static class PngStill
{
    private static readonly uint[] Crc = BuildCrc();

    public static byte[] Encode(int width, int height, byte[] bgra)
    {
        var rows = new byte[(width * 4 + 1) * height];
        var o = 0;
        for (var y = 0; y < height; y++)
        {
            rows[o++] = 0;
            var src = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                rows[o++] = bgra[src + 2];
                rows[o++] = bgra[src + 1];
                rows[o++] = bgra[src];
                rows[o++] = bgra[src + 3];
                src += 4;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true))
        {
            zlib.Write(rows);
        }

        var idat = compressed.ToArray();
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WriteChunk(png, "IHDR", Ihdr(width, height));
        WriteChunk(png, "IDAT", idat);
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static byte[] Ihdr(int width, int height)
    {
        var body = new byte[13];
        WriteU32(body, 0, (uint)width);
        WriteU32(body, 4, (uint)height);
        body[8] = 8;
        body[9] = 6;
        return body;
    }

    private static void WriteChunk(Stream png, string type, byte[] data)
    {
        var name = System.Text.Encoding.ASCII.GetBytes(type);

        WriteU32Stream(png, (uint)data.Length);
        var crcSrc = new byte[name.Length + data.Length];
        name.CopyTo(crcSrc, 0);
        data.CopyTo(crcSrc, name.Length);
        png.Write(name);
        png.Write(data);
        WriteU32Stream(png, Crc32(crcSrc));
    }

    private static void WriteU32(byte[] dest, int offset, uint value)
    {
        dest[offset] = (byte)(value >> 24);
        dest[offset + 1] = (byte)(value >> 16);
        dest[offset + 2] = (byte)(value >> 8);
        dest[offset + 3] = (byte)value;
    }

    private static void WriteU32Stream(Stream png, uint value)
    {
        png.WriteByte((byte)(value >> 24));
        png.WriteByte((byte)(value >> 16));
        png.WriteByte((byte)(value >> 8));
        png.WriteByte((byte)value);
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        for (var i = 0; i < data.Length; i++)
        {
            crc = Crc[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrc()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
