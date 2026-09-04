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
    private int category;
    private string body = string.Empty;
    private string lastStatus = string.Empty;

    public FeedbackApplet(IFeedbackDesk desk, IGameSession game)
    {
        this.desk = desk;
        this.game = game;
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
        var content = frame.Content.Inset(frame.Units(16f));
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(8f));
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Feedback",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(32f)),
            "Sends to the Linkpearl Discord feedback channel. Up to 4000 characters.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Category",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var kind = stack.Take(frame.Units(40f));
        CardChrome.Draw(frame, kind);
        category = frame.TextField.Combo("feedback-kind", kind.Inset(frame.Units(6f)), Categories, category);

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Your note",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var box = stack.Take(MathF.Max(frame.Units(160f), stack.Remaining.Height - frame.Units(120f)));
        CardChrome.Draw(frame, box);
        body = frame.TextField.Write("feedback-body", box.Inset(frame.Units(10f)), body,
            "What should we know?", MaxBody);

        var meta = stack.Take(frame.Units(18f));
        frame.Text.DrawIn(meta, body.Length + " / " + MaxBody,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint, TextAlign.Right));

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
                game.Character.Name, game.Character.WorldName));
        });
        DrawAction(frame, discord, "Discord", false, desk.OpenCommunity);

        if (desk.Status.StartsWith("Sent", StringComparison.Ordinal) &&
            !string.Equals(lastStatus, desk.Status, StringComparison.Ordinal))
        {
            body = string.Empty;
        }

        lastStatus = desk.Status;
        frame.Text.DrawIn(stack.Take(frame.Units(36f)), desk.Status,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
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
