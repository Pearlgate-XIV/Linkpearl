using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
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
    private readonly HashSet<string> failed = new(StringComparer.Ordinal);
    private readonly HashSet<string> missing = new(StringComparer.Ordinal);

    public DalamudTextureSource(ITextureProvider provider)
    {
        this.provider = provider;
    }

    public ITextureHandle? GameIcon(uint iconId, bool highResolution = true) => null;

    public ITextureHandle? FromBytes(ReadOnlySpan<byte> data, string cacheKey) => null;

    public ITextureHandle? FromFile(string path)
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
}
