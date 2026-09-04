using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Linkpearl.Media;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Canvas.Painting;

// Rent-for-this-frame handle over Dalamud's shared file textures. Dispose is a no-op: the wrap
// is owned by ISharedImmediateTexture and is only valid for the current UI frame.
internal sealed class FrameTexture : ITextureHandle
{
    public nint Handle { get; set; }

    public Vector2 Size { get; set; }

    public bool IsReady { get; set; }

    public void Dispose()
    {
    }

    public void Clear()
    {
        Handle = 0;
        Size = Vector2.Zero;
        IsReady = false;
    }

    public void Bind(IDalamudTextureWrap wrap)
    {
        Handle = (nint)wrap.Handle.Handle;
        Size = new Vector2(wrap.Width, wrap.Height);
        IsReady = wrap.Width > 0 && wrap.Height > 0;
    }
}

public sealed class DalamudTextureSource : ITextureSource
{
    private readonly ITextureProvider provider;
    private readonly Dictionary<string, ISharedImmediateTexture> shared = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FrameTexture> frames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FileMips> mips = new(StringComparer.Ordinal);
    private readonly HashSet<string> failed = new(StringComparer.Ordinal);
    private readonly HashSet<string> missing = new(StringComparer.Ordinal);
    private readonly HashSet<string> mipFailed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CoverUv> opaque = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IDalamudTextureWrap> rawWraps = new(StringComparer.Ordinal);

    public DalamudTextureSource(ITextureProvider provider)
    {
        this.provider = provider;
    }

    public ITextureHandle? GameIcon(uint iconId, bool highResolution = true)
    {
        if (iconId == 0)
        {
            return null;
        }

        var key = highResolution ? "icon:" + iconId + ":hi" : "icon:" + iconId;
        if (failed.Contains(key))
        {
            return null;
        }

        if (!shared.TryGetValue(key, out var texture))
        {
            texture = provider.GetFromGameIcon(new GameIconLookup(iconId, false, highResolution));
            shared[key] = texture;
        }

        if (!texture.TryGetWrap(out var wrap, out var error))
        {
            if (error is not null)
            {
                failed.Add(key);
            }

            return null;
        }

        if (!frames.TryGetValue(key, out var frame))
        {
            frame = new FrameTexture();
            frames[key] = frame;
        }

        frame.Bind(wrap);
        return frame.IsReady ? frame : null;
    }

    public CoverUv FileOpaqueUv(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return CoverUv.Full;
        }

        if (opaque.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var measured = MeasureOpaque(path);
        opaque[path] = measured;
        return measured;
    }

    public ITextureHandle? FromBytes(ReadOnlySpan<byte> data, string cacheKey)
    {
        if (data.Length == 0 || string.IsNullOrWhiteSpace(cacheKey) || failed.Contains(cacheKey))
        {
            return null;
        }

        if (!rawWraps.TryGetValue(cacheKey, out var wrap))
        {
            try
            {
                using var stream = new MemoryStream(data.ToArray(), false);
                using var loaded = (Bitmap)Image.FromStream(stream);
                using var canvas = loaded.Clone(new Rectangle(0, 0, loaded.Width, loaded.Height),
                    PixelFormat.Format32bppArgb);
                var packed = PackBitmap(canvas);
                var spec = RawImageSpecification.Bgra32(canvas.Width, canvas.Height);
                wrap = provider.CreateFromRaw(spec, packed, "linkpearl-bytes:" + cacheKey);
                if (wrap is null)
                {
                    failed.Add(cacheKey);
                    return null;
                }

                rawWraps[cacheKey] = wrap;
            }
            catch (ArgumentException)
            {
                failed.Add(cacheKey);
                return null;
            }
            catch (IOException)
            {
                failed.Add(cacheKey);
                return null;
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                failed.Add(cacheKey);
                return null;
            }
        }

        if (!frames.TryGetValue(cacheKey, out var frame))
        {
            frame = new FrameTexture();
            frames[cacheKey] = frame;
        }

        frame.Bind(wrap);
        return frame.IsReady ? frame : null;
    }

    private static byte[] PackBitmap(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bits = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = bitmap.Width * 4;
            var packed = new byte[row * bitmap.Height];
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(bits.Scan0 + y * bits.Stride, packed, y * row, row);
            }

            return packed;
        }
        finally
        {
            bitmap.UnlockBits(bits);
        }
    }

    public ITextureHandle? FromFile(string path) => FromFile(path, Vector2.Zero);

    public ITextureHandle? FromFile(string path, Vector2 destPixels)
    {
        if (destPixels.X > 1f && destPixels.Y > 1f)
        {
            var crisp = FromMip(path, destPixels);
            if (crisp is not null)
            {
                return crisp;
            }
        }

        return FromSharedFile(path);
    }

    private ITextureHandle? FromSharedFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || failed.Contains(path) || missing.Contains(path))
        {
            return null;
        }

        if (!shared.TryGetValue(path, out var texture))
        {
            if (!File.Exists(path))
            {
                missing.Add(path);
                return null;
            }

            texture = provider.GetFromFileAbsolute(path);
            shared[path] = texture;
        }

        if (!texture.TryGetWrap(out var wrap, out var error))
        {
            if (error is not null)
            {
                failed.Add(path);
            }

            return null;
        }

        if (!frames.TryGetValue(path, out var frame))
        {
            frame = new FrameTexture();
            frames[path] = frame;
        }

        frame.Bind(wrap);
        return frame.IsReady ? frame : null;
    }

    private ITextureHandle? FromMip(string path, Vector2 destPixels)
    {
        if (string.IsNullOrWhiteSpace(path) || mipFailed.Contains(path) || missing.Contains(path))
        {
            return null;
        }

        if (!mips.TryGetValue(path, out var chain))
        {
            if (!File.Exists(path))
            {
                missing.Add(path);
                return null;
            }

            try
            {
                chain = FileMips.Build(provider, path);
            }
            catch (IOException)
            {
                mipFailed.Add(path);
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                mipFailed.Add(path);
                return null;
            }
            catch (ArgumentException)
            {
                mipFailed.Add(path);
                return null;
            }
            catch (InvalidOperationException)
            {
                mipFailed.Add(path);
                return null;
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                mipFailed.Add(path);
                return null;
            }

            if (chain.Levels.Count == 0)
            {
                mipFailed.Add(path);
                return null;
            }

            mips[path] = chain;
        }

        var want = MathF.Max(destPixels.X, destPixels.Y);
        var chosen = chain.Levels[0];
        for (var index = 0; index < chain.Levels.Count; index++)
        {
            var level = chain.Levels[index];
            if (level.Width + 0.01f >= want && level.Height + 0.01f >= want)
            {
                chosen = level;
                continue;
            }

            break;
        }

        var key = path + "@" + chosen.Width.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!frames.TryGetValue(key, out var frame))
        {
            frame = new FrameTexture();
            frames[key] = frame;
        }

        frame.Bind(chosen.Wrap);
        return frame.IsReady ? frame : null;
    }

    private static CoverUv MeasureOpaque(string path)
    {
        if (!File.Exists(path))
        {
            return CoverUv.Full;
        }

        try
        {
            using var loaded = (Bitmap)Image.FromFile(path);
            using var canvas = loaded.Clone(new Rectangle(0, 0, loaded.Width, loaded.Height),
                PixelFormat.Format32bppArgb);
            return OpaqueUv(canvas);
        }
        catch (IOException)
        {
            return CoverUv.Full;
        }
        catch (UnauthorizedAccessException)
        {
            return CoverUv.Full;
        }
        catch (ArgumentException)
        {
            return CoverUv.Full;
        }
        catch (ExternalException)
        {
            return CoverUv.Full;
        }
    }

    private static CoverUv OpaqueUv(Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        if (width <= 0 || height <= 0)
        {
            return CoverUv.Full;
        }

        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        var rect = new Rectangle(0, 0, width, height);
        var bits = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                var src = (byte*)bits.Scan0;
                for (var y = 0; y < height; y++)
                {
                    var row = src + y * bits.Stride;
                    for (var x = 0; x < width; x++)
                    {
                        if (row[x * 4 + 3] <= 12)
                        {
                            continue;
                        }

                        if (x < minX)
                        {
                            minX = x;
                        }

                        if (y < minY)
                        {
                            minY = y;
                        }

                        if (x > maxX)
                        {
                            maxX = x;
                        }

                        if (y > maxY)
                        {
                            maxY = y;
                        }
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bits);
        }

        if (maxX < minX || maxY < minY)
        {
            return CoverUv.Full;
        }

        var pad = 2;
        minX = Math.Max(0, minX - pad);
        minY = Math.Max(0, minY - pad);
        maxX = Math.Min(width - 1, maxX + pad);
        maxY = Math.Min(height - 1, maxY + pad);
        return new CoverUv(
            new Vector2(minX / (float)width, minY / (float)height),
            new Vector2((maxX + 1) / (float)width, (maxY + 1) / (float)height));
    }

    private sealed class FileMips
    {
        public List<MipLevel> Levels { get; } = new();

        public static FileMips Build(ITextureProvider provider, string path)
        {
            var chain = new FileMips();
            using var loaded = (Bitmap)Image.FromFile(path);
            using var canvas = loaded.Clone(new Rectangle(0, 0, loaded.Width, loaded.Height),
                PixelFormat.Format32bppArgb);
            var current = canvas;
            var owned = false;
            try
            {
                while (true)
                {
                    var wrap = Upload(provider, current, path);
                    if (wrap is null)
                    {
                        break;
                    }

                    chain.Levels.Add(new MipLevel(wrap, current.Width, current.Height));
                    if (current.Width <= 32 && current.Height <= 32)
                    {
                        break;
                    }

                    var next = Halve(current);
                    if (owned)
                    {
                        current.Dispose();
                    }

                    current = next;
                    owned = true;
                }
            }
            finally
            {
                if (owned)
                {
                    current.Dispose();
                }
            }

            return chain;
        }

        private static IDalamudTextureWrap? Upload(ITextureProvider provider, Bitmap bitmap, string path)
        {
            var packed = PackBgra(bitmap);
            var spec = RawImageSpecification.Bgra32(bitmap.Width, bitmap.Height);
            return provider.CreateFromRaw(spec, packed,
                "linkpearl:" + Path.GetFileName(path) + ":" + bitmap.Width);
        }

        private static byte[] PackBgra(Bitmap bitmap)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bits = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var stride = bits.Stride;
                var row = bitmap.Width * 4;
                var packed = new byte[row * bitmap.Height];
                for (var y = 0; y < bitmap.Height; y++)
                {
                    Marshal.Copy(bits.Scan0 + y * stride, packed, y * row, row);
                }

                return packed;
            }
            finally
            {
                bitmap.UnlockBits(bits);
            }
        }

        private static Bitmap Halve(Bitmap source)
        {
            var width = Math.Max(1, source.Width / 2);
            var height = Math.Max(1, source.Height / 2);
            var dest = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var srcRect = new Rectangle(0, 0, source.Width, source.Height);
            var dstRect = new Rectangle(0, 0, width, height);
            var srcBits = source.LockBits(srcRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var dstBits = dest.LockBits(dstRect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    var src = (byte*)srcBits.Scan0;
                    var dst = (byte*)dstBits.Scan0;
                    for (var y = 0; y < height; y++)
                    {
                        var sy = Math.Min(y * 2, source.Height - 1);
                        var sy2 = Math.Min(sy + 1, source.Height - 1);
                        var dstRow = dst + y * dstBits.Stride;
                        var srcA = src + sy * srcBits.Stride;
                        var srcB = src + sy2 * srcBits.Stride;
                        for (var x = 0; x < width; x++)
                        {
                            var sx = Math.Min(x * 2, source.Width - 1);
                            var sx2 = Math.Min(sx + 1, source.Width - 1);
                            Acc(srcA + sx * 4, srcA + sx2 * 4, srcB + sx * 4, srcB + sx2 * 4, dstRow + x * 4);
                        }
                    }
                }
            }
            finally
            {
                dest.UnlockBits(dstBits);
                source.UnlockBits(srcBits);
            }

            return dest;
        }

        private static unsafe void Acc(byte* a, byte* b, byte* c, byte* d, byte* dest)
        {
            var aA = a[3] / 255f;
            var bA = b[3] / 255f;
            var cA = c[3] / 255f;
            var dA = d[3] / 255f;
            var alpha = (aA + bA + cA + dA) * 0.25f;
            if (alpha <= 0.0001f)
            {
                dest[0] = 0;
                dest[1] = 0;
                dest[2] = 0;
                dest[3] = 0;
                return;
            }

            dest[0] = (byte)Math.Clamp(((a[0] * aA + b[0] * bA + c[0] * cA + d[0] * dA) * 0.25f / alpha) + 0.5f, 0f, 255f);
            dest[1] = (byte)Math.Clamp(((a[1] * aA + b[1] * bA + c[1] * cA + d[1] * dA) * 0.25f / alpha) + 0.5f, 0f, 255f);
            dest[2] = (byte)Math.Clamp(((a[2] * aA + b[2] * bA + c[2] * cA + d[2] * dA) * 0.25f / alpha) + 0.5f, 0f, 255f);
            dest[3] = (byte)Math.Clamp(alpha * 255f + 0.5f, 0f, 255f);
        }
    }

    private readonly struct MipLevel
    {
        public MipLevel(IDalamudTextureWrap wrap, int width, int height)
        {
            Wrap = wrap;
            Width = width;
            Height = height;
        }

        public IDalamudTextureWrap Wrap { get; }

        public int Width { get; }

        public int Height { get; }
    }
}
