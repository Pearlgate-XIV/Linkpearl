namespace Linkpearl.Media;

public readonly record struct GifCel(byte[] Bgra, int DelayMs);

/// <summary>GIF raster. Wine/libgdiplus often cannot decode GIF, so chat stills go through here.</summary>
public static class GifStill
{
    private const int MaxCels = 48;

    public static bool IsGif(ReadOnlySpan<byte> data) =>
        data.Length >= 13 && data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F';

    public static bool TryUnpack(ReadOnlySpan<byte> data, out int width, out int height, out byte[] bgra)
    {
        if (!TryUnpackReel(data, out width, out height, out var cels) || cels.Length == 0)
        {
            bgra = [];
            return false;
        }

        bgra = cels[0].Bgra;
        return true;
    }

    public static bool TryUnpackReel(ReadOnlySpan<byte> data, out int width, out int height, out GifCel[] cels)
    {
        width = 0;
        height = 0;
        cels = [];
        if (!IsGif(data))
        {
            return false;
        }

        try
        {
            return UnpackReel(data, out width, out height, out cels);
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (IndexOutOfRangeException)
        {
            return false;
        }
    }

    private static bool UnpackReel(ReadOnlySpan<byte> data, out int width, out int height, out GifCel[] cels)
    {
        width = 0;
        height = 0;
        cels = [];
        var cursor = 6;
        width = ReadU16(data, ref cursor);
        height = ReadU16(data, ref cursor);
        if (width <= 0 || height <= 0 || width > 2048 || height > 2048)
        {
            return false;
        }

        var packed = data[cursor++];
        var bgIndex = data[cursor++];
        cursor++;
        var gctSize = (packed & 0x80) != 0 ? 1 << ((packed & 7) + 1) : 0;
        var gct = ReadTable(data, ref cursor, gctSize);
        var canvas = new byte[width * height * 4];
        if (gct.Length >= (bgIndex + 1) * 3)
        {
            Fill(canvas, gct, bgIndex);
        }

        var trans = -1;
        var dispose = 0;
        var delayMs = 100;
        var reel = new List<GifCel>(8);
        while (cursor < data.Length && reel.Count < MaxCels)
        {
            var tag = data[cursor++];
            if (tag == 0x3B)
            {
                break;
            }

            if (tag == 0x21)
            {
                if (cursor >= data.Length)
                {
                    break;
                }

                var label = data[cursor++];
                if (label == 0xF9 && cursor + 5 <= data.Length)
                {
                    var len = data[cursor++];
                    var flags = data[cursor++];
                    var delayCs = ReadU16(data, ref cursor);
                    var transIndex = data[cursor++];
                    trans = (flags & 1) != 0 ? transIndex : -1;
                    dispose = (flags >> 2) & 7;
                    delayMs = delayCs <= 1 ? 100 : delayCs * 10;
                    if (len > 4)
                    {
                        cursor += len - 4;
                    }

                    if (cursor < data.Length && data[cursor] == 0)
                    {
                        cursor++;
                    }

                    continue;
                }

                SkipBlocks(data, ref cursor);
                continue;
            }

            if (tag != 0x2C)
            {
                return reel.Count > 0;
            }

            var left = ReadU16(data, ref cursor);
            var top = ReadU16(data, ref cursor);
            var iw = ReadU16(data, ref cursor);
            var ih = ReadU16(data, ref cursor);
            var ip = data[cursor++];
            var lctSize = (ip & 0x80) != 0 ? 1 << ((ip & 7) + 1) : 0;
            var table = lctSize > 0 ? ReadTable(data, ref cursor, lctSize) : gct;
            if (table.Length < 3 || cursor >= data.Length)
            {
                break;
            }

            var minCode = data[cursor++];
            var compressed = ReadBlocks(data, ref cursor);
            var index = Lzw(compressed, minCode, iw * ih);
            byte[]? backup = null;
            if (dispose == 3)
            {
                backup = (byte[])canvas.Clone();
            }

            Blit(canvas, width, height, left, top, iw, ih, index, table, trans, (ip & 0x40) != 0);
            reel.Add(new GifCel((byte[])canvas.Clone(), delayMs));
            if (dispose == 2)
            {
                Restore(canvas, width, height, left, top, iw, ih, gct, bgIndex);
            }
            else if (dispose == 3 && backup is not null)
            {
                backup.CopyTo(canvas, 0);
            }

            trans = -1;
            dispose = 0;
            delayMs = 100;
        }

        cels = reel.ToArray();
        return cels.Length > 0;
    }

    private static void Fill(byte[] canvas, byte[] table, int index)
    {
        var o = index * 3;
        var b = table[o + 2];
        var g = table[o + 1];
        var r = table[o];
        for (var i = 0; i < canvas.Length; i += 4)
        {
            canvas[i] = b;
            canvas[i + 1] = g;
            canvas[i + 2] = r;
            canvas[i + 3] = 255;
        }
    }

    private static void Restore(byte[] canvas, int cw, int ch, int left, int top, int iw, int ih, byte[] table,
        int bgIndex)
    {
        byte b = 0;
        byte g = 0;
        byte r = 0;
        byte a = 0;
        if (table.Length >= (bgIndex + 1) * 3)
        {
            var o = bgIndex * 3;
            b = table[o + 2];
            g = table[o + 1];
            r = table[o];
            a = 255;
        }

        for (var y = 0; y < ih; y++)
        {
            var dy = top + y;
            if (dy < 0 || dy >= ch)
            {
                continue;
            }

            for (var x = 0; x < iw; x++)
            {
                var dx = left + x;
                if (dx < 0 || dx >= cw)
                {
                    continue;
                }

                var dest = (dy * cw + dx) * 4;
                canvas[dest] = b;
                canvas[dest + 1] = g;
                canvas[dest + 2] = r;
                canvas[dest + 3] = a;
            }
        }
    }

    private static void Blit(byte[] canvas, int cw, int ch, int left, int top, int iw, int ih, byte[] index,
        byte[] table, int trans, bool interlace)
    {
        var n = 0;
        for (var row = 0; row < ih; row++)
        {
            var y = interlace ? InterlaceY(row, ih) : row;
            var dy = top + y;
            if (dy < 0 || dy >= ch)
            {
                n += iw;
                continue;
            }

            for (var x = 0; x < iw; x++)
            {
                if (n >= index.Length)
                {
                    return;
                }

                var color = index[n++];
                if (color == trans)
                {
                    continue;
                }

                var dx = left + x;
                if (dx < 0 || dx >= cw || color * 3 + 2 >= table.Length)
                {
                    continue;
                }

                var dest = (dy * cw + dx) * 4;
                var o = color * 3;
                canvas[dest] = table[o + 2];
                canvas[dest + 1] = table[o + 1];
                canvas[dest + 2] = table[o];
                canvas[dest + 3] = 255;
            }
        }
    }

    private static int InterlaceY(int row, int height)
    {
        var count = 0;
        foreach (var y in PassYs(height))
        {
            if (count == row)
            {
                return y;
            }

            count++;
        }

        return Math.Min(row, height - 1);
    }

    private static IEnumerable<int> PassYs(int height)
    {
        for (var y = 0; y < height; y += 8)
        {
            yield return y;
        }

        for (var y = 4; y < height; y += 8)
        {
            yield return y;
        }

        for (var y = 2; y < height; y += 4)
        {
            yield return y;
        }

        for (var y = 1; y < height; y += 2)
        {
            yield return y;
        }
    }

    private static byte[] Lzw(byte[] src, int minCode, int pixels)
    {
        var clear = 1 << minCode;
        var eoi = clear + 1;
        var codeSize = minCode + 1;
        var next = eoi + 1;
        var prefix = new int[4096];
        var suffix = new byte[4096];
        for (var i = 0; i < clear; i++)
        {
            prefix[i] = -1;
            suffix[i] = (byte)i;
        }

        var bits = 0;
        var hold = 0;
        var pos = 0;
        var prev = -1;
        var dest = new byte[Math.Max(pixels, 1)];
        var written = 0;
        var stack = new byte[4096];

        int NextCode()
        {
            while (bits < codeSize)
            {
                if (pos >= src.Length)
                {
                    return -1;
                }

                hold |= src[pos++] << bits;
                bits += 8;
            }

            var code = hold & ((1 << codeSize) - 1);
            hold >>= codeSize;
            bits -= codeSize;
            return code;
        }

        void Reset()
        {
            codeSize = minCode + 1;
            next = eoi + 1;
            prev = -1;
        }

        byte First(int code)
        {
            var walk = code;
            var guard = 0;
            while (prefix[walk] >= 0 && guard++ < 4096)
            {
                walk = prefix[walk];
            }

            return suffix[walk];
        }

        void Emit(int code)
        {
            var sp = 0;
            var walk = code;
            var guard = 0;
            while (walk >= 0 && sp < stack.Length && guard++ < 4096)
            {
                stack[sp++] = suffix[walk];
                walk = prefix[walk];
            }

            while (sp > 0 && written < dest.Length)
            {
                dest[written++] = stack[--sp];
            }
        }

        while (written < pixels)
        {
            var code = NextCode();
            if (code < 0 || code == eoi)
            {
                break;
            }

            if (code == clear)
            {
                Reset();
                continue;
            }

            if (prev < 0)
            {
                Emit(code);
                prev = code;
                continue;
            }

            var kWk = code == next;
            if (kWk)
            {
                Emit(prev);
                if (written < dest.Length)
                {
                    dest[written++] = First(prev);
                }
            }
            else if (code < next)
            {
                Emit(code);
            }
            else
            {
                break;
            }

            if (next < 4096)
            {
                prefix[next] = prev;
                suffix[next] = First(kWk ? prev : code);
                next++;
                if (next == (1 << codeSize) && codeSize < 12)
                {
                    codeSize++;
                }
            }

            prev = kWk ? next - 1 : code;
        }

        return dest;
    }

    private static byte[] ReadTable(ReadOnlySpan<byte> data, ref int cursor, int colors)
    {
        var bytes = colors * 3;
        if (colors <= 0 || cursor + bytes > data.Length)
        {
            return [];
        }

        var table = data.Slice(cursor, bytes).ToArray();
        cursor += bytes;
        return table;
    }

    private static byte[] ReadBlocks(ReadOnlySpan<byte> data, ref int cursor)
    {
        var buffer = new List<byte>(4096);
        while (cursor < data.Length)
        {
            var len = data[cursor++];
            if (len == 0)
            {
                break;
            }

            if (cursor + len > data.Length)
            {
                break;
            }

            for (var i = 0; i < len; i++)
            {
                buffer.Add(data[cursor++]);
            }
        }

        return buffer.ToArray();
    }

    private static void SkipBlocks(ReadOnlySpan<byte> data, ref int cursor)
    {
        while (cursor < data.Length)
        {
            var len = data[cursor++];
            if (len == 0)
            {
                return;
            }

            cursor += len;
        }
    }

    private static int ReadU16(ReadOnlySpan<byte> data, ref int cursor)
    {
        var value = data[cursor] | (data[cursor + 1] << 8);
        cursor += 2;
        return value;
    }
}
