using Linkpearl.Geometry;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Badges;

public static class BadgeArt
{
    public static string Absolute(HostPaths paths, string iconAsset)
    {
        if (string.IsNullOrWhiteSpace(iconAsset))
        {
            return string.Empty;
        }

        return paths.Asset(Path.Combine("Icons", iconAsset.Replace('/', Path.DirectorySeparatorChar)));
    }

    public static bool TryDraw(IPaintSurface paint, ITextureSource textures, HostPaths paths, Rect area,
        string iconAsset)
    {
        var path = Absolute(paths, iconAsset);
        if (path.Length == 0)
        {
            return false;
        }

        var native = textures.FromFile(path);
        if (native is not { IsReady: true })
        {
            return false;
        }

        var box = CoverFit.Snapped(CoverFit.InscribedSquare(area));
        var pad = MathF.Max(1f, MathF.Min(box.Width, box.Height) * 0.12f);
        var inner = box.Inset(pad);
        if (inner.Width < 1f || inner.Height < 1f)
        {
            return false;
        }

        var crop = textures.FileOpaqueUv(path);
        var content = new Vector2(
            native.Size.X * MathF.Max(0.02f, crop.Max.X - crop.Min.X),
            native.Size.Y * MathF.Max(0.02f, crop.Max.Y - crop.Min.Y));
        var dest = CoverFit.Snapped(CoverFit.Contained(content, inner));
        var face = textures.FromFile(path, dest.Size) ?? native;
        if (!face.IsReady)
        {
            return false;
        }

        paint.Image(face, dest, crop.Min, crop.Max, Vector4.One);
        return true;
    }

    public static bool TryDrawFill(IPaintSurface paint, ITextureSource textures, HostPaths paths, Rect area,
        string iconAsset)
    {
        var path = Absolute(paths, iconAsset);
        if (path.Length == 0)
        {
            return false;
        }

        var native = textures.FromFile(path);
        if (native is not { IsReady: true })
        {
            return false;
        }

        var box = CoverFit.Snapped(CoverFit.InscribedSquare(area));
        if (box.Width < 1f || box.Height < 1f)
        {
            return false;
        }

        var opaque = textures.FileOpaqueUv(path);
        var content = new Vector2(
            native.Size.X * MathF.Max(0.02f, opaque.Max.X - opaque.Min.X),
            native.Size.Y * MathF.Max(0.02f, opaque.Max.Y - opaque.Min.Y));
        var cover = CoverFit.Uv(content, box.Size);
        var min = new Vector2(
            opaque.Min.X + (opaque.Max.X - opaque.Min.X) * cover.Min.X,
            opaque.Min.Y + (opaque.Max.Y - opaque.Min.Y) * cover.Min.Y);
        var max = new Vector2(
            opaque.Min.X + (opaque.Max.X - opaque.Min.X) * cover.Max.X,
            opaque.Min.Y + (opaque.Max.Y - opaque.Min.Y) * cover.Max.Y);
        var face = textures.FromFile(path, box.Size) ?? native;
        if (!face.IsReady)
        {
            return false;
        }

        paint.Image(face, box, min, max, Vector4.One);
        return true;
    }
}
