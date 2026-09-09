using Linkpearl.Applets;
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
    }

    public void Leave()
    {
    }

    public void Compose(in AppletFrame frame)
    {
        TakePicks();
        var content = frame.Content.Inset(frame.Units(16f));
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(8f));
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Feedback",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(28f)),
            "Sends to Discord. Attach FFXIV/Dalamud crashes, pictures, or Word docs.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Category",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var kind = stack.Take(frame.Units(40f));
        CardChrome.Draw(frame, kind);
        category = frame.TextField.Combo("feedback-kind", kind.Inset(frame.Units(6f)), Categories, category);

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Your note",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var box = stack.Take(MathF.Max(frame.Units(88f), stack.Remaining.Height - frame.Units(260f)));
        CardChrome.Draw(frame, box);
        body = frame.TextField.Write("feedback-body", box.Inset(frame.Units(10f)), body,
            "What should we know?", MaxBody);

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Recent crashes",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var crashes = desk.RecentCrashes();
        if (crashes.Count == 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(20f)), "None on this PC yet.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint));
        }
        else
        {
            var show = Math.Min(crashes.Count, 4);
            for (var index = 0; index < show; index++)
            {
                DrawCrash(frame, stack.Take(frame.Units(40f)), crashes[index]);
            }
        }

        var attach = stack.Take(frame.Units(36f));
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
            frame.Text.DrawIn(stack.Take(frame.Units(18f)), attachments.Count + " file(s) attached",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        var actions = stack.Take(frame.Units(44f));
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
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), desk.Status,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
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
