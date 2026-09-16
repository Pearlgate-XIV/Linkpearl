using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Camera;

internal static class PhotosChrome
{
    public static readonly Vector4 Tile = new(0.141f, 0.141f, 0.145f, 1f);
    public static readonly Vector4 Ink = new(0.973f, 0.973f, 0.973f, 1f);
    public static readonly Vector4 Mute = new(0.690f, 0.690f, 0.706f, 1f);
    public static readonly Vector4 Accent = new(0.259f, 0.522f, 0.957f, 1f);
    public static readonly Vector4 AccentInk = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 Check = new(0.180f, 0.800f, 0.443f, 1f);

    public static void Wheel(in AppletFrame frame, Rect area, ref float scroll, float content)
    {
        ScrollSlider.Apply(frame, area, ref scroll, content);
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

    public static void PickMark(in AppletFrame frame, Rect tile, bool on)
    {
        if (on)
        {
            frame.Paint.Fill(tile, new Vector4(0f, 0f, 0f, 0.28f));
        }

        var side = MathF.Min(frame.Units(16f), tile.Width * 0.28f);
        var pad = frame.Units(5f);
        var center = new Vector2(tile.Max.X - pad - side * 0.5f, tile.Min.Y + pad + side * 0.5f);
        frame.Paint.FillCircle(center, side * 0.5f, on ? Check : new Vector4(0.04f, 0.04f, 0.05f, 0.55f));
        frame.Paint.StrokeCircle(center, side * 0.5f, on ? AccentInk : Ink with { W = 0.88f },
            MathF.Max(1.1f, frame.Units(1.2f)));
        if (on)
        {
            frame.Paint.FillCircle(center, side * 0.18f, AccentInk);
        }
    }
}
