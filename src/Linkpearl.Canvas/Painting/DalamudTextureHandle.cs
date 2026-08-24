using Dalamud.Interface.Textures.TextureWraps;
using Linkpearl.Painting;

namespace Linkpearl.Canvas.Painting;

internal sealed class DalamudTextureHandle : ITextureHandle
{
    private readonly IDalamudTextureWrap wrap;
    private bool disposed;

    internal DalamudTextureHandle(IDalamudTextureWrap wrap)
    {
        this.wrap = wrap;
    }

    public nint Handle => (nint)wrap.Handle.Handle;

    public Vector2 Size => new(wrap.Width, wrap.Height);

    public bool IsReady => !disposed;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        wrap.Dispose();
    }
}
