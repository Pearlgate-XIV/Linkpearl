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

    public void FillSquircle(Rect area, Vector4 color, float radius)
    {
        if (area.IsEmpty)
        {
            return;
        }

        var clamped = MathF.Min(radius, MathF.Min(area.Width, area.Height) * 0.5f);
        Fill(area, color, clamped, Corner.All);
    }

    public void Line(Vector2 from, Vector2 to, Vector4 color, float thickness) =>
        drawList.AddLine(from, to, ImGui.GetColorU32(color), thickness);

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

    public void PushClip(Rect area)
    {
        drawList.PushClipRect(area.Min, area.Max, true);
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
