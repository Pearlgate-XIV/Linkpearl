using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Music;

public sealed partial class MusicApplet
{
    private IReadOnlyList<MusicPerson> Roster() =>
        MusicRoster.Directory(state, pearl.Current, community.Live, community.Mine, community.Directory,
            community.Broadcasting, MarkedName());

    private void DrawPersonSection(in AppletFrame frame, ref Stack stack, string title, IReadOnlyList<MusicPerson> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), title);
        for (var index = 0; index < rows.Count; index++)
        {
            DrawPersonRow(frame, stack.Take(frame.Units(56f)), rows[index]);
        }
    }

    private void DrawPersonRow(in AppletFrame frame, Rect row, MusicPerson person)
    {
        MusicChrome.GlowPlate(frame, row, frame.Units(12f), person.Live || person.Mine);
        var inset = row.Inset(frame.Units(8f));
        var face = inset.LeftSlice(frame.Units(36f));
        frame.Paint.FillCircle(face.Center, frame.Units(14f), MusicChrome.Purple);
        frame.Text.DrawIn(face, person.Name.Length > 0 ? char.ToUpperInvariant(person.Name[0]).ToString() : "♪",
            new TextStyle(FontRole.CaptionStrong, MusicChrome.GroundHi, TextAlign.Center));
        var follow = !person.Mine ? inset.RightSlice(frame.Units(64f)) : Rect.Empty;
        var body = inset.Inset(new Edges(frame.Units(42f), 0f, person.Mine ? 0f : frame.Units(68f), 0f));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), person.Name,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(16f)),
            person.Handle + " · " + person.Role + (person.Live ? " · Live" : string.Empty),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        if (!person.Mine)
        {
            var on = person.Id.StartsWith("live:", StringComparison.Ordinal)
                ? state.FollowsStationId(person.Id)
                : state.Following.Contains(person.Id);
            frame.Paint.Fill(follow, on ? MusicChrome.Purple : MusicChrome.CardHi, frame.Units(10f));
            frame.Text.DrawIn(follow, on ? "Followed" : "Follow",
                new TextStyle(FontRole.CaptionStrong, on ? MusicChrome.GroundHi : MusicChrome.Purple, TextAlign.Center));
            if (frame.Input.ConsumeClick(follow))
            {
                FollowPerson(person);
                return;
            }
        }

        if (frame.Input.ConsumeClick(row))
        {
            ShowProfile(person.Id);
        }
    }
}
