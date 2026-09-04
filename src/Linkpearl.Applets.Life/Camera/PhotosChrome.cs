using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Camera;

internal static class PhotosChrome
{
    public static readonly Vector4 Ground = new(0.055f, 0.055f, 0.058f, 1f);
    public static readonly Vector4 Tile = new(0.141f, 0.141f, 0.145f, 1f);
    public static readonly Vector4 Ink = new(0.973f, 0.973f, 0.973f, 1f);
    public static readonly Vector4 Mute = new(0.690f, 0.690f, 0.706f, 1f);
    public static readonly Vector4 Accent = new(0.259f, 0.522f, 0.957f, 1f);
    public static readonly Vector4 AccentInk = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 Check = new(0.180f, 0.800f, 0.443f, 1f);

    public static void Fill(in AppletFrame frame) => frame.Paint.Fill(frame.Content, Ground);

    public static void Wheel(in AppletFrame frame, Rect area, ref float scroll, float content)
    {
        if (frame.Input.IsHovering(area) && frame.Input.ScrollDelta != 0f)
        {
            scroll = Math.Clamp(scroll - frame.Input.ScrollDelta * frame.Units(18f), 0f,
                MathF.Max(0f, content - area.Height));
        }
    }

    public static void Cover(in AppletFrame frame, Rect area, ITextureHandle texture)
    {
        if (area.IsEmpty || texture.Size.X <= 0f || texture.Size.Y <= 0f)
        {
            return;
        }

        var image = texture.Size.X / texture.Size.Y;
        var box = area.Width / MathF.Max(area.Height, 1f);
        Vector2 uvMin;
        Vector2 uvMax;
        if (image > box)
        {
            var crop = (1f - box / image) * 0.5f;
            uvMin = new Vector2(crop, 0f);
            uvMax = new Vector2(1f - crop, 1f);
        }
        else
        {
            var crop = (1f - image / box) * 0.5f;
            uvMin = new Vector2(0f, crop);
            uvMax = new Vector2(1f, 1f - crop);
        }

        frame.Paint.Image(texture, area, uvMin, uvMax, Vector4.One);
    }

    public static void Contain(in AppletFrame frame, Rect area, ITextureHandle texture)
    {
        if (area.IsEmpty || texture.Size.X <= 0f || texture.Size.Y <= 0f)
        {
            return;
        }

        var image = texture.Size.X / texture.Size.Y;
        var box = area.Width / MathF.Max(area.Height, 1f);
        Rect dest;
        if (image > box)
        {
            var height = area.Width / image;
            var top = area.Min.Y + (area.Height - height) * 0.5f;
            dest = Rect.FromSize(new Vector2(area.Min.X, top), new Vector2(area.Width, height));
        }
        else
        {
            var width = area.Height * image;
            var left = area.Min.X + (area.Width - width) * 0.5f;
            dest = Rect.FromSize(new Vector2(left, area.Min.Y), new Vector2(width, area.Height));
        }

        frame.Paint.Image(texture, dest, Vector4.One);
    }

    public static void Placeholder(in AppletFrame frame, Rect area)
    {
        frame.Paint.Fill(area, Tile);
        frame.Text.DrawIn(area, "◇", new TextStyle(FontRole.Title, Mute, TextAlign.Center));
    }
}
