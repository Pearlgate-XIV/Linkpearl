using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using Linkpearl.Canvas.Text;
using Linkpearl.Emoji;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Canvas.Input;

public sealed class DalamudTextField : ITextField
{
    private readonly HandsetFontService fonts;
    private readonly List<char> strokes = new();
    private readonly HashSet<VirtualKey> held = new();
    private int backHold;
    private bool primed;
    private string ownerId = string.Empty;
    private string pendingFocus = string.Empty;
    private string caretOwner = string.Empty;
    private double caretSince;
    private readonly Dictionary<string, int> carets = new(StringComparer.Ordinal);
    private readonly List<string> wireFaces = new();
    private IPaintSurface? paint;
    private ITextPainter? text;
    private ITextureSource? textures;
    private HostPaths? paths;

    public DalamudTextField(HandsetFontService fonts)
    {
        this.fonts = fonts;
    }

    public bool Capturing { get; private set; }

    public void BeginFrame() => Capturing = false;

    public void Release()
    {
        ownerId = string.Empty;
        pendingFocus = string.Empty;
        caretOwner = string.Empty;
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
        pendingFocus = string.Empty;
        caretOwner = string.Empty;
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
        var caps = Pressed(keys, VirtualKey.CAPITAL);

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

        foreach (var (key, glyph) in Glyphs(shift, caps))
        {
            if (Edge(keys, key))
            {
                strokes.Add(glyph);
            }
        }

        RememberHeld(keys);
    }

    public bool Owns(string id) => ownerId == id;

    public void Dress(IPaintSurface paint, ITextPainter text, ITextureSource textures, HostPaths paths)
    {
        this.paint = paint;
        this.text = text;
        this.textures = textures;
        this.paths = paths;
    }

    public void Focus(string id)
    {
        ownerId = id;
        pendingFocus = id;
        Capturing = true;
    }

    public string Insert(string id, string value, string text)
    {
        if (text.Length == 0)
        {
            return value;
        }

        var at = carets.TryGetValue(id, out var caret) ? caret : value.Length;
        at = EmojiBits.ClampIndex(value, at);
        ownerId = id;
        pendingFocus = id;
        Capturing = true;
        var next = EmojiBits.Insert(value, at, text);
        carets[id] = at + text.Length;
        return next.Length <= 4000 ? next : next[..4000];
    }

    public string Draw(string id, Rect area, string value, string placeholder) =>
        Draw(id, area, value, placeholder, 128, out _, false);

    public string Draw(string id, Rect area, string value, string placeholder, int maxLength, out bool submitted) =>
        Draw(id, area, value, placeholder, maxLength, out submitted, false);

    public string Draw(string id, Rect area, string value, string placeholder, int maxLength, out bool submitted,
        bool retainFocus) =>
        Draw(id, area, value, placeholder, maxLength, out submitted, retainFocus, false);

    public string Draw(string id, Rect area, string value, string placeholder, int maxLength, out bool submitted,
        bool retainFocus, bool secret)
    {
        submitted = false;
        if (area.Width < 1f || area.Height < 1f)
        {
            return value;
        }

        if (retainFocus && ownerId != id && ImGui.IsKeyPressed(ImGuiKey.Enter) &&
            !ImGui.GetIO().WantTextInput)
        {
            ownerId = id;
            pendingFocus = id;
        }

        using var font = fonts.Handle(FontRole.Body).Push();
        var line = ImGui.GetTextLineHeight();
        var padY = area.Height > line * 2.2f
            ? MathF.Max(area.Height * 0.06f, 6f)
            : MathF.Max((area.Height - line) * 0.5f, 0f);
        var padX = MathF.Max(area.Height * 0.18f, 8f);
        var ink = new Vector4(0.96f, 0.96f, 0.97f, 1f);
        var native = ownerId == id;

        if (pendingFocus == id)
        {
            ImGui.SetKeyboardFocusHere();
            pendingFocus = string.Empty;
        }

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

        var flags = ImGuiInputTextFlags.EnterReturnsTrue;
        if (secret)
        {
            flags |= ImGuiInputTextFlags.Password;
        }

        var current = secret ? value : EmojiBits.ToWire(value, wireFaces);
        var enter = ImGui.InputTextWithHint($"##{id}", placeholder, ref current,
            Math.Max(maxLength + (secret ? 0 : wireFaces.Count * 8), 1), flags);
        var overField = ImGui.IsMouseHoveringRect(area.Min, area.Max, true);
        var itemActive = ImGui.IsItemActive() || ImGui.IsItemFocused();

        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(6);
        ImGui.PopClipRect();

        if (itemActive)
        {
            ownerId = id;
            strokes.Clear();
        }
        else if (ownerId == id && strokes.Count > 0)
        {
            current = EmojiBits.ToWire(Apply(value, strokes, Math.Max(maxLength, 1), out var harvestedEnter),
                wireFaces);
            enter = enter || harvestedEnter;
            strokes.Clear();
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !DalamudInputProbe.OtherWindowAbove())
        {
            if (overField)
            {
                ownerId = id;
                pendingFocus = id;
            }
            else if (ownerId == id)
            {
                ownerId = string.Empty;
                pendingFocus = string.Empty;
                ImGui.SetCursorScreenPos(new Vector2(-10000f, -10000f));
                ImGui.SetKeyboardFocusHere();
                ImGui.InvisibleButton("##linkpearl-blur", new Vector2(1f, 1f));
            }
        }

        if (enter && !retainFocus)
        {
            ownerId = string.Empty;
        }
        else if (enter && retainFocus)
        {
            ownerId = id;
            pendingFocus = id;
        }

        var focused = ownerId == id;
        if (focused)
        {
            Capturing = true;
        }

        submitted = enter;
        var shown = secret ? current : EmojiBits.FromWire(current, wireFaces);
        if (!secret && EmojiBits.HasFace(value) && !EmojiBits.HasFace(shown) && current.Contains('?'))
        {
            shown = value;
        }

        if (shown.Length > maxLength)
        {
            shown = secret ? shown[..maxLength] : shown[..EmojiBits.ClampIndex(shown, maxLength)];
        }

        if (shown.Length != value.Length)
        {
            carets[id] = shown.Length;
        }

        Paint(area, secret && shown.Length > 0 ? new string('•', shown.Length) : shown, placeholder, focused, padX,
            padY, line, ink);
        _ = native;
        return shown;
    }

    public string Write(string id, Rect area, string value, string placeholder, int maxLength)
    {
        if (area.Width < 1f || area.Height < 1f)
        {
            return value;
        }

        using var font = fonts.Handle(FontRole.Body).Push();
        var pad = MathF.Max(area.Height * 0.04f, 8f);
        var ink = new Vector4(0.96f, 0.96f, 0.97f, 1f);
        var native = ownerId == id;

        if (pendingFocus == id)
        {
            ImGui.SetKeyboardFocusHere();
            pendingFocus = string.Empty;
        }

        ImGui.PushClipRect(area.Min, area.Max, true);
        ImGui.SetCursorScreenPos(area.Min);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, ink);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, ink with { W = 0.42f });
        ImGui.PushStyleColor(ImGuiCol.Border, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(pad * 0.35f, pad * 0.25f));

        var current = EmojiBits.ToWire(value, wireFaces);
        ImGui.InputTextMultiline("##" + id, ref current, Math.Max(maxLength + wireFaces.Count * 8, 1),
            new Vector2(area.Width, area.Height), ImGuiInputTextFlags.AllowTabInput);
        var overField = ImGui.IsMouseHoveringRect(area.Min, area.Max, true);
        var itemActive = ImGui.IsItemActive() || ImGui.IsItemFocused();

        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(6);
        ImGui.PopClipRect();

        if (itemActive)
        {
            ownerId = id;
            strokes.Clear();
            Capturing = true;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !DalamudInputProbe.OtherWindowAbove())
        {
            if (overField)
            {
                ownerId = id;
                pendingFocus = id;
            }
            else if (ownerId == id)
            {
                ownerId = string.Empty;
                pendingFocus = string.Empty;
            }
        }

        if (current.Length == 0 && !itemActive && ownerId != id)
        {
            var draw = ImGui.GetWindowDrawList();
            draw.AddText(area.Min + new Vector2(pad * 0.35f, pad * 0.25f),
                ImGui.GetColorU32(ink with { W = 0.42f }), placeholder);
        }

        _ = native;
        var shown = EmojiBits.FromWire(current, wireFaces);
        return shown.Length <= maxLength ? shown : shown[..Math.Max(maxLength, 0)];
    }

    public int Pick(string id, Rect area, IReadOnlyList<string> labels, int selected)
    {
        if (area.Width < 8f || area.Height < 8f || labels.Count == 0)
        {
            return selected;
        }

        selected = Math.Clamp(selected, 0, labels.Count - 1);
        ImGui.SetCursorScreenPos(area.Min);
        ImGui.PushClipRect(area.Min, area.Max, false);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.071f, 0.071f, 0.094f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.102f, 0.102f, 0.133f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.102f, 0.102f, 0.133f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.659f, 0.333f, 0.969f, 0.55f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.659f, 0.333f, 0.969f, 0.75f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.659f, 0.333f, 0.969f, 0.95f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.659f, 0.333f, 0.969f, 0.55f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        using (fonts.Handle(FontRole.CaptionStrong).Push())
        {
            if (ImGui.BeginListBox("##" + id, new Vector2(area.Width, area.Height)))
            {
                for (var index = 0; index < labels.Count; index++)
                {
                    var on = index == selected;
                    var label = labels[index] + "###" + id + index;
                    if (ImGui.Selectable(label, on, ImGuiSelectableFlags.SpanAllColumns,
                            new Vector2(0f, 28f)))
                    {
                        selected = index;
                        ownerId = string.Empty;
                        Capturing = false;
                    }

                    if (on)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }

                ImGui.EndListBox();
            }
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(8);
        ImGui.PopClipRect();
        return selected;
    }

    public int Combo(string id, Rect area, IReadOnlyList<string> labels, int selected)
    {
        if (area.Width < 8f || area.Height < 8f || labels.Count == 0)
        {
            return selected;
        }

        selected = Math.Clamp(selected, 0, labels.Count - 1);
        ImGui.SetCursorScreenPos(area.Min);
        ImGui.SetNextItemWidth(area.Width);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.071f, 0.071f, 0.094f, 0.92f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.12f, 0.10f, 0.07f, 0.96f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.14f, 0.11f, 0.06f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.071f, 0.071f, 0.094f, 0.92f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.12f, 0.10f, 0.07f, 0.96f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.14f, 0.11f, 0.06f, 1f));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(0.06f, 0.06f, 0.08f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.92f, 0.78f, 0.42f, 0.28f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.92f, 0.78f, 0.42f, 0.42f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.92f, 0.78f, 0.42f, 0.58f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.92f, 0.78f, 0.42f, 0.55f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, MathF.Min(area.Height * 0.5f, 12f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 10f);
        using (fonts.Handle(FontRole.CaptionStrong).Push())
        {
            if (ImGui.BeginCombo("##" + id, labels[selected], ImGuiComboFlags.HeightLarge))
            {
                for (var index = 0; index < labels.Count; index++)
                {
                    var on = index == selected;
                    if (ImGui.Selectable(labels[index] + "###" + id + index, on))
                    {
                        selected = index;
                    }

                    if (on)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }

                ImGui.EndCombo();
            }
        }

        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(12);
        return selected;
    }

    private void Paint(Rect area, string current, string placeholder, bool focused, float padX, float padY,
        float line, Vector4 ink)
    {
        _ = padY;
        _ = line;
        if (paint is not null && text is not null && textures is not null && paths is not null)
        {
            EmojiText.DrawField(paint, text, textures, paths, area, current, placeholder, ink, padX, focused,
                CaretOn(focused));
            return;
        }

        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(area.Min, area.Max, true);
        var empty = current.Length == 0;
        var hint = empty && !focused;
        var shown = hint ? placeholder : current;
        var origin = area.Min + new Vector2(padX, padY);
        if (hint || !empty)
        {
            draw.AddText(origin, ImGui.GetColorU32(hint ? ink with { W = 0.42f } : ink), shown);
        }

        if (CaretOn(focused))
        {
            var face = MathF.Max(line, 1f);
            var caretH = MathF.Max(10f, face * 0.82f);
            var caretX = Math.Clamp(empty ? origin.X : origin.X + ImGui.CalcTextSize(shown).X + 1f,
                area.Min.X + 1f, area.Max.X - 2f);
            var top = origin.Y + MathF.Max(0f, (face - caretH) * 0.5f);
            draw.AddLine(new Vector2(caretX, top), new Vector2(caretX, top + caretH),
                ImGui.GetColorU32(ink), 1.2f);
        }

        draw.PopClipRect();
    }

    private bool CaretOn(bool focused)
    {
        if (!focused)
        {
            caretOwner = string.Empty;
            return false;
        }

        var now = ImGui.GetTime();
        if (caretOwner != ownerId)
        {
            caretOwner = ownerId;
            caretSince = now;
        }

        return now - caretSince < 0.55d || (int)(now * 2d) % 2 == 0;
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
                    var cut = current.Length;
                    var walk = System.Globalization.StringInfo.GetTextElementEnumerator(current);
                    var last = 0;
                    while (walk.MoveNext())
                    {
                        last = walk.ElementIndex;
                    }

                    cut = last;
                    current = current[..cut];
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

    private static IEnumerable<(VirtualKey Key, char Glyph)> Glyphs(bool shift, bool caps)
    {
        yield return (VirtualKey.SPACE, ' ');
        for (var index = 0; index < 26; index++)
        {
            var upper = shift ^ caps;
            var letter = (char)((upper ? 'A' : 'a') + index);
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

    public string ClipboardText()
    {
        try
        {
            return ImGui.GetClipboardText() ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
