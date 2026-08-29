using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Destinations.You;

public sealed class YouDestination : IDestinationScreen
{
    private readonly IGameSession game;
    private readonly IPearlHub pearl;

    public YouDestination(IGameSession game, IPearlHub pearl)
    {
        this.game = game;
        this.pearl = pearl;
    }

    public DestinationTab Tab => DestinationTab.You;

    public string Glyph => "🧑";

    public string Label => "You";

    public float Compose(in AppletFrame frame)
    {
        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        var snapshot = pearl.Current;

        var profileRow = stack.Take(frame.Units(72f));
        CardChrome.Draw(frame, profileRow);
        DrawProfile(frame, profileRow.Inset(frame.Units(12f)), snapshot);

        if (snapshot.Notice.Length > 0)
        {
            var noticeRow = stack.Take(frame.Units(56f));
            CardChrome.Draw(frame, noticeRow);
            frame.Text.DrawWrapped(noticeRow.Inset(frame.Units(12f)), snapshot.Notice,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        if (snapshot.ChallengeCode.Length > 0)
        {
            var codeRow = stack.Take(frame.Units(56f));
            CardChrome.Draw(frame, codeRow);
            DrawChallenge(frame, codeRow.Inset(frame.Units(12f)), snapshot.ChallengeCode);
        }

        var accountRow = stack.Take(frame.Units(44f));
        CardChrome.Draw(frame, accountRow);
        DrawAccount(frame, accountRow.Inset(new Edges(frame.Units(12f), 0f)), snapshot);

        if (snapshot.SignedIn)
        {
            DrawStat(frame, stack.Take(frame.Units(44f)), "Followers",
                snapshot.Followers.ToString(CultureInfo.InvariantCulture));
            DrawStat(frame, stack.Take(frame.Units(44f)), "Following",
                snapshot.Following.ToString(CultureInfo.InvariantCulture));
            if (snapshot.MyNumber.Length > 0)
            {
                DrawStat(frame, stack.Take(frame.Units(44f)), "Number", snapshot.MyNumber);
            }
        }

        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private void DrawProfile(in AppletFrame frame, Rect inset, PearlSnapshot snapshot)
    {
        var name = snapshot.MeName.Length > 0 ? snapshot.MeName : game.Character.Name;
        if (name.Length == 0)
        {
            name = "Not logged in";
        }

        var world = snapshot.MeWorld.Length > 0 ? snapshot.MeWorld : game.Character.WorldName;
        var detail = world;
        if (snapshot.MeHandle.Length > 0)
        {
            detail = world.Length > 0 ? world + " · @" + snapshot.MeHandle : "@" + snapshot.MeHandle;
        }

        if (detail.Length == 0)
        {
            detail = snapshot.SignedIn ? "Pearlgate" : "Sign in to Pearlgate";
        }

        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawIn(stack.Take(frame.Units(22f)), name,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawChallenge(in AppletFrame frame, Rect inset, string code)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Sign-in code",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        frame.Text.DrawIn(stack.Take(frame.Units(22f)), code,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
    }

    private void DrawAccount(in AppletFrame frame, Rect row, PearlSnapshot snapshot)
    {
        var label = snapshot.Busy ? "Working..." : snapshot.SignedIn ? "Sign out" : "Sign in";
        frame.Text.DrawIn(row.LeftSlice(row.Width - frame.Units(110f)), "Pearlgate",
            new TextStyle(FontRole.Body, frame.Theme.Palette.Ink));

        var action = row.RightSlice(frame.Units(110f));
        if (snapshot.Busy)
        {
            frame.Paint.Fill(action.Inset(frame.Units(2f)), frame.Theme.Palette.SurfaceRaised, frame.Units(999f));
        }
        else
        {
            frame.Paint.Fill(action.Inset(frame.Units(2f)), frame.Theme.Palette.Accent, frame.Units(999f));
        }

        var ink = snapshot.Busy ? frame.Theme.Palette.InkMuted : frame.Theme.Palette.AccentInk;
        frame.Text.DrawIn(action, label, new TextStyle(FontRole.Caption, ink, TextAlign.Center));

        if (!snapshot.Busy && frame.Input.ConsumeClick(action))
        {
            if (snapshot.SignedIn)
            {
                pearl.SignOut();
            }
            else
            {
                pearl.BeginSignIn();
            }
        }
    }

    private static void DrawStat(in AppletFrame frame, Rect row, string label, string value)
    {
        CardChrome.Draw(frame, row);
        var inset = row.Inset(new Edges(frame.Units(12f), 0f));
        frame.Text.DrawIn(inset.LeftSlice(inset.Width - frame.Units(140f)), label,
            new TextStyle(FontRole.Body, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(inset.RightSlice(frame.Units(140f)), value,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Right));
    }
}
