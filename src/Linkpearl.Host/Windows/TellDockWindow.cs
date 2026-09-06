using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Linkpearl.Talk;
using Linkpearl.Theming;

namespace Linkpearl.Host.Windows;

public sealed class TellDockWindow : Window
{
    private readonly ITalk talk;
    private readonly ITheme theme;
    private readonly Action persist;
    private readonly Action? closed;
    private readonly List<string> tabs = [];
    private readonly Dictionary<string, TabState> states = new(StringComparer.OrdinalIgnoreCase);
    private readonly Vector2? pinnedPos;
    private readonly Vector2? pinnedSize;
    private bool pinPlace;
    private string active = string.Empty;
    private float tabScroll;
    private Vector2 lastPos;
    private Vector2 lastSize = new(380f, 420f);

    public TellDockWindow(ITalk talk, ITheme theme, Action persist, Action? closed, Vector2? position, Vector2? size)
        : base("Tells###LinkpearlTellDock", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar)
    {
        this.talk = talk;
        this.theme = theme;
        this.persist = persist;
        this.closed = closed;
        pinnedPos = position;
        pinnedSize = size;
        pinPlace = position.HasValue || size.HasValue;
        Size = size ?? new Vector2(380f, 420f);
        SizeCondition = ImGuiCond.FirstUseEver;
        RespectCloseHotkey = false;
        IsOpen = true;
    }

    public IReadOnlyList<string> Tabs => tabs;

    public Vector2 LastPos => lastPos;

    public Vector2 LastSize => lastSize;

    public bool Has(string threadId) =>
        threadId.Length > 0 && tabs.Exists(id => string.Equals(id, threadId, StringComparison.OrdinalIgnoreCase));

    public void OpenTab(string threadId, bool select)
    {
        if (threadId.Length == 0)
        {
            return;
        }

        if (!Has(threadId))
        {
            tabs.Add(threadId);
            states[threadId] = new TabState();
        }

        if (select || active.Length == 0)
        {
            Select(threadId);
        }

        IsOpen = true;
    }

    public void CloseTab(string threadId)
    {
        var at = IndexOf(threadId);
        if (at < 0)
        {
            return;
        }

        tabs.RemoveAt(at);
        states.Remove(threadId);
        if (string.Equals(active, threadId, StringComparison.OrdinalIgnoreCase))
        {
            active = tabs.Count == 0 ? string.Empty : tabs[Math.Min(at, tabs.Count - 1)];
            if (active.Length > 0)
            {
                Snap(active);
            }
        }

        if (tabs.Count == 0)
        {
            IsOpen = false;
        }

        persist();
    }

    public override void OnClose() => closed?.Invoke();

    public void Snap(string threadId)
    {
        if (!states.TryGetValue(threadId, out var state))
        {
            return;
        }

        state.StickBottom = true;
        state.LogOffset = 0f;
        state.SeenGeneration = -1;
    }

    public override void PreDraw()
    {
        if (pinPlace)
        {
            if (pinnedPos.HasValue)
            {
                ImGui.SetNextWindowPos(pinnedPos.Value, ImGuiCond.Always);
            }

            if (pinnedSize.HasValue)
            {
                ImGui.SetNextWindowSize(pinnedSize.Value, ImGuiCond.Always);
            }

            pinPlace = false;
        }

        var palette = theme.Palette;
        ImGui.PushStyleColor(ImGuiCol.WindowBg, palette.Surface);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, palette.Surface with { W = 0.20f });
        ImGui.PushStyleColor(ImGuiCol.TitleBg, palette.SurfaceSunken);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, palette.SurfaceRaised);
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, palette.SurfaceSunken);
        ImGui.PushStyleColor(ImGuiCol.Text, palette.Ink);
        ImGui.PushStyleColor(ImGuiCol.Border, palette.WarmAccent with { W = 0.42f });
        ImGui.PushStyleColor(ImGuiCol.FrameBg, palette.SurfaceOverlay);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, palette.SurfaceOverlay with { W = 0.85f });
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, palette.SurfaceRaised);
        ImGui.PushStyleColor(ImGuiCol.Button, palette.WarmAccent with { W = 0.22f });
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, palette.WarmAccent with { W = 0.38f });
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, palette.WarmAccent with { W = 0.55f });
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 14f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1.2f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(5);
        ImGui.PopStyleColor(13);
    }

    public override void Draw()
    {
        lastPos = ImGui.GetWindowPos();
        lastSize = ImGui.GetWindowSize();
        var palette = theme.Palette;
        var unread = UnreadTotal();
        WindowName = unread > 0 ? "Tells (" + unread + ")###LinkpearlTellDock" : "Tells###LinkpearlTellDock";
        DrawTabs(palette);
        if (active.Length == 0)
        {
            ImGui.TextUnformatted("Waiting for tells.");
            return;
        }

        talk.MarkRead(active);
        var thread = talk.Find(active);
        var title = thread?.Title.Length > 0 == true ? thread.Value.Title : "Tell";
        var state = StateOf(active);
        var footer = 44f;
        var body = ImGui.GetContentRegionAvail();
        var logHeight = MathF.Max(64f, body.Y - footer - 10f);
        DrawLog(active, title, state, logHeight, palette);
        ImGui.Dummy(new Vector2(1f, 6f));
        DrawComposer(active, state, thread?.CanSend == true, palette);
    }

    private void DrawTabs(Palette palette)
    {
        var gold = palette.WarmAccent;
        var width = MathF.Max(32f, ImGui.GetContentRegionAvail().X);
        var height = 28f;
        var origin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##tell-tabs", new Vector2(width, height));
        if (ImGui.IsItemHovered())
        {
            tabScroll = MathF.Max(0f, tabScroll - ImGui.GetIO().MouseWheel * 48f);
        }

        var draw = ImGui.GetWindowDrawList();
        draw.AddLine(origin + new Vector2(0f, height - 1f), origin + new Vector2(width, height - 1f),
            ImGui.ColorConvertFloat4ToU32(gold with { W = 0.28f }), 1f);
        var cursor = origin.X - tabScroll;
        var maxX = origin.X + width;
        var total = 0f;
        string? closing = null;
        for (var index = 0; index < tabs.Count; index++)
        {
            var id = tabs[index];
            var label = TabLabel(id);
            var size = ImGui.CalcTextSize(label);
            var tabW = MathF.Max(64f, size.X + 36f);
            total += tabW + 4f;
            var min = new Vector2(cursor, origin.Y);
            var max = new Vector2(cursor + tabW, origin.Y + height - 2f);
            var on = string.Equals(active, id, StringComparison.OrdinalIgnoreCase);
            if (max.X > origin.X && min.X < maxX)
            {
                var fill = on ? gold with { W = 0.28f } : palette.SurfaceOverlay with { W = 0.45f };
                draw.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(fill), 8f);
                if (on)
                {
                    draw.AddRectFilled(new Vector2(min.X + 6f, max.Y - 3f), new Vector2(max.X - 6f, max.Y),
                        ImGui.ColorConvertFloat4ToU32(gold), 2f);
                }

                ImGui.SetCursorScreenPos(min + new Vector2(8f, (height - size.Y) * 0.5f));
                ImGui.TextUnformatted(label);
                var thread = talk.Find(id);
                var unread = !on && thread is { Unread: > 0 };
                if (unread)
                {
                    draw.AddCircleFilled(new Vector2(max.X - 18f, min.Y + 8f), 3.4f,
                        ImGui.ColorConvertFloat4ToU32(gold));
                }

                var closeMin = new Vector2(max.X - 16f, min.Y + 6f);
                var closeMax = new Vector2(max.X - 4f, max.Y - 6f);
                var overClose = ImGui.IsMouseHoveringRect(closeMin, closeMax);
                ImGui.SetCursorScreenPos(new Vector2(closeMin.X, min.Y + 5f));
                ImGui.PushStyleColor(ImGuiCol.Text, overClose ? gold : palette.InkMuted);
                ImGui.TextUnformatted("×");
                ImGui.PopStyleColor();
                if (ImGui.IsMouseHoveringRect(min, max) && ImGui.IsMouseClicked(ImGuiMouseButton.Middle))
                {
                    closing = id;
                }
                else if (ImGui.IsMouseHoveringRect(min, max) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    if (overClose)
                    {
                        closing = id;
                    }
                    else
                    {
                        Select(id);
                    }
                }
            }

            cursor += tabW + 4f;
        }

        tabScroll = Math.Clamp(tabScroll, 0f, MathF.Max(0f, total - width));
        ImGui.SetCursorScreenPos(origin + new Vector2(0f, height + 4f));
        if (closing != null)
        {
            CloseTab(closing);
        }
    }

    private void DrawLog(string threadId, string title, TabState state, float logHeight, Palette palette)
    {
        var width = MathF.Max(32f, ImGui.GetContentRegionAvail().X);
        var origin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##tell-log-" + threadId, new Vector2(width, logHeight));
        var hovering = ImGui.IsItemHovered();
        var lines = talk.Lines(threadId);
        if (lines.Count == 0)
        {
            ImGui.SetCursorScreenPos(origin + new Vector2(8f, 8f));
            ImGui.PushStyleColor(ImGuiCol.Text, palette.InkFaint);
            ImGui.TextUnformatted("No messages yet.");
            ImGui.PopStyleColor();
            ImGui.SetCursorScreenPos(origin + new Vector2(0f, logHeight));
            return;
        }

        const float gap = 8f;
        var heights = new float[lines.Count];
        var total = 0f;
        for (var index = 0; index < lines.Count; index++)
        {
            heights[index] = MeasureBubble(lines[index], title, width);
            total += heights[index] + (index == 0 ? 0f : gap);
        }

        var maxOffset = MathF.Max(0f, total - logHeight);
        if (talk.Generation != state.SeenGeneration)
        {
            state.SeenGeneration = talk.Generation;
            state.StickBottom = true;
        }

        if (hovering)
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (MathF.Abs(wheel) > 0.01f)
            {
                state.StickBottom = false;
                state.LogOffset = Math.Clamp(state.LogOffset - wheel * 48f, 0f, maxOffset);
            }
        }

        if (state.StickBottom)
        {
            state.LogOffset = maxOffset;
        }
        else
        {
            state.LogOffset = Math.Clamp(state.LogOffset, 0f, maxOffset);
            if (state.LogOffset >= maxOffset - 1.5f)
            {
                state.StickBottom = true;
            }
        }

        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin, origin + new Vector2(width, logHeight), true);
        var cursor = origin.Y - state.LogOffset;
        for (var index = 0; index < lines.Count; index++)
        {
            var height = heights[index];
            if (cursor + height >= origin.Y && cursor <= origin.Y + logHeight)
            {
                DrawBubble(new Vector2(origin.X, cursor), lines[index], title, width, palette);
            }

            cursor += height + gap;
        }

        draw.PopClipRect();
        ImGui.SetCursorScreenPos(origin + new Vector2(0f, logHeight));
    }

    private void DrawComposer(string threadId, TabState state, bool canSend, Palette palette)
    {
        if (state.FocusDraft)
        {
            ImGui.SetKeyboardFocusHere();
            state.FocusDraft = false;
        }

        ImGui.SetNextItemWidth(MathF.Max(40f, ImGui.GetContentRegionAvail().X - 64f));
        ImGui.PushStyleColor(ImGuiCol.Border, palette.WarmAccent with { W = 0.40f });
        ImGui.PushStyleColor(ImGuiCol.Text, palette.Ink);
        var enter = ImGui.InputTextWithHint("##tell-draft-" + threadId, canSend ? "Message" : "Can't send here",
            ref state.Draft, 400, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.PopStyleColor(2);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, palette.WarmAccent);
        var clicked = ImGui.Button("Send");
        ImGui.PopStyleColor();
        if ((clicked || enter) && canSend && state.Draft.Trim().Length > 0)
        {
            talk.Send(threadId, state.Draft.Trim());
            state.Draft = string.Empty;
            state.StickBottom = true;
            state.FocusDraft = true;
        }
        else if (enter && canSend)
        {
            state.FocusDraft = true;
        }
    }

    private void Select(string threadId)
    {
        active = threadId;
        Snap(threadId);
        talk.MarkRead(threadId);
    }

    private TabState StateOf(string threadId)
    {
        if (states.TryGetValue(threadId, out var state))
        {
            return state;
        }

        state = new TabState();
        states[threadId] = state;
        return state;
    }

    private int UnreadTotal()
    {
        var count = 0;
        for (var index = 0; index < tabs.Count; index++)
        {
            if (string.Equals(tabs[index], active, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            count += talk.Find(tabs[index])?.Unread ?? 0;
        }

        return count;
    }

    private string TabLabel(string threadId)
    {
        var title = talk.Find(threadId)?.Title ?? string.Empty;
        if (title.Length == 0 && TalkIds.TryParseTell(threadId, out var name, out _))
        {
            title = name;
        }

        var space = title.IndexOf(' ');
        return space > 0 ? title[..space] : title.Length > 0 ? title : "Tell";
    }

    private int IndexOf(string threadId)
    {
        for (var index = 0; index < tabs.Count; index++)
        {
            if (string.Equals(tabs[index], threadId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static float MeasureBubble(TalkLine line, string peerName, float width)
    {
        const float pad = 8f;
        SizeBubble(line, peerName, width, out _, out var nameSize, out var bodySize);
        return pad + nameSize.Y + 2f + bodySize.Y + pad;
    }

    private static void SizeBubble(TalkLine line, string peerName, float width, out float bubbleW,
        out Vector2 nameSize, out Vector2 bodySize)
    {
        const float pad = 8f;
        var who = Who(line, peerName);
        var body = line.Body ?? string.Empty;
        bubbleW = MathF.Max(72f, width * 0.78f);
        var wrap = MathF.Max(24f, bubbleW - pad * 2f);
        nameSize = ImGui.CalcTextSize(who, false, wrap);
        bodySize = ImGui.CalcTextSize(body, false, wrap);
        var neededW = MathF.Max(nameSize.X, bodySize.X) + pad * 2f;
        if (neededW <= bubbleW)
        {
            return;
        }

        bubbleW = MathF.Min(width, neededW);
        wrap = MathF.Max(24f, bubbleW - pad * 2f);
        nameSize = ImGui.CalcTextSize(who, false, wrap);
        bodySize = ImGui.CalcTextSize(body, false, wrap);
    }

    private static string Who(TalkLine line, string peerName) =>
        line.Mine ? "Me" : line.Sender.Length > 0 ? line.Sender : peerName.Length > 0 ? peerName : "Them";

    private static void DrawBubble(Vector2 row, TalkLine line, string peerName, float width, Palette palette)
    {
        const float pad = 8f;
        const float gap = 2f;
        const float radius = 12f;
        var gold = palette.WarmAccent;
        SizeBubble(line, peerName, width, out var bubbleW, out var nameSize, out var bodySize);
        var wrap = MathF.Max(24f, bubbleW - pad * 2f);
        var bubbleH = pad + nameSize.Y + gap + bodySize.Y + pad;
        var x = line.Mine ? MathF.Max(0f, width - bubbleW) : 0f;
        var origin = row + new Vector2(x, 0f);
        var max = origin + new Vector2(bubbleW, bubbleH);
        var draw = ImGui.GetWindowDrawList();
        var sunk = palette.SurfaceSunken;
        var fill = line.Mine
            ? new Vector4(sunk.X * 0.72f, sunk.Y * 0.72f, sunk.Z * 0.72f, 0.78f)
            : palette.SurfaceOverlay with { W = 0.55f };
        draw.AddRectFilled(origin, max, ImGui.ColorConvertFloat4ToU32(fill), radius);
        draw.AddRect(origin, max, ImGui.ColorConvertFloat4ToU32(gold with { W = line.Mine ? 0.62f : 0.38f }), radius,
            ImDrawFlags.None, 1.2f);
        draw.PushClipRect(origin + new Vector2(1.5f, 1.5f), max - new Vector2(1.5f, 1.5f), true);
        var textMin = origin + new Vector2(pad, pad);
        ImGui.SetCursorScreenPos(textMin);
        ImGui.PushTextWrapPos(ImGui.GetCursorPos().X + wrap);
        ImGui.PushStyleColor(ImGuiCol.Text, gold);
        ImGui.TextWrapped(Who(line, peerName));
        ImGui.PopStyleColor();
        ImGui.SetCursorScreenPos(new Vector2(textMin.X, ImGui.GetItemRectMax().Y + gap));
        ImGui.PushStyleColor(ImGuiCol.Text, palette.Ink);
        ImGui.TextWrapped(line.Body ?? string.Empty);
        ImGui.PopStyleColor();
        ImGui.PopTextWrapPos();
        draw.PopClipRect();
    }

    private sealed class TabState
    {
        public string Draft = string.Empty;
        public bool StickBottom = true;
        public float LogOffset;
        public int SeenGeneration = -1;
        public bool FocusDraft;
    }
}
