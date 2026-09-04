using Dalamud.Bindings.ImGui;
using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Canvas.Painting;

public sealed class DalamudPaintSurface : IPaintSurface
{
    private readonly ImDrawListPtr drawList;
    private int clipDepth;

    public DalamudPaintSurface(ImDrawListPtr drawList)
    {
        this.drawList = drawList;
    }

    public void Fill(Rect area, Vector4 color) => Fill(area, color, 0f);

    public void Fill(Rect area, Vector4 color, float radius, Corner corners = Corner.All)
    {
        if (area.IsEmpty)
        {
            return;
        }

        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(color), radius, ToDrawFlags(corners));
    }

    public void Stroke(Rect area, Vector4 color, float thickness, float radius = 0f, Corner corners = Corner.All)
    {
        if (area.IsEmpty)
        {
            return;
        }

        drawList.AddRect(area.Min, area.Max, ImGui.GetColorU32(color), radius, ToDrawFlags(corners), thickness);
    }

    public void FillGradient(Rect area, Vector4 from, Vector4 to, GradientAxis axis)
    {
        if (area.IsEmpty)
        {
            return;
        }

        var fromColor = ImGui.GetColorU32(from);
        var toColor = ImGui.GetColorU32(to);
        if (axis == GradientAxis.Vertical)
        {
            drawList.AddRectFilledMultiColor(area.Min, area.Max, fromColor, fromColor, toColor, toColor);
        }
        else
        {
            drawList.AddRectFilledMultiColor(area.Min, area.Max, fromColor, toColor, toColor, fromColor);
        }
    }

    public void FillCorners(Rect area, Vector4 topLeft, Vector4 topRight, Vector4 bottomRight, Vector4 bottomLeft)
    {
        if (area.IsEmpty)
        {
            return;
        }

        drawList.AddRectFilledMultiColor(area.Min, area.Max, ImGui.GetColorU32(topLeft), ImGui.GetColorU32(topRight),
            ImGui.GetColorU32(bottomRight), ImGui.GetColorU32(bottomLeft));
    }

    public void FillCircle(Vector2 center, float radius, Vector4 color)
    {
        if (radius <= 0f)
        {
            return;
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color));
    }

    public void StrokeCircle(Vector2 center, float radius, Vector4 color, float thickness)
    {
        if (radius <= 0f)
        {
            return;
        }

        drawList.AddCircle(center, radius, ImGui.GetColorU32(color), 0, thickness);
    }

    public void FillOutsideRound(Rect area, float radius, Vector4 color)
    {
        var cap = MathF.Min(radius, MathF.Min(area.Width, area.Height) * 0.5f);
        if (area.IsEmpty || cap <= 0.5f)
        {
            return;
        }

        var ink = ImGui.GetColorU32(color);
        Cap(area.Min, new Vector2(1f, 1f), cap, ink);
        Cap(new Vector2(area.Max.X, area.Min.Y), new Vector2(-1f, 1f), cap, ink);
        Cap(new Vector2(area.Min.X, area.Max.Y), new Vector2(1f, -1f), cap, ink);
        Cap(area.Max, new Vector2(-1f, -1f), cap, ink);
    }

    private void Cap(Vector2 corner, Vector2 sign, float radius, uint color)
    {
        var center = corner + sign * radius;
        drawList.PathClear();
        drawList.PathLineTo(corner);
        drawList.PathLineTo(new Vector2(corner.X + sign.X * radius, corner.Y));
        const int steps = 12;
        for (var index = 0; index <= steps; index++)
        {
            var t = index / (float)steps;
            var start = sign.Y > 0f ? -MathF.PI * 0.5f : MathF.PI * 0.5f;
            var sweep = -sign.X * sign.Y * MathF.PI * 0.5f;
            var angle = start + sweep * t;
            drawList.PathLineTo(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }

        drawList.PathLineTo(new Vector2(corner.X, corner.Y + sign.Y * radius));
        drawList.PathFillConvex(color);
    }

    public void FillSquircle(Rect area, Vector4 color, float radius)
    {
        if (area.IsEmpty)
        {
            return;
        }

        var clamped = MathF.Min(radius, MathF.Min(area.Width, area.Height) * 0.5f);
        Fill(area, color, clamped, Corner.All);
    }

    public void FillSquircleGradient(Rect area, Vector4 topLeft, Vector4 topRight, Vector4 bottomRight,
        Vector4 bottomLeft, float radius)
    {
        if (area.IsEmpty)
        {
            return;
        }

        var clamped = MathF.Min(radius, MathF.Min(area.Width, area.Height) * 0.5f);
        const int bands = 18;
        var height = area.Height;
        if (height <= 1f || bands <= 1)
        {
            Fill(area, topLeft, clamped, Corner.All);
            return;
        }

        for (var index = 0; index < bands; index++)
        {
            var start = index / (float)bands;
            var end = (index + 1) / (float)bands;
            var mid = (start + end) * 0.5f;
            var slice = new Rect(
                new Vector2(area.Min.X, area.Min.Y + height * start),
                new Vector2(area.Max.X, area.Min.Y + height * end));
            var left = Mix(topLeft, bottomLeft, mid);
            var right = Mix(topRight, bottomRight, mid);
            var corners = index == 0 ? Corner.Top : index == bands - 1 ? Corner.Bottom : Corner.None;
            if (corners == Corner.None)
            {
                FillGradient(slice, left, right, GradientAxis.Horizontal);
                continue;
            }

            Fill(slice, Mix(left, right, 0.5f), clamped, corners);
            FillGradient(slice, left with { W = left.W * 0.85f }, right with { W = right.W * 0.85f },
                GradientAxis.Horizontal);
        }
    }

    public void FillAppTile(Rect area, Vector4 color)
    {
        if (area.IsEmpty)
        {
            return;
        }

        PathAppTile(area);
        drawList.PathFillConvex(ImGui.GetColorU32(color));
    }

    public void FillAppTile(Rect area, Vector4 from, Vector4 to)
    {
        if (area.IsEmpty)
        {
            return;
        }

        const int steps = 72;
        var center = area.Center;
        var width = MathF.Max(area.Width, 1f);
        var height = MathF.Max(area.Height, 1f);
        var previous = AppTilePoint(area, 0f);
        for (var index = 1; index <= steps; index++)
        {
            var next = AppTilePoint(area, index / (float)steps);
            var mid = (previous + next + center) / 3f;
            var slant = Math.Clamp(((mid.X - area.Min.X) / width + (mid.Y - area.Min.Y) / height) * 0.5f, 0f, 1f);
            drawList.AddTriangleFilled(center, previous, next, ImGui.GetColorU32(Mix(from, to, slant)));
            previous = next;
        }
    }

    public void StrokeAppTile(Rect area, Vector4 color, float thickness)
    {
        if (area.IsEmpty || thickness <= 0f)
        {
            return;
        }

        PathAppTile(area);
        drawList.PathStroke(ImGui.GetColorU32(color), ImDrawFlags.Closed, thickness);
    }

    // One UI / S25 mask: a superellipse, slightly square, almost a circle.
    private void PathAppTile(Rect area)
    {
        const int steps = 72;
        drawList.PathClear();
        for (var index = 0; index <= steps; index++)
        {
            drawList.PathLineTo(AppTilePoint(area, index / (float)steps));
        }
    }

    private static Vector2 AppTilePoint(Rect area, float turn)
    {
        const float exponent = 4.8f;
        var angle = turn * MathF.PI * 2f;
        var power = 2f / exponent;
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        var x = area.Width * 0.5f * MathF.CopySign(MathF.Pow(MathF.Abs(cos), power), cos);
        var y = area.Height * 0.5f * MathF.CopySign(MathF.Pow(MathF.Abs(sin), power), sin);
        return area.Center + new Vector2(x, y);
    }

    private static Vector4 Mix(Vector4 from, Vector4 to, float amount) =>
        from + (to - from) * Math.Clamp(amount, 0f, 1f);

    public void Line(Vector2 from, Vector2 to, Vector4 color, float thickness) =>
        drawList.AddLine(from, to, ImGui.GetColorU32(color), thickness);

    public void StrokeArc(Vector2 center, float radius, float start, float sweep, Vector4 color, float thickness)
    {
        if (radius <= 0.5f || MathF.Abs(sweep) < 0.001f)
        {
            return;
        }

        const int steps = 14;
        drawList.PathClear();
        for (var index = 0; index <= steps; index++)
        {
            var angle = start + sweep * (index / (float)steps);
            drawList.PathLineTo(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }

        drawList.PathStroke(ImGui.GetColorU32(color), ImDrawFlags.None, thickness);
    }

    public void Polyline(ReadOnlySpan<Vector2> points, Vector4 color, float thickness, bool closed)
    {
        if (points.Length < 2)
        {
            return;
        }

        var colorU32 = ImGui.GetColorU32(color);
        for (var index = 0; index < points.Length - 1; index++)
        {
            drawList.AddLine(points[index], points[index + 1], colorU32, thickness);
        }

        if (closed)
        {
            drawList.AddLine(points[^1], points[0], colorU32, thickness);
        }
    }

    public void Glow(Rect area, Vector4 color, float radius, float spread)
    {
        var steps = Math.Clamp((int)(spread / 2f), 3, 10);
        for (var step = steps; step >= 1; step--)
        {
            var fraction = (float)step / steps;
            var expanded = area.Expand(spread * fraction);
            var alpha = color.W * (1f - fraction) * (1f - fraction) / steps;
            Fill(expanded, color with { W = alpha }, radius + spread * fraction);
        }
    }

    public void Image(ITextureHandle texture, Rect area, Vector4 tint) =>
        Image(texture, area, Vector2.Zero, Vector2.One, tint);

    public void Image(ITextureHandle texture, Rect area, Vector2 uvMin, Vector2 uvMax, Vector4 tint)
    {
        if (!texture.IsReady || area.IsEmpty)
        {
            return;
        }

        drawList.AddImage(new ImTextureID(texture.Handle), area.Min, area.Max, uvMin, uvMax, ImGui.GetColorU32(tint));
    }

    public void ImageRounded(ITextureHandle texture, Rect area, Vector2 uvMin, Vector2 uvMax, Vector4 tint,
        float rounding)
    {
        if (!texture.IsReady || area.IsEmpty)
        {
            return;
        }

        var radius = MathF.Min(rounding, MathF.Min(area.Width, area.Height) * 0.5f);
        drawList.AddImageRounded(new ImTextureID(texture.Handle), area.Min, area.Max, uvMin, uvMax,
            ImGui.GetColorU32(tint), radius);
    }

    public void PushClip(Rect area)
    {
        if (area.Width <= 0f || area.Height <= 0f || !float.IsFinite(area.Min.X) || !float.IsFinite(area.Min.Y) ||
            !float.IsFinite(area.Max.X) || !float.IsFinite(area.Max.Y))
        {
            drawList.PushClipRect(area.Min, area.Min, true);
        }
        else
        {
            drawList.PushClipRect(area.Min, area.Max, true);
        }

        clipDepth++;
    }

    public void PopClip()
    {
        if (clipDepth <= 0)
        {
            return;
        }

        clipDepth--;
        drawList.PopClipRect();
    }

    private static ImDrawFlags ToDrawFlags(Corner corners)
    {
        if (corners == Corner.All)
        {
            return ImDrawFlags.RoundCornersAll;
        }

        if (corners == Corner.None)
        {
            return ImDrawFlags.RoundCornersNone;
        }

        var flags = ImDrawFlags.RoundCornersNone;
        if ((corners & Corner.TopLeft) != 0)
        {
            flags |= ImDrawFlags.RoundCornersTopLeft;
        }

        if ((corners & Corner.TopRight) != 0)
        {
            flags |= ImDrawFlags.RoundCornersTopRight;
        }

        if ((corners & Corner.BottomLeft) != 0)
        {
            flags |= ImDrawFlags.RoundCornersBottomLeft;
        }

        if ((corners & Corner.BottomRight) != 0)
        {
            flags |= ImDrawFlags.RoundCornersBottomRight;
        }

        return flags;
    }
}
