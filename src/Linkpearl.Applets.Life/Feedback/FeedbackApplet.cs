using Linkpearl.Cards;
using Linkpearl.Feedback;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Applets.Life.Feedback;

public sealed class FeedbackApplet : IApplet
{
    public const int MaxBody = 4000;

    public static readonly string[] Categories =
    {
        "Bug",
        "Crash",
        "Feature",
        "UI",
        "Audio",
        "Social",
        "Other",
    };

    public static readonly AppletManifest Manifest = new()
    {
        Id = "feedback",
        DisplayNameKey = "Feedback",
        Family = AppletFamily.Life,
        Glyph = "✉",
        Capabilities = AppletCapabilities.RequiresNetwork,
        HomeOrder = 18,
    };

    private readonly IFeedbackDesk desk;
    private readonly IGameSession game;
    private readonly IFilePicker files;
    private readonly List<string> attachments = [];
    private readonly HashSet<string> crashKeys = new(StringComparer.OrdinalIgnoreCase);
    private int category;
    private string body = string.Empty;
    private string lastStatus = string.Empty;
    private float scroll;

    public FeedbackApplet(IFeedbackDesk desk, IGameSession game, IFilePicker files)
    {
        this.desk = desk;
        this.game = game;
        this.files = files;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public void Enter(AppletEntry entry)
    {
        scroll = 0f;
    }

    public void Leave()
    {
    }

    public void Compose(in AppletFrame frame)
    {
        TakePicks();
        var pad = frame.Units(14f);
        var gap = frame.Units(8f);
        var inner = frame.Content.Inset(new Edges(pad, frame.Units(8f), pad, frame.Units(6f)));
        var statusH = frame.Units(22f);
        var actionsH = frame.Units(44f);
        var foot = inner.BottomSlice(actionsH + gap + statusH);
        var status = foot.TopSlice(statusH);
        var actions = foot.BottomSlice(actionsH);
        var bodyArea = new Rect(inner.Min, new Vector2(inner.Max.X, foot.Min.Y - gap));

        var crashes = desk.RecentCrashes();
        var crashRows = crashes.Count == 0 ? 1 : Math.Min(crashes.Count, 4);
        var hintH = frame.Units(36f);
        var chipsH = frame.Units(64f);
        var noteH = frame.Units(112f);
        var attachH = frame.Units(36f);
        var filesH = attachments.Count > 0 ? frame.Units(18f) : 0f;
        var used = frame.Units(26f) + gap + hintH + gap + frame.Units(16f) + gap + chipsH + gap +
                   frame.Units(16f) + gap + noteH + gap + frame.Units(16f) + gap +
                   crashRows * (frame.Units(40f) + gap) + attachH + (filesH > 0f ? gap + filesH : 0f);

        ScrollSlider.Apply(frame, bodyArea, ref scroll, used);
        frame.Paint.PushClip(bodyArea);
        var stack = new LayoutFlow(
            new Rect(new Vector2(bodyArea.Min.X, bodyArea.Min.Y - scroll),
                new Vector2(bodyArea.Max.X, bodyArea.Min.Y - scroll + MathF.Max(used, bodyArea.Height))),
            StackAxis.Vertical, gap);

        frame.Text.DrawIn(stack.Take(frame.Units(26f)), "Feedback",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        frame.Text.DrawWrapped(stack.Take(hintH),
            "Sends to Discord. Attach FFXIV/Dalamud crashes, pictures, or Word docs.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Category",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        DrawCategories(frame, stack.Take(chipsH));

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Your note",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var box = stack.Take(noteH);
        CardChrome.Draw(frame, box);
        body = frame.TextField.Write("feedback-body", box.Inset(frame.Units(10f)), body,
            "What should we know?", MaxBody);

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Recent crashes",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        if (crashes.Count == 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(20f)), "None on this PC yet.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint));
        }
        else
        {
            for (var index = 0; index < crashRows; index++)
            {
                DrawCrash(frame, stack.Take(frame.Units(40f)), crashes[index]);
            }
        }

        var attach = stack.Take(attachH);
        CardChrome.Draw(frame, attach);
        frame.Text.DrawIn(attach,
            files.Picking ? "Looking…" : "Attach picture or document",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
        if (!files.Picking && frame.Input.ConsumeClick(attach))
        {
            files.BeginAttachPick();
        }

        if (attachments.Count > 0)
        {
            frame.Text.DrawIn(stack.Take(filesH), attachments.Count + " file(s) attached",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        frame.Paint.PopClip();

        frame.Text.DrawEllipsized(status, desk.Status,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var send = actions.LeftSlice(actions.Width * 0.58f);
        var discord = actions.RightSlice(actions.Width * 0.40f);
        DrawAction(frame, send, desk.Busy ? "Sending…" : "Send", true, () =>
        {
            if (desk.Busy)
            {
                return;
            }

            if (body.Length > MaxBody)
            {
                body = body[..MaxBody];
            }

            desk.Send(new FeedbackNote(Categories[Math.Clamp(category, 0, Categories.Length - 1)], body,
                game.Character.Name, game.Character.WorldName, attachments.ToArray()));
        });
        DrawAction(frame, discord, "Discord", false, desk.OpenCommunity);

        if (desk.Status.StartsWith("Sent", StringComparison.Ordinal) &&
            !string.Equals(lastStatus, desk.Status, StringComparison.Ordinal))
        {
            body = string.Empty;
            attachments.Clear();
            crashKeys.Clear();
        }

        lastStatus = desk.Status;
    }

    private void DrawCategories(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var cols = 4;
        var rows = 2;
        var gap = frame.Units(6f);
        var cellW = (area.Width - gap * (cols - 1)) / cols;
        var cellH = (area.Height - gap) / rows;
        for (var index = 0; index < Categories.Length; index++)
        {
            var col = index % cols;
            var row = index / cols;
            var chip = Rect.FromSize(
                new Vector2(area.Min.X + col * (cellW + gap), area.Min.Y + row * (cellH + gap)),
                new Vector2(cellW, cellH));
            var on = category == index;
            if (on)
            {
                CardChrome.DrawGold(frame, chip);
            }
            else
            {
                CardChrome.Draw(frame, chip);
            }

            frame.Text.DrawIn(chip, Categories[index],
                new TextStyle(FontRole.CaptionStrong,
                    on ? gold : frame.Theme.Palette.Ink, TextAlign.Center));
            if (frame.Input.ConsumeClick(chip))
            {
                category = index;
            }
        }
    }

    private void DrawCrash(in AppletFrame frame, Rect row, CrashPick crash)
    {
        var on = crashKeys.Contains(crash.Paths[0]);
        CardChrome.Draw(frame, row);
        if (on)
        {
            CardChrome.DrawGold(frame, row);
        }

        var copy = row.Inset(new Edges(frame.Units(10f), frame.Units(4f)));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(18f)), crash.Label,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(14f)), crash.Detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        if (frame.Input.ConsumeClick(row))
        {
            ToggleCrash(crash);
        }
    }

    private void ToggleCrash(CrashPick crash)
    {
        var key = crash.Paths[0];
        if (!crashKeys.Add(key))
        {
            crashKeys.Remove(key);
            for (var index = 0; index < crash.Paths.Count; index++)
            {
                attachments.Remove(crash.Paths[index]);
            }

            return;
        }

        for (var index = 0; index < crash.Paths.Count; index++)
        {
            var path = crash.Paths[index];
            if (!attachments.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                attachments.Add(path);
            }
        }
    }

    private void TakePicks()
    {
        if (!files.TryTakeImages(out var paths))
        {
            return;
        }

        for (var index = 0; index < paths.Count; index++)
        {
            var path = paths[index];
            if (path.Length > 0 && !attachments.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                attachments.Add(path);
            }
        }
    }

    private static void DrawAction(in AppletFrame frame, Rect area, string label, bool primary, Action tap)
    {
        if (primary)
        {
            CardChrome.DrawGold(frame, area);
        }
        else
        {
            CardChrome.Draw(frame, area);
        }

        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.BodyStrong,
                primary ? frame.Theme.Palette.WarmAccent : frame.Theme.Palette.Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(area))
        {
            tap();
        }
    }
}
