using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Linkpearl.Talk;
using Linkpearl.Theming;

namespace Linkpearl.Host.Windows;

public sealed class TalkPopoutWindow : Window
{
    private readonly ITalk talk;
    private readonly ITheme theme;
    private readonly Action<string> closed;
    private readonly string threadId;
    private readonly Vector2? pinnedPos;
    private readonly Vector2? pinnedSize;
    private bool pinPlace;
    private string draft = string.Empty;
    private int seenGeneration = -1;
    private bool stickBottom = true;
    private bool focusDraft;
    private Vector2 lastPos;
    private Vector2 lastSize = new(340f, 380f);

    public TalkPopoutWindow(string threadId, ITalk talk, ITheme theme, Action<string> closed,
        Vector2? position = null, Vector2? size = null)
        : base("##LinkpearlTalk-" + threadId,
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar)
    {
        this.threadId = threadId;
        this.talk = talk;
        this.theme = theme;
        this.closed = closed;
        pinnedPos = position;
        pinnedSize = size;
        pinPlace = position.HasValue || size.HasValue;
        Size = size ?? new Vector2(340f, 380f);
        SizeCondition = ImGuiCond.FirstUseEver;
        RespectCloseHotkey = false;
        IsOpen = true;
    }

    public string ThreadId => threadId;

    public Vector2 LastPos => lastPos;

    public Vector2 LastSize => lastSize;

    public override void OnClose() => closed(threadId);

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
        var thread = talk.Find(threadId);
        var title = thread?.Title.Length > 0 == true ? thread.Value.Title : "Chat";
        WindowName = title + "###LinkpearlTalk-" + threadId;
        talk.MarkRead(threadId);

        var footer = 44f;
        var body = ImGui.GetContentRegionAvail();
        var logHeight = MathF.Max(64f, body.Y - footer - 10f);
        if (ImGui.BeginChild("##talk-log-" + threadId, new Vector2(body.X, logHeight), false,
                ImGuiWindowFlags.None))
        {
            var lines = talk.Lines(threadId);
            if (lines.Count == 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, palette.InkFaint);
                ImGui.TextUnformatted("No messages yet.");
                ImGui.PopStyleColor();
            }
            else
            {
                var width = ImGui.GetContentRegionAvail().X;
                for (var index = 0; index < lines.Count; index++)
                {
                    DrawBubble(lines[index], title, width, palette);
                    ImGui.Dummy(new Vector2(1f, 8f));
                }
            }

            if (talk.Generation != seenGeneration)
            {
                seenGeneration = talk.Generation;
                stickBottom = true;
            }

            if (stickBottom)
            {
                ImGui.SetScrollHereY(1f);
                stickBottom = false;
            }
        }

        ImGui.EndChild();
        ImGui.Dummy(new Vector2(1f, 6f));
        DrawComposer(thread?.CanSend == true, palette);
    }

    private static void DrawBubble(TalkLine line, string peerName, float width, Palette palette)
    {
        const float pad = 8f;
        const float gap = 2f;
        const float radius = 12f;
        var gold = palette.WarmAccent;
        var who = line.Mine ? "Me" : line.Sender.Length > 0 ? line.Sender : peerName.Length > 0 ? peerName : "Them";
        var body = line.Body ?? string.Empty;
        var bubbleW = MathF.Max(72f, width * 0.78f);
        var wrap = MathF.Max(24f, bubbleW - pad * 2f);
        var nameSize = ImGui.CalcTextSize(who, false, wrap);
        var bodySize = ImGui.CalcTextSize(body, false, wrap);
        var neededW = MathF.Max(nameSize.X, bodySize.X) + pad * 2f;
        if (neededW > bubbleW)
        {
            bubbleW = MathF.Min(width, neededW);
            wrap = MathF.Max(24f, bubbleW - pad * 2f);
            nameSize = ImGui.CalcTextSize(who, false, wrap);
            bodySize = ImGui.CalcTextSize(body, false, wrap);
        }

        var bubbleH = pad + nameSize.Y + gap + bodySize.Y + pad;
        var x = line.Mine ? MathF.Max(0f, width - bubbleW) : 0f;
        var origin = ImGui.GetCursorScreenPos() + new Vector2(x, 0f);
        var max = origin + new Vector2(bubbleW, bubbleH);
        var draw = ImGui.GetWindowDrawList();
        var fill = line.Mine ? MineFill(palette) : palette.SurfaceOverlay with { W = 0.55f };
        var stroke = gold with { W = line.Mine ? 0.62f : 0.38f };
        draw.AddRectFilled(origin, max, ImGui.ColorConvertFloat4ToU32(fill), radius);
        draw.AddRect(origin, max, ImGui.ColorConvertFloat4ToU32(stroke), radius, ImDrawFlags.None, 1.2f);
        draw.PushClipRect(origin + new Vector2(1.5f, 1.5f), max - new Vector2(1.5f, 1.5f), true);

        var textMin = origin + new Vector2(pad, pad);
        ImGui.SetCursorScreenPos(textMin);
        ImGui.PushTextWrapPos(ImGui.GetCursorPos().X + wrap);
        ImGui.PushStyleColor(ImGuiCol.Text, gold);
        ImGui.TextWrapped(who);
        ImGui.PopStyleColor();
        ImGui.SetCursorScreenPos(new Vector2(textMin.X, ImGui.GetItemRectMax().Y + gap));
        ImGui.PushStyleColor(ImGuiCol.Text, palette.Ink);
        ImGui.TextWrapped(body);
        ImGui.PopStyleColor();
        ImGui.PopTextWrapPos();
        draw.PopClipRect();

        var bottom = MathF.Max(max.Y, ImGui.GetItemRectMax().Y + pad);
        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, bottom));
        ImGui.Dummy(new Vector2(1f, 1f));
    }

    private void DrawComposer(bool canSend, Palette palette)
    {
        if (focusDraft)
        {
            ImGui.SetKeyboardFocusHere();
            focusDraft = false;
        }

        ImGui.SetNextItemWidth(MathF.Max(40f, ImGui.GetContentRegionAvail().X - 64f));
        ImGui.PushStyleColor(ImGuiCol.Border, palette.WarmAccent with { W = 0.40f });
        ImGui.PushStyleColor(ImGuiCol.Text, palette.Ink);
        var enter = ImGui.InputTextWithHint("##talk-draft-" + threadId, canSend ? "Message" : "Can't send here",
            ref draft, 400, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.PopStyleColor(2);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, palette.WarmAccent);
        var clicked = ImGui.Button("Send");
        ImGui.PopStyleColor();
        if ((clicked || enter) && canSend && draft.Trim().Length > 0)
        {
            talk.Send(threadId, draft.Trim());
            draft = string.Empty;
            stickBottom = true;
            focusDraft = true;
        }
        else if (enter && canSend)
        {
            focusDraft = true;
        }
    }

    private static Vector4 MineFill(Palette palette)
    {
        var sunk = palette.SurfaceSunken;
        return new Vector4(sunk.X * 0.72f, sunk.Y * 0.72f, sunk.Z * 0.72f, 0.78f);
    }
}
