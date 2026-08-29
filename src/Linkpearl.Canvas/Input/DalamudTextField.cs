using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using Linkpearl.Canvas.Text;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;

namespace Linkpearl.Canvas.Input;

public sealed class DalamudTextField : ITextField
{
    private readonly HandsetFontService fonts;
    private readonly List<char> strokes = new();
    private readonly HashSet<VirtualKey> held = new();
    private int backHold;
    private bool primed;
    private string ownerId = string.Empty;

    public DalamudTextField(HandsetFontService fonts)
    {
        this.fonts = fonts;
    }

    public bool Capturing { get; private set; }

    public void BeginFrame() => Capturing = false;

    public void Release()
    {
        ownerId = string.Empty;
        Capturing = false;
        primed = false;
        backHold = 0;
        strokes.Clear();
        held.Clear();
    }

    public void EndFrame()
    {
        if (Capturing)
        {
            return;
        }

        primed = false;
        backHold = 0;
        ownerId = string.Empty;
        strokes.Clear();
        held.Clear();
    }

    public void Harvest(IKeyState keys)
    {
        if (!primed)
        {
            RememberHeld(keys);
            primed = true;
            return;
        }
        var control = Pressed(keys, VirtualKey.CONTROL) || Pressed(keys, VirtualKey.LCONTROL) ||
                      Pressed(keys, VirtualKey.RCONTROL);
        var shift = Pressed(keys, VirtualKey.SHIFT) || Pressed(keys, VirtualKey.LSHIFT) ||
                    Pressed(keys, VirtualKey.RSHIFT);

        if (control)
        {
            if (Edge(keys, VirtualKey.V))
            {
                var paste = ImGui.GetClipboardText();
                if (!string.IsNullOrEmpty(paste))
                {
                    foreach (var glyph in paste)
                    {
                        if (!char.IsControl(glyph))
                        {
                            strokes.Add(glyph);
                        }
                    }
                }
            }

            RememberHeld(keys);
            return;
        }

        if (Edge(keys, VirtualKey.RETURN))
        {
            strokes.Add('\n');
        }

        if (Pressed(keys, VirtualKey.BACK))
        {
            backHold++;
            if (Edge(keys, VirtualKey.BACK) || (backHold > 10 && backHold % 2 == 0))
            {
                strokes.Add('\b');
            }
        }
        else
        {
            backHold = 0;
        }

        foreach (var (key, glyph) in Glyphs(shift))
        {
            if (Edge(keys, key))
            {
                strokes.Add(glyph);
            }
        }

        RememberHeld(keys);
    }

    public string Draw(string id, Rect area, string value, string placeholder) =>
        Draw(id, area, value, placeholder, 128, out _);

    public string Draw(string id, Rect area, string value, string placeholder, int maxLength, out bool submitted)
    {
        submitted = false;
        if (area.Width < 1f || area.Height < 1f)
        {
            return value;
        }

        using var font = fonts.Handle(FontRole.Body).Push();
        var line = ImGui.GetTextLineHeight();
        var padY = MathF.Max((area.Height - line) * 0.5f, 0f);
        var padX = MathF.Max(area.Height * 0.18f, 8f);

        ImGui.PushClipRect(area.Min, area.Max, true);
        ImGui.SetCursorScreenPos(area.Min);
        ImGui.SetNextItemWidth(area.Width);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Border, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, MathF.Min(area.Height * 0.35f, 12f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(padX, padY));

        var current = value;
        var enter = ImGui.InputTextWithHint($"##{id}", placeholder, ref current, Math.Max(maxLength, 1),
            ImGuiInputTextFlags.EnterReturnsTrue);
        var overField = ImGui.IsMouseHoveringRect(area.Min, area.Max, true);

        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(6);
        ImGui.PopClipRect();

        if (ownerId == id && strokes.Count > 0)
        {
            current = Apply(value, strokes, Math.Max(maxLength, 1), out var harvestedEnter);
            enter = enter || harvestedEnter;
            strokes.Clear();
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (overField)
            {
                ownerId = id;
            }
            else if (ownerId == id)
            {
                ownerId = string.Empty;
                ImGui.SetCursorScreenPos(new Vector2(-10000f, -10000f));
                ImGui.SetKeyboardFocusHere();
                ImGui.InvisibleButton("##linkpearl-blur", new Vector2(1f, 1f));
            }
        }

        if (enter)
        {
            ownerId = string.Empty;
        }

        var focused = ownerId == id;
        if (focused)
        {
            Capturing = true;
        }

        submitted = enter;
        Paint(area, current, placeholder, focused, padX, padY, line);
        return current;
    }

    private static void Paint(Rect area, string current, string placeholder, bool focused, float padX, float padY,
        float line)
    {
        var draw = ImGui.GetForegroundDrawList();
        draw.PushClipRect(area.Min, area.Max, true);
        var empty = current.Length == 0;
        var shown = empty ? placeholder : current;
        var inner = MathF.Max(area.Width - padX * 2f, 1f);
        var textWidth = ImGui.CalcTextSize(shown).X;
        var scroll = !empty && textWidth > inner ? textWidth - inner : 0f;
        var origin = area.Min + new Vector2(padX - scroll, padY);
        var color = empty
            ? new Vector4(0.96f, 0.96f, 0.97f, 0.42f)
            : new Vector4(0.96f, 0.96f, 0.97f, 1f);
        draw.AddText(origin, ImGui.GetColorU32(color), shown);

        if (focused && (int)(ImGui.GetTime() * 2d) % 2 == 0)
        {
            var caretX = empty ? origin.X : origin.X + textWidth + 1f;
            draw.AddLine(new Vector2(caretX, origin.Y), new Vector2(caretX, origin.Y + line),
                ImGui.GetColorU32(new Vector4(0.96f, 0.96f, 0.97f, 1f)), 1.2f);
        }

        draw.PopClipRect();
    }

    private static string Apply(string value, List<char> incoming, int maxLength, out bool submitted)
    {
        submitted = false;
        var current = value;
        for (var index = 0; index < incoming.Count; index++)
        {
            var glyph = incoming[index];
            if (glyph == '\n')
            {
                submitted = true;
                continue;
            }

            if (glyph == '\b')
            {
                if (current.Length > 0)
                {
                    current = current[..^1];
                }

                continue;
            }

            if (current.Length < maxLength)
            {
                current += glyph;
            }
        }

        return current;
    }

    private bool Edge(IKeyState keys, VirtualKey key) => Pressed(keys, key) && !held.Contains(key);

    private static bool Pressed(IKeyState keys, VirtualKey key) =>
        keys.IsVirtualKeyValid(key) && keys[key];

    private void RememberHeld(IKeyState keys)
    {
        held.Clear();
        foreach (var key in keys.GetValidVirtualKeys())
        {
            if (keys[key])
            {
                held.Add(key);
            }
        }
    }

    private static IEnumerable<(VirtualKey Key, char Glyph)> Glyphs(bool shift)
    {
        yield return (VirtualKey.SPACE, ' ');
        for (var index = 0; index < 26; index++)
        {
            var letter = (char)((shift ? 'A' : 'a') + index);
            yield return ((VirtualKey)((int)VirtualKey.A + index), letter);
        }

        const string digits = "0123456789";
        const string shifted = ")!@#$%^&*(";
        for (var index = 0; index < 10; index++)
        {
            yield return ((VirtualKey)((int)VirtualKey.KEY_0 + index), shift ? shifted[index] : digits[index]);
            yield return ((VirtualKey)((int)VirtualKey.NUMPAD0 + index), digits[index]);
        }

        yield return (VirtualKey.OEM_1, shift ? ':' : ';');
        yield return (VirtualKey.OEM_PLUS, shift ? '+' : '=');
        yield return (VirtualKey.OEM_COMMA, shift ? '<' : ',');
        yield return (VirtualKey.OEM_MINUS, shift ? '_' : '-');
        yield return (VirtualKey.OEM_PERIOD, shift ? '>' : '.');
        yield return (VirtualKey.OEM_2, shift ? '?' : '/');
        yield return (VirtualKey.OEM_3, shift ? '~' : '`');
        yield return (VirtualKey.OEM_4, shift ? '{' : '[');
        yield return (VirtualKey.OEM_5, shift ? '|' : '\\');
        yield return (VirtualKey.OEM_6, shift ? '}' : ']');
        yield return (VirtualKey.OEM_7, shift ? '"' : '\'');
    }
}
