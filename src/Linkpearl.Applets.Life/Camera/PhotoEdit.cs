using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Linkpearl.Applets.Life.Camera;

internal static class PhotoEdit
{
    public static bool Write(string source, string dest, float x, float y, float width, float height, int turns)
    {
        if (!File.Exists(source))
        {
            return false;
        }

        try
        {
            using var image = Load(source);
            ApplyTurns(image, turns);
            using var cut = Crop(image, x, y, width, height);
            return Save(cut, dest);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return false;
        }
    }

    public static byte[]? Preview(string source, int turns)
    {
        if (!File.Exists(source))
        {
            return null;
        }

        try
        {
            using var image = Load(source);
            ApplyTurns(image, turns);
            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return null;
        }
    }

    private static Bitmap Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes, writable: false);
        using var loaded = (Bitmap)Image.FromStream(stream);
        return loaded.Clone(new Rectangle(0, 0, loaded.Width, loaded.Height), PixelFormat.Format32bppArgb);
    }

    private static void ApplyTurns(Image image, int turns)
    {
        var step = ((turns % 4) + 4) % 4;
        if (step == 1)
        {
            image.RotateFlip(RotateFlipType.Rotate90FlipNone);
        }
        else if (step == 2)
        {
            image.RotateFlip(RotateFlipType.Rotate180FlipNone);
        }
        else if (step == 3)
        {
            image.RotateFlip(RotateFlipType.Rotate270FlipNone);
        }
    }

    private static Bitmap Crop(Image image, float x, float y, float width, float height)
    {
        var left = (int)MathF.Floor(Math.Clamp(x, 0f, 1f) * image.Width);
        var top = (int)MathF.Floor(Math.Clamp(y, 0f, 1f) * image.Height);
        var right = (int)MathF.Ceiling(Math.Clamp(x + width, 0f, 1f) * image.Width);
        var bottom = (int)MathF.Ceiling(Math.Clamp(y + height, 0f, 1f) * image.Height);
        left = Math.Clamp(left, 0, image.Width - 1);
        top = Math.Clamp(top, 0, image.Height - 1);
        right = Math.Clamp(right, left + 1, image.Width);
        bottom = Math.Clamp(bottom, top + 1, image.Height);
        var box = new Rectangle(left, top, right - left, bottom - top);
        var dest = new Bitmap(box.Width, box.Height, PixelFormat.Format32bppArgb);
        using var paint = Graphics.FromImage(dest);
        paint.InterpolationMode = InterpolationMode.HighQualityBicubic;
        paint.PixelOffsetMode = PixelOffsetMode.HighQuality;
        paint.DrawImage(image, new Rectangle(0, 0, dest.Width, dest.Height), box, GraphicsUnit.Pixel);
        return dest;
    }

    private static bool Save(Image image, string dest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? ".");
        var temp = dest + ".tmp";
        var kind = Path.GetExtension(dest);
        if (string.Equals(kind, ".jpg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, ".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            image.Save(temp, ImageFormat.Jpeg);
        }
        else
        {
            image.Save(temp, ImageFormat.Png);
        }

        if (File.Exists(dest))
        {
            File.Delete(dest);
        }

        File.Move(temp, dest);
        return true;
    }
}
