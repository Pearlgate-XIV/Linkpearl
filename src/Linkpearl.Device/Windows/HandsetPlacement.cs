using Dalamud.Bindings.ImGui;
using Linkpearl.Chassis;

namespace Linkpearl.Device.Windows;

// Two remembered corners: the open phone, and the miniature. Minimize and restore lerp
// between them so a phone left on the right comes back on the right.
public sealed class HandsetPlacement
{
    public Vector2 Open { get; private set; }

    public Vector2 Pocket { get; private set; }

    public bool HasOpen { get; private set; }

    public bool HasPocket { get; private set; }

    public bool Dirty { get; private set; }

    public void Load(bool hasOpen, float openX, float openY, bool hasPocket, float pocketX, float pocketY)
    {
        if (hasOpen && float.IsFinite(openX) && float.IsFinite(openY))
        {
            Open = new Vector2(openX, openY);
            HasOpen = true;
        }

        if (hasPocket && float.IsFinite(pocketX) && float.IsFinite(pocketY))
        {
            Pocket = new Vector2(pocketX, pocketY);
            HasPocket = true;
        }

        Dirty = false;
    }

    public void RememberOpen(Vector2 pos)
    {
        if (HasOpen && Nearly(Open, pos))
        {
            return;
        }

        Open = pos;
        HasOpen = true;
        Dirty = true;
    }

    public void RememberPocket(Vector2 pos)
    {
        if (HasPocket && Nearly(Pocket, pos))
        {
            return;
        }

        Pocket = pos;
        HasPocket = true;
        Dirty = true;
    }

    public void SeedPocketFromOpen(Vector2 fullSize, Vector2 faceSize)
    {
        if (HasPocket || !HasOpen)
        {
            return;
        }

        Pocket = Clamp(Open + (fullSize - faceSize) * 0.5f, faceSize);
        HasPocket = true;
        Dirty = true;
    }

    public Vector2 Current(float fold, Vector2 fullSize, Vector2 faceSize)
    {
        if (HasOpen && HasPocket)
        {
            return Vector2.Lerp(Open, Pocket, fold);
        }

        if (fold > 0.5f && HasPocket)
        {
            return Pocket;
        }

        return HasOpen ? Open : ImGui.GetWindowPos();
    }

    public void ClearDirty() => Dirty = false;

    public static Vector2 FaceSize(HandsetForm form, float pocketScale, float dip) =>
        FaceSize(form, HandsetCase.Pearl, pocketScale, dip);

    public static Vector2 FaceSize(HandsetForm form, HandsetCase casing, float pocketScale, float dip)
    {
        var height = 248f * pocketScale * dip;
        return new Vector2(height * ChassisCatalog.For(form, casing).Aspect, height);
    }

    public static Vector2 Clamp(Vector2 pos, Vector2 size)
    {
        var viewport = ImGui.GetMainViewport();
        var min = viewport.WorkPos;
        var max = viewport.WorkPos + viewport.WorkSize - size;
        var x = float.IsFinite(pos.X) ? pos.X : min.X;
        var y = float.IsFinite(pos.Y) ? pos.Y : min.Y;
        var maxX = MathF.Max(min.X, max.X);
        var maxY = MathF.Max(min.Y, max.Y);
        return new Vector2(Math.Clamp(x, min.X, maxX), Math.Clamp(y, min.Y, maxY));
    }

    private static bool Nearly(Vector2 a, Vector2 b) =>
        MathF.Abs(a.X - b.X) < 0.5f && MathF.Abs(a.Y - b.Y) < 0.5f;
}
