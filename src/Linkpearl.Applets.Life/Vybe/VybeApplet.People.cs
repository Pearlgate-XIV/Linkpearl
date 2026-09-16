using System.Globalization;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Vybe;

public sealed partial class VybeApplet
{
    private static readonly Vector4 FindInk = new(0.961f, 0.961f, 0.949f, 1f);
    private static readonly Vector4 FindMute = new(0.596f, 0.612f, 0.647f, 1f);
    private static readonly Vector4 FindLine = new(0f, 0f, 0f, 0.35f);
    private static readonly Vector4 FindLift = new(0f, 0f, 0f, 0.42f);

    private PeopleCard[] findDeck = [];

    private void DrawPeopleDiscovery(in AppletFrame frame, ref LayoutFlow stack, bool night)
    {
        var find = state.PeopleFind;
        var mine = PeopleFindBook.Mine(state);
        var home = game.Character.WorldName;
        var picks = PeopleFindBook.Match(findDeck, find, state, home, mine);
        var chrome = frame.Units(14f);
        if (night)
        {
            var next = DrawPlusSplit(frame, PadX(stack.Take(frame.Units(56f)), chrome), "People", state.PeoplePlus,
                night);
            if (next != state.PeoplePlus)
            {
                state.PeoplePlus = next;
                state.Scroll = 0f;
            }
        }

        if (picks.Count == 0)
        {
            DrawPeopleEmpty(frame, PadX(stack.Take(frame.Units(120f)), chrome), find, night, findDeck.Length == 0);
            return;
        }

        var gap = frame.Units(2f);
        var half = (stack.Remaining.Width - gap) * 0.5f;
        var photoH = half;
        var factsH = PeopleTileFactsHeight(frame);
        var cardH = photoH + factsH;
        stack.Take(frame.Units(4f));
        for (var index = 0; index < picks.Count; index += 2)
        {
            var row = stack.Take(cardH);
            DrawPeopleTile(frame, Rect.FromSize(row.Min, new Vector2(half, row.Height)), picks[index], find, mine,
                home, night);
            if (index + 1 < picks.Count)
            {
                DrawPeopleTile(frame,
                    Rect.FromSize(new Vector2(row.Min.X + half + gap, row.Min.Y), new Vector2(half, row.Height)),
                    picks[index + 1], find, mine, home, night);
            }
        }
    }

    private static Rect PadX(Rect area, float pad) => area.Inset(new Edges(pad, 0f, pad, 0f));

    private static float PeopleTileFactsHeight(in AppletFrame frame) =>
        frame.Units(8f) +
        MathF.Max(frame.Units(18f), frame.Text.LineHeight(FontRole.BodyStrong)) +
        MathF.Max(frame.Units(14f), frame.Text.LineHeight(FontRole.Caption)) +
        frame.Units(8f) +
        MathF.Max(frame.Units(15f), frame.Text.LineHeight(FontRole.Caption)) * 6f +
        frame.Units(10f);

    private void DrawAppliedFilters(in AppletFrame frame, ref LayoutFlow stack, PeopleFindState find, bool night)
    {
        var tags = find.Applied();
        if (tags.Count == 0)
        {
            return;
        }

        var tone = VybeChrome.Tone(night);
        var gap = frame.Units(6f);
        var rowH = frame.Units(26f);
        var pad = frame.Units(10f);
        var cursorX = 0f;
        var rows = 1;
        var widths = new float[tags.Count];
        for (var index = 0; index < tags.Count; index++)
        {
            var text = Short(tags[index]);
            widths[index] = MathF.Min(stack.Remaining.Width, frame.Text.Measure(text, FontRole.CaptionStrong).X + pad * 2f);
            if (cursorX > 0f && cursorX + widths[index] > stack.Remaining.Width)
            {
                rows++;
                cursorX = 0f;
            }

            cursorX += widths[index] + gap;
        }

        VybeChrome.Mute(frame, stack.Take(frame.Units(14f)), "Your search", night);
        var board = stack.Take(rowH * rows + gap * Math.Max(0, rows - 1));
        var x = board.Min.X;
        var y = board.Min.Y;
        for (var index = 0; index < tags.Count; index++)
        {
            if (x > board.Min.X && x + widths[index] > board.Max.X)
            {
                x = board.Min.X;
                y += rowH + gap;
            }

            var cell = Rect.FromSize(new Vector2(x, y), new Vector2(widths[index], rowH));
            frame.Paint.Fill(cell, tone.AccentDim, rowH * 0.5f);
            frame.Paint.Stroke(cell, tone.Accent with { W = 0.55f }, frame.Units(1f), rowH * 0.5f);
            frame.Text.DrawEllipsized(cell.Inset(new Edges(pad * 0.45f, 0f)), Short(tags[index]),
                new TextStyle(FontRole.CaptionStrong, FindInk, TextAlign.Center));
            if (frame.Input.ConsumeClick(cell))
            {
                find.Drop(tags[index]);
                if (find.Query.Length == 0)
                {
                    BindDiscoverSearch(string.Empty);
                }

                state.Save(paths);
            }

            x += widths[index] + gap;
        }
    }

    private void DrawPeopleEmpty(in AppletFrame frame, Rect area, PeopleFindState find, bool night, bool noPeople)
    {
        VybeChrome.Plate(frame, area, frame.Units(16f), night);
        var inner = area.Inset(frame.Units(14f));
        VybeChrome.Title(frame, inner.TopSlice(frame.Units(24f)),
            noPeople
                ? "No Pearlgate people yet."
                : state.PeoplePlus
                    ? "No VYBE+ people yet."
                    : "No one matches those filters yet.", night);
        VybeChrome.Mute(frame, inner.Inset(new Edges(0f, frame.Units(28f), 0f, frame.Units(52f))),
            noPeople
                ? "People show up here after they sign in. Open VYBE again in a few seconds."
                : "Open Filters to widen the search, or tap a chip above to drop it.", night);
        var row = inner.BottomSlice(frame.Units(40f));
        var left = row.LeftSlice(row.Width * 0.48f);
        var right = row.RightSlice(row.Width * 0.48f);
        VybeChrome.Primary(frame, left, "Broaden Search", night);
        VybeChrome.Glow(frame, right, frame.Units(12f), false, night);
        frame.Text.DrawIn(right, "Reset Filters",
            new TextStyle(FontRole.CaptionStrong, FindInk, TextAlign.Center));
        if (frame.Input.ConsumeClick(left))
        {
            find.Quick.Clear();
            find.Activity.Clear();
            find.Nearby.Clear();
            find.SharedFloor = string.Empty;
            state.Save(paths);
        }

        if (frame.Input.ConsumeClick(right))
        {
            find.Clear();
            state.Save(paths);
        }
    }

    private void DrawPeopleTile(in AppletFrame frame, Rect area, PeopleCard card, PeopleFindState find,
        IReadOnlyCollection<string> mine, string home, bool night)
    {
        var tone = VybeChrome.Tone(night);
        var photo = area.TopSlice(area.Width);
        DrawPeopleCover(frame, photo, card, night, 0f);
        var live = new Vector2(photo.Min.X + frame.Units(10f), photo.Min.Y + frame.Units(10f));
        frame.Paint.FillCircle(live, frame.Units(5.2f), new Vector4(0f, 0f, 0f, 0.45f));
        frame.Paint.FillCircle(live, frame.Units(3.8f),
            card.Online ? VybeChrome.Online : new Vector4(0.52f, 0.52f, 0.56f, 1f));
        var likes = PersonLikeCount(card.Id);
        var likeText = likes.ToString(CultureInfo.InvariantCulture);
        var likeW = frame.Units(22f) + frame.Text.Measure(likeText, FontRole.Caption).X + frame.Units(10f);
        var heart = Rect.FromSize(
            new Vector2(photo.Max.X - likeW - frame.Units(6f), photo.Min.Y + frame.Units(8f)),
            new Vector2(likeW, frame.Units(22f)));
        DrawHottHeart(frame, heart, state.LikedPeople.Contains(card.Id), likeText, tone);
        var copy = area.Inset(new Edges(frame.Units(8f), photo.Height + frame.Units(8f), frame.Units(8f),
            frame.Units(6f)));
        frame.Paint.Fill(new Rect(new Vector2(area.Min.X, photo.Max.Y), area.Max), FindLift);
        var lines = new LayoutFlow(copy, StackAxis.Vertical, frame.Units(2f));
        var nameH = MathF.Max(frame.Units(18f), frame.Text.LineHeight(FontRole.BodyStrong));
        var nameRow = lines.Take(nameH);
        if (card.PlusMember || card.PlusOnly)
        {
            var tagW = MathF.Min(VybeChrome.PlusTagWidth(frame), nameRow.Width * 0.40f);
            frame.Text.DrawEllipsized(nameRow.Inset(new Edges(0f, 0f, tagW + frame.Units(4f), 0f)), card.Name,
                new TextStyle(FontRole.BodyStrong, FindInk));
            VybeChrome.PlusTag(frame, nameRow.RightSlice(tagW));
        }
        else
        {
            frame.Text.DrawEllipsized(nameRow, card.Name, new TextStyle(FontRole.BodyStrong, FindInk));
        }

        frame.Text.DrawEllipsized(lines.Take(MathF.Max(frame.Units(14f), frame.Text.LineHeight(FontRole.Caption))),
            card.Handle, new TextStyle(FontRole.Caption, FindMute));
        lines.Take(frame.Units(4f));

        var world = card.World;
        if (card.DataCenter.Length > 0)
        {
            world = world.Length > 0 ? world + " · " + card.DataCenter : card.DataCenter;
        }

        if (card.Age > 0 && card.DatingOn && state.DatingDiscovery)
        {
            world = card.Age.ToString(CultureInfo.InvariantCulture) + " · " + world;
        }

        DrawPeopleFact(frame, ref lines, "Race", card.Race, FindMute, FindInk);
        DrawPeopleFact(frame, ref lines, "World", world, FindMute, FindInk);
        DrawPeopleFact(frame, ref lines, "Job", JoinPair(card.Job, card.Role), FindMute, FindInk);
        DrawPeopleFact(frame, ref lines, "I am", JoinPair(card.Gender, card.Sexuality), FindMute, FindInk);
        DrawPeopleFact(frame, ref lines, "Seek", string.Join(" · ", Take(card.LookingFor, 2)), FindMute, FindInk);
        var shared = PeopleFindBook.SharedCount(card.Interests, mine);
        var score = PeopleFindBook.Score(card, find, home, PeopleFindBook.DataCenterOf(home), mine,
            state.LaneDone ? state.LaneMarks : null);
        var status = card.Online ? "Online now" : card.Activity == "Today" ? "Active today" : "Active this week";
        DrawPeopleFact(frame, ref lines, "Match",
            score.ToString(CultureInfo.InvariantCulture) + "%  ·  " + status +
            (shared > 0 ? "  ·  " + shared.ToString(CultureInfo.InvariantCulture) + " shared" : string.Empty),
            FindMute, card.Online ? VybeChrome.Online : tone.Accent);

        if (frame.Input.ConsumeClick(heart))
        {
            state.ToggleLikedPerson(card.Id);
            find.PulseId = card.Id;
            find.Pulse = 0.22f;
            state.Save(paths);
            return;
        }

        if (frame.Input.ConsumeClick(area))
        {
            OpenFound(card);
        }
    }

    private static void DrawPeopleFact(in AppletFrame frame, ref LayoutFlow stack, string label, string value,
        Vector4 mute, Vector4 ink)
    {
        var shown = (value ?? string.Empty).Trim();
        if (shown.Length == 0)
        {
            return;
        }

        var row = stack.Take(MathF.Max(frame.Units(15f), frame.Text.LineHeight(FontRole.Caption)));
        var labelW = MathF.Min(frame.Units(40f), row.Width * 0.34f);
        frame.Text.DrawEllipsized(row.LeftSlice(labelW), label, new TextStyle(FontRole.Caption, mute));
        frame.Text.DrawEllipsized(row.Inset(new Edges(labelW + frame.Units(4f), 0f, 0f, 0f)), shown,
            new TextStyle(FontRole.CaptionStrong, ink));
    }

    private static string JoinPair(string left, string right)
    {
        left = (left ?? string.Empty).Trim();
        right = (right ?? string.Empty).Trim();
        if (left.Length == 0)
        {
            return right;
        }

        return right.Length == 0 ? left : left + " · " + right;
    }

    private void DrawPeopleCard(in AppletFrame frame, Rect area, PeopleCard card, PeopleFindState find,
        IReadOnlyCollection<string> mine, string home, bool night)
    {
        var tone = VybeChrome.Tone(night);
        var radius = frame.Units(16f);
        frame.Paint.Fill(area, FindLift, radius);
        frame.Paint.Stroke(area, FindLine, frame.Units(1f), radius);
        var photo = area.TopSlice(frame.Units(168f)).Inset(new Edges(frame.Units(8f), frame.Units(8f), frame.Units(8f),
            0f));
        DrawPeopleCover(frame, photo, card, night);
        var live = new Vector2(photo.Min.X + frame.Units(12f), photo.Min.Y + frame.Units(12f));
        frame.Paint.FillCircle(live, frame.Units(5.2f), new Vector4(0f, 0f, 0f, 0.45f));
        frame.Paint.FillCircle(live, frame.Units(3.8f),
            card.Online ? VybeChrome.Online : new Vector4(0.52f, 0.52f, 0.56f, 1f));
        var body = area.Inset(new Edges(frame.Units(12f), frame.Units(180f), frame.Units(12f), frame.Units(8f)));
        var stack = new LayoutFlow(body, StackAxis.Vertical, frame.Units(3f));
        frame.Text.DrawEllipsized(stack.Take(frame.Units(20f)), card.Name,
            new TextStyle(FontRole.Title, FindInk));
        var zone = card.TimeZoneId.Length > 0 ? card.TimeZoneId : WorldZones.PickFor(card.GateId);
        var sub = card.World + " • " + card.DataCenter + " • " + ZoneClock.Line(zone, display.Use24HourClock);
        if (card.Age > 0 && card.DatingOn && state.DatingDiscovery)
        {
            sub = card.Age.ToString(CultureInfo.InvariantCulture) + " • " + sub;
        }

        frame.Text.DrawEllipsized(stack.Take(frame.Units(14f)), card.Handle + "  " + sub,
            new TextStyle(FontRole.Caption, FindMute));
        frame.Text.DrawEllipsized(stack.Take(frame.Units(14f)), card.Job + "  ·  " + card.Role,
            new TextStyle(FontRole.CaptionStrong, FindInk));
        frame.Text.DrawEllipsized(stack.Take(frame.Units(14f)), "Looking for: " + string.Join(" • ", card.LookingFor),
            new TextStyle(FontRole.Caption, FindMute));
        var shared = PeopleFindBook.SharedCount(card.Interests, mine);
        frame.Text.DrawEllipsized(stack.Take(frame.Units(14f)),
            shared.ToString(CultureInfo.InvariantCulture) + " Shared Interests",
            new TextStyle(FontRole.CaptionStrong, tone.Accent));
        frame.Text.DrawEllipsized(stack.Take(frame.Units(14f)), string.Join("   ", Take(card.Interests, 3)),
            new TextStyle(FontRole.Caption, FindInk));
        frame.Text.DrawEllipsized(stack.Take(frame.Units(16f)), card.Bio,
            new TextStyle(FontRole.Caption, FindMute));
        frame.Text.DrawEllipsized(stack.Take(frame.Units(14f)),
            card.Online ? "● Online Now" : card.Activity == "Today" ? "Active today" : "Active this week",
            new TextStyle(FontRole.Caption, card.Online ? VybeChrome.Online : FindMute));
        var homeDc = PeopleFindBook.DataCenterOf(home);
        var why = stack.Take(frame.Units(20f));
        var score = PeopleFindBook.Score(card, find, home, homeDc, mine,
            state.LaneDone ? state.LaneMarks : null);
        frame.Text.DrawEllipsized(why, "Why you're seeing them  ·  " + score.ToString(CultureInfo.InvariantCulture) +
                                       "% match",
            new TextStyle(FontRole.Caption, tone.Accent));
        if (frame.Input.ConsumeClick(why))
        {
            find.WhyOpen = !find.WhyOpen || find.WhyId != card.Id;
            find.WhyId = card.Id;
        }

        if (find.WhyOpen && find.WhyId == card.Id)
        {
            var reasons = PeopleFindBook.Reasons(card, home, homeDc, mine, find,
                state.LaneDone ? state.LaneMarks : null);
            frame.Text.DrawEllipsized(stack.Take(frame.Units(28f)), string.Join("  ·  ", reasons),
                new TextStyle(FontRole.Caption, FindMute));
        }

        var actions = stack.Take(frame.Units(36f));
        var likes = PersonLikeCount(card.Id);
        var likeText = likes.ToString(CultureInfo.InvariantCulture);
        var heart = actions.LeftSlice(MathF.Min(actions.Width * 0.42f, frame.Units(88f)));
        DrawHottHeart(frame, heart, state.LikedPeople.Contains(card.Id), likeText, tone);
        if (frame.Input.ConsumeClick(heart))
        {
            state.ToggleLikedPerson(card.Id);
            find.PulseId = card.Id;
            find.Pulse = 0.22f;
            state.Save(paths);
            return;
        }

        if (frame.Input.ConsumeClick(photo) || frame.Input.ConsumeClick(actions))
        {
            OpenFound(card);
        }
    }

    private static ScenePerson PersonFromCard(PeopleCard card) =>
        new(card.Id, card.GateId, card.Name, card.Handle, card.World, card.Bio, card.Online,
            4, card.PlusOnly, new Vector4(0.20f, 0.22f, 0.28f, 1f), card.LookingFor, card.Interests,
            card.Avatar, PlusMember: card.PlusMember, TimeZoneId: card.TimeZoneId);

    private void DrawPeopleCover(in AppletFrame frame, Rect area, PeopleCard card, bool night, float radius = -1f)
    {
        var person = PersonFromCard(card);
        DrawPersonCover(frame, area, person, night, radius);
        frame.Paint.FillGradient(area, new Vector4(0f, 0f, 0f, 0.04f), new Vector4(0f, 0f, 0f, 0.48f),
            GradientAxis.Vertical);
    }

    private void DrawPeopleFilters(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var find = state.PeopleFind;
        var stack = new LayoutFlow(area, StackAxis.Vertical, frame.Units(8f));
        var head = stack.Take(frame.Units(28f));
        if (VybeChrome.Back(frame, head.LeftSlice(frame.Units(72f)), "Filters", night))
        {
            state.Back();
            return;
        }

        var count = find.Count();
        frame.Text.DrawIn(head.RightSlice(frame.Units(90f)),
            count > 0 ? "Filters (" + count.ToString(CultureInfo.InvariantCulture) + ")" : "Filters",
            new TextStyle(FontRole.CaptionStrong, tone.Accent, TextAlign.Right));
        DrawAppliedFilters(frame, ref stack, find, night);

        var top = stack.Take(frame.Units(34f));
        VybeChrome.Glow(frame, top.LeftSlice(top.Width * 0.48f), frame.Units(12f), false, night);
        frame.Text.DrawIn(top.LeftSlice(top.Width * 0.48f), "Reset",
            new TextStyle(FontRole.CaptionStrong, FindInk, TextAlign.Center));
        VybeChrome.Primary(frame, top.RightSlice(top.Width * 0.48f), "Apply", night);
        if (frame.Input.ConsumeClick(top.LeftSlice(top.Width * 0.48f)))
        {
            find.Clear();
            BindDiscoverSearch(string.Empty);
            state.Save(paths);
            return;
        }

        if (frame.Input.ConsumeClick(top.RightSlice(top.Width * 0.48f)))
        {
            state.Save(paths);
            state.Back();
            return;
        }

        var nearby = stack.Take(frame.Units(32f));
        DrawToggle(frame, nearby, "Appear in Nearby Discovery", state.NearbyDiscovery, night);
        if (frame.Input.ConsumeClick(nearby))
        {
            state.NearbyDiscovery = !state.NearbyDiscovery;
            state.Save(paths);
        }

        if (night)
        {
            var dating = stack.Take(frame.Units(32f));
            DrawToggle(frame, dating, "Appear in Dating Discovery", state.DatingDiscovery, night);
            if (frame.Input.ConsumeClick(dating))
            {
                state.DatingDiscovery = !state.DatingDiscovery;
                state.Save(paths);
            }
        }

        DrawChoiceRow(frame, stack.Take(frame.Units(28f)), "Mode", PeopleFindBook.Modes,
            PeopleFindBook.Modes[Math.Clamp(find.Mode, 0, PeopleFindBook.Modes.Length - 1)], night, value =>
            {
                var next = 0;
                for (var index = 0; index < PeopleFindBook.Modes.Length; index++)
                {
                    if (PeopleFindBook.Modes[index] == value)
                    {
                        next = index;
                        break;
                    }
                }

                find.Mode = next;
                if (next == 2)
                {
                    state.DatingDiscovery = true;
                }
            });
        DrawChipBlock(frame, ref stack, "Looking For", PeopleFindBook.LookingFor, find.LookingFor, night);
        DrawChipBlock(frame, ref stack, "Race", SceneBook.Races, find.Races, night);
        DrawChipBlock(frame, ref stack, "Location", PeopleFindBook.DataCenters, find.DataCenters, night);
        DrawChipBlock(frame, ref stack, "World", PeopleFindBook.Worlds, find.Worlds, night);
        DrawChipBlock(frame, ref stack, "Interests", Concat(PeopleFindBook.Play, PeopleFindBook.Social,
            PeopleFindBook.Creative), find.Interests, night);
        DrawChipBlock(frame, ref stack, "Activity", PeopleFindBook.Activity, find.Activity, night);
        DrawChoiceRow(frame, stack.Take(frame.Units(28f)), "Shared Interests", PeopleFindBook.Shared, find.SharedFloor,
            night, value => find.SharedFloor = value);

        var more = stack.Take(frame.Units(32f));
        VybeChrome.Glow(frame, more, frame.Units(12f), find.MoreOpen, night);
        frame.Text.DrawIn(more, find.MoreOpen ? "Less Filters" : "More Filters",
            new TextStyle(FontRole.CaptionStrong, FindInk, TextAlign.Center));
        if (frame.Input.ConsumeClick(more))
        {
            find.MoreOpen = !find.MoreOpen;
        }

        if (!find.MoreOpen)
        {
            DrawFilterFoot(frame, stack.Take(frame.Units(36f)), find, night);
            return;
        }

        DrawChipBlock(frame, ref stack, "Connection", PeopleFindBook.Connection, find.Connection, night);
        DrawChipBlock(frame, ref stack, "Nearby", PeopleFindBook.Nearby, find.Nearby, night);
        DrawChipBlock(frame, ref stack, "Role", PeopleFindBook.Roles, find.Roles, night);
        DrawChipBlock(frame, ref stack, "Job", PeopleFindBook.Jobs, find.Jobs, night);
        DrawChipBlock(frame, ref stack, "Roleplay", PeopleFindBook.RpInterest, find.RpInterest, night);
        DrawChipBlock(frame, ref stack, "RP Type", PeopleFindBook.RpTypes, find.RpTypes, night);
        DrawChoiceRow(frame, stack.Take(frame.Units(28f)), "Walk-Up RP", PeopleFindBook.WalkUp, find.WalkUp, night,
            value => find.WalkUp = value);
        DrawChipBlock(frame, ref stack, "Play Time", PeopleFindBook.PlayTimes, find.PlayTimes, night);
        var overlap = stack.Take(frame.Units(32f));
        DrawToggle(frame, overlap, "My play time overlaps theirs", find.OverlapPlay, night);
        if (frame.Input.ConsumeClick(overlap))
        {
            find.OverlapPlay = !find.OverlapPlay;
        }

        DrawChipBlock(frame, ref stack, "Communication", PeopleFindBook.Comm, find.Communication, night);
        DrawChipBlock(frame, ref stack, "Social Style", PeopleFindBook.Style, find.Social, night);
        if (state.DatingDiscovery)
        {
            DrawChipBlock(frame, ref stack, "Dating Intent", PeopleFindBook.DatingIntent, find.DatingIntent, night);
            DrawChipBlock(frame, ref stack, "Relationship Status", PeopleFindBook.Relationship, find.Status, night);
        }

        if (night)
        {
            DrawLanePriorities(frame, ref stack, find, night);
        }

        DrawFilterFoot(frame, stack.Take(frame.Units(36f)), find, night);
    }

    private void DrawLanePriorities(in AppletFrame frame, ref LayoutFlow stack, PeopleFindState find, bool night)
    {
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "SEARCH PRIORITIES", night);
        VybeChrome.Mute(frame, stack.Take(frame.Units(32f)),
            "Set how strongly you want each role. 0% ignores that lane. Match % uses these sliders.", night);
        var want = VybeLaneMap.Fit(find.LaneWant);
        for (var index = 0; index < VybeLaneMap.Lanes.Length; index++)
        {
            var lane = index;
            var row = stack.Take(frame.Units(46f));
            if (VybeLaneMap.DrawWant(frame, row, VybeLaneMap.Lanes[lane], want[lane], night, laneDrag, value =>
                {
                    want[lane] = value;
                    find.LaneWant = want;
                }, out laneDrag))
            {
                state.Save(paths);
            }
        }
    }

    private void DrawFilterFoot(in AppletFrame frame, Rect area, PeopleFindState find, bool night)
    {
        VybeChrome.Primary(frame, area, "Apply", night);
        if (frame.Input.ConsumeClick(area))
        {
            state.Save(paths);
            state.Back();
        }

        _ = find;
    }

    private void DrawChipBlock(in AppletFrame frame, ref LayoutFlow stack, string title, string[] options,
        List<string> picked, bool night)
    {
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), title.ToUpperInvariant(), night);
        const int per = 3;
        for (var index = 0; index < options.Length; index += per)
        {
            var row = stack.Take(frame.Units(28f));
            var w = row.Width / per;
            for (var col = 0; col < per && index + col < options.Length; col++)
            {
                var label = options[index + col];
                var cell = Rect.FromSize(new Vector2(row.Min.X + w * col + frame.Units(2f), row.Min.Y),
                    new Vector2(w - frame.Units(4f), row.Height));
                if (VybeChrome.Chip(frame, cell, Short(label), picked.Contains(label), night))
                {
                    PeopleFindState.Flip(picked, label);
                    state.Save(paths);
                }
            }
        }
    }

    private static void DrawChoiceRow(in AppletFrame frame, Rect area, string title, string[] options, string current,
        bool night, Action<string> set)
    {
        VybeChrome.Mute(frame, area.LeftSlice(frame.Units(86f)), title, night);
        var rest = area.Inset(new Edges(frame.Units(90f), 0f, 0f, 0f));
        var w = rest.Width / options.Length;
        for (var index = 0; index < options.Length; index++)
        {
            var cell = Rect.FromSize(new Vector2(rest.Min.X + w * index, rest.Min.Y), new Vector2(w - frame.Units(2f),
                rest.Height));
            var on = current == options[index] || (options[index] == "Any" && current.Length == 0);
            if (VybeChrome.Chip(frame, cell, options[index], on, night))
            {
                set(on && options[index] != "Any" ? string.Empty : options[index] == "Any" ? string.Empty : options[index]);
            }
        }
    }

    private static void DrawToggle(in AppletFrame frame, Rect area, string label, bool on, bool night)
    {
        VybeChrome.Glow(frame, area, frame.Units(12f), on, night);
        frame.Text.DrawIn(area.Inset(new Edges(frame.Units(10f), 0f)), label,
            new TextStyle(FontRole.CaptionStrong, FindInk));
    }

    private void OpenFound(PeopleCard card)
    {
        if (!state.TryFindGate(card.GateId, out _))
        {
            state.Roster.Add(new ScenePerson(card.Id, card.GateId, card.Name, card.Handle, card.World, card.Bio,
                card.Online, 4, false, new Vector4(0.20f, 0.22f, 0.28f, 1f), card.LookingFor, card.Interests,
                card.Avatar, card.Gender, card.Sexuality, card.Relationship, card.DmsOpen,
                TimeZoneId: card.TimeZoneId));
        }

        state.PersonKey = card.GateId;
        state.PersonIndex = card.Id;
        pearl.WatchProfile(card.GateId);
        state.Peek(NightPage.Person);
    }

    private static float DriveRail(in AppletFrame frame, Rect area, float drag, float span)
    {
        var max = MathF.Max(0f, span - area.Width);
        if (frame.Input.IsHovering(area) && frame.Input.IsHeld())
        {
            return Math.Clamp(drag - frame.Input.PointerDelta.X, 0f, max);
        }

        return Math.Clamp(drag, 0f, max);
    }

    private static string[] Take(string[] values, int count)
    {
        if (values.Length <= count)
        {
            return values;
        }

        var cut = new string[count];
        Array.Copy(values, cut, count);
        return cut;
    }

    private static string[] Concat(params string[][] packs)
    {
        var n = 0;
        for (var index = 0; index < packs.Length; index++)
        {
            n += packs[index].Length;
        }

        var all = new string[n];
        var at = 0;
        for (var index = 0; index < packs.Length; index++)
        {
            packs[index].CopyTo(all, at);
            at += packs[index].Length;
        }

        return all;
    }

    private static string Short(string label) =>
        label.Length > 16 ? label[..15] + "…" : label;
}
