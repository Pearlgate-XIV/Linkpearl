using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Linkpearl.Applets;
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

    private void DrawPeopleDiscovery(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var find = state.PeopleFind;
        find.Pulse = MathF.Max(0f, find.Pulse - frame.DeltaSeconds);
        find.PassFade = MathF.Max(0f, find.PassFade - frame.DeltaSeconds);
        findDeck = PeopleFindBook.Deck(paths);

        var mine = PeopleFindBook.Mine(state);
        var home = game.Character.WorldName;
        var picks = PeopleFindBook.Match(findDeck, find, state, home, mine);
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        DrawPeopleHead(frame, stack.Take(frame.Units(28f)), find, tone, night);
        DrawDiscoverPanes(frame, stack.Take(frame.Units(34f)), night);
        if (night)
        {
            var next = DrawPlusSplit(frame, stack.Take(frame.Units(56f)), "People", state.PeoplePlus, night);
            if (next != state.PeoplePlus)
            {
                state.PeoplePlus = next;
                state.Scroll = 0f;
            }
        }
        if (state.DiscoverPane != 0)
        {
            return;
        }

        if (find.SearchOpen)
        {
            find.Query = frame.TextField.Draw("vybe-people-q", stack.Take(frame.Units(32f)), find.Query,
                "Search name, world, job");
        }

        DrawAppliedFilters(frame, ref stack, find, night);

        if (picks.Count == 0)
        {
            DrawPeopleEmpty(frame, stack.Take(frame.Units(120f)), find, night);
            return;
        }

        var gap = frame.Units(8f);
        var cardH = frame.Units(312f);
        for (var index = 0; index < picks.Count; index += 2)
        {
            var row = stack.Take(cardH);
            var half = (row.Width - gap) * 0.5f;
            DrawPeopleTile(frame, Rect.FromSize(row.Min, new Vector2(half, row.Height)), picks[index], find, mine,
                home, night);
            if (index + 1 < picks.Count)
            {
                DrawPeopleTile(frame,
                    Rect.FromSize(new Vector2(row.Min.X + half + gap, row.Min.Y), new Vector2(half, row.Height)),
                    picks[index + 1], find, mine, home, night);
            }
        }

        _ = tone;
    }

    private void DrawPeopleHead(in AppletFrame frame, Rect area, PeopleFindState find, NightPalette tone, bool night)
    {
        if (VybeChrome.Back(frame, area.LeftSlice(frame.Units(28f)), string.Empty, night))
        {
            find.SearchOpen = false;
            find.Query = string.Empty;
            return;
        }

        VybeChrome.Title(frame, area.Inset(new Edges(frame.Units(32f), 0f, frame.Units(72f), 0f)), "People",
            night);
        var tools = area.RightSlice(frame.Units(64f));
        var search = tools.LeftSlice(frame.Units(28f));
        var filter = tools.RightSlice(frame.Units(28f));
        DrawSearchGlyph(frame, search.Center, frame.Units(8f), find.SearchOpen ? tone.Accent : FindInk);
        DrawFilterGlyph(frame, filter.Center, frame.Units(9f), find.Count() > 0 ? tone.Accent : FindInk);
        if (frame.Input.ConsumeClick(search))
        {
            find.SearchOpen = !find.SearchOpen;
            if (!find.SearchOpen)
            {
                find.Query = string.Empty;
            }
        }

        if (frame.Input.ConsumeClick(filter))
        {
            state.Peek(NightPage.Filters);
        }
    }

    private void DrawAppliedFilters(in AppletFrame frame, ref Stack stack, PeopleFindState find, bool night)
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
                state.Save(paths);
            }

            x += widths[index] + gap;
        }
    }

    private void DrawPeopleEmpty(in AppletFrame frame, Rect area, PeopleFindState find, bool night)
    {
        VybeChrome.Plate(frame, area, frame.Units(16f), night);
        var inner = area.Inset(frame.Units(14f));
        VybeChrome.Title(frame, inner.TopSlice(frame.Units(24f)),
            state.PeoplePlus ? "No VYBE+ people yet." : "No one matches those filters yet.", night);
        VybeChrome.Mute(frame, inner.Inset(new Edges(0f, frame.Units(28f), 0f, frame.Units(52f))),
            "Open Filters to widen the search, or tap a chip above to drop it.", night);
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
        frame.Paint.Fill(area, FindLift, frame.Units(16f));
        frame.Paint.Stroke(area, FindLine, frame.Units(1f), frame.Units(16f));
        var photo = area.TopSlice(frame.Units(148f)).Inset(new Edges(frame.Units(6f), frame.Units(6f), frame.Units(6f),
            0f));
        DrawPeopleCover(frame, photo, card, night);
        var live = new Vector2(photo.Min.X + frame.Units(10f), photo.Min.Y + frame.Units(10f));
        frame.Paint.FillCircle(live, frame.Units(5.2f), new Vector4(0f, 0f, 0f, 0.45f));
        frame.Paint.FillCircle(live, frame.Units(3.8f),
            card.Online ? VybeChrome.Online : new Vector4(0.52f, 0.52f, 0.56f, 1f));
        var heart = Rect.FromSize(new Vector2(photo.Max.X - frame.Units(24f), photo.Min.Y + frame.Units(6f)),
            new Vector2(frame.Units(18f), frame.Units(18f)));
        var liked = state.LikedPeople.Contains(card.Id);
        DrawHottHeart(frame, heart, tone.Accent, liked);
        var copy = area.Inset(new Edges(frame.Units(8f), frame.Units(156f), frame.Units(8f), frame.Units(6f)));
        var lines = new Stack(copy, StackAxis.Vertical, frame.Units(1.5f));
        var nameRow = lines.Take(frame.Units(16f));
        if (card.PlusMember || card.PlusOnly)
        {
            var tagW = MathF.Min(VybeChrome.PlusTagWidth(frame), nameRow.Width * 0.42f);
            frame.Text.DrawEllipsized(nameRow.Inset(new Edges(0f, 0f, tagW + frame.Units(4f), 0f)), card.Name,
                new TextStyle(FontRole.CaptionStrong, FindInk));
            VybeChrome.PlusTag(frame, nameRow.RightSlice(tagW));
        }
        else
        {
            frame.Text.DrawEllipsized(nameRow, card.Name,
                new TextStyle(FontRole.CaptionStrong, FindInk));
        }
        var place = card.World + " • " + card.DataCenter;
        if (card.Age > 0 && card.DatingOn && state.DatingDiscovery)
        {
            place = card.Age.ToString(CultureInfo.InvariantCulture) + " • " + place;
        }

        frame.Text.DrawEllipsized(lines.Take(frame.Units(12f)), card.Handle + "  " + place,
            new TextStyle(FontRole.Caption, FindMute));
        frame.Text.DrawEllipsized(lines.Take(frame.Units(12f)), card.Job + " · " + card.Role,
            new TextStyle(FontRole.CaptionStrong, FindInk));
        frame.Text.DrawEllipsized(lines.Take(frame.Units(12f)), string.Join(" • ", Take(card.LookingFor, 3)),
            new TextStyle(FontRole.Caption, FindMute));
        var shared = PeopleFindBook.SharedCount(card.Interests, mine);
        frame.Text.DrawEllipsized(lines.Take(frame.Units(12f)),
            shared.ToString(CultureInfo.InvariantCulture) + " shared  ·  " + string.Join(" ", Take(card.Interests, 2)),
            new TextStyle(FontRole.Caption, tone.Accent));
        frame.Text.DrawEllipsized(lines.Take(frame.Units(12f)), card.Bio,
            new TextStyle(FontRole.Caption, FindMute));
        frame.Text.DrawEllipsized(lines.Take(frame.Units(12f)),
            card.Online ? "Online now" : card.Activity == "Today" ? "Active today" : "Active this week",
            new TextStyle(FontRole.Caption, card.Online ? VybeChrome.Online : FindMute));
        var score = PeopleFindBook.Score(card, find, home, PeopleFindBook.DataCenterOf(home), mine);
        frame.Text.DrawEllipsized(lines.Take(frame.Units(12f)),
            score.ToString(CultureInfo.InvariantCulture) + "% match",
            new TextStyle(FontRole.Caption, tone.Accent));
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
        var stack = new Stack(body, StackAxis.Vertical, frame.Units(3f));
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
        var score = PeopleFindBook.Score(card, find, home, homeDc, mine);
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
            var reasons = PeopleFindBook.Reasons(card, home, homeDc, mine);
            frame.Text.DrawEllipsized(stack.Take(frame.Units(28f)), string.Join("  ·  ", reasons),
                new TextStyle(FontRole.Caption, FindMute));
        }

        var actions = stack.Take(frame.Units(36f));
        var slot = actions.Width / 4f;
        DrawPeopleAct(frame, Slice(actions, 0, slot), "✕", FindMute, false);
        DrawPeopleAct(frame, Slice(actions, 1, slot), "★", find.Saved.Contains(card.Id) ? tone.Accent : FindInk,
            find.Saved.Contains(card.Id));
        var liked = state.LikedPeople.Contains(card.Id);
        var heart = Slice(actions, 2, slot);
        var pulse = find.PulseId == card.Id ? 1f + find.Pulse * 0.18f : 1f;
        DrawPeopleAct(frame, heart.Inset(heart.Width * (1f - pulse) * 0.5f), liked ? "♥" : "♡",
            liked ? tone.Accent : FindInk, liked);
        VybeChrome.Glow(frame, Slice(actions, 3, slot), frame.Units(10f), false, night);
        frame.Text.DrawIn(Slice(actions, 3, slot), "View",
            new TextStyle(FontRole.CaptionStrong, FindInk, TextAlign.Center));
        if (frame.Input.ConsumeClick(Slice(actions, 0, slot)))
        {
            find.LastPass = card.Id;
            find.PassFade = 0.28f;
            if (!find.Passed.Contains(card.Id))
            {
                find.Passed.Add(card.Id);
            }

            state.Save(paths);
            return;
        }

        if (frame.Input.ConsumeClick(Slice(actions, 1, slot)))
        {
            PeopleFindState.FlipInt(find.Saved, card.Id);
            state.Save(paths);
            return;
        }

        if (frame.Input.ConsumeClick(heart))
        {
            state.ToggleLikedPerson(card.Id);
            find.PulseId = card.Id;
            find.Pulse = 0.22f;
            state.Save(paths);
            return;
        }

        if (frame.Input.ConsumeClick(Slice(actions, 3, slot)) || frame.Input.ConsumeClick(photo))
        {
            OpenFound(card);
        }
    }

    private void DrawPeopleCover(in AppletFrame frame, Rect area, PeopleCard card, bool night)
    {
        var person = new ScenePerson(card.Id, card.GateId, card.Name, card.Handle, card.World, card.Bio, card.Online,
            4, card.PlusOnly, new Vector4(0f, 0f, 0f, 0.42f), card.LookingFor, card.Interests, card.Avatar,
            PlusMember: card.PlusMember, TimeZoneId: card.TimeZoneId);
        DrawPersonCover(frame, area, person, night);
        frame.Paint.FillGradient(area, new Vector4(0f, 0f, 0f, 0.04f), new Vector4(0f, 0f, 0f, 0.55f),
            GradientAxis.Vertical);
    }

    private static void DrawPeopleAct(in AppletFrame frame, Rect area, string mark, Vector4 ink, bool on)
    {
        frame.Paint.Fill(area.Inset(frame.Units(2f)), on ? FindLine : FindLift, area.Height * 0.5f);
        frame.Paint.Stroke(area.Inset(frame.Units(2f)), FindLine, frame.Units(1f), area.Height * 0.5f);
        frame.Text.DrawIn(area, mark, new TextStyle(FontRole.BodyStrong, ink, TextAlign.Center));
    }

    private void DrawPeopleFilters(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var find = state.PeopleFind;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        var head = stack.Take(frame.Units(28f));
        if (VybeChrome.Back(frame, head.LeftSlice(frame.Units(72f)), "Filters", night))
        {
            state.Back();
            return;
        }

        var count = find.Count();
        frame.Text.DrawIn(head.RightSlice(frame.Units(90f)),
            count > 0 ? "Filters (" + count.ToString(CultureInfo.InvariantCulture) + ")" : "People Filters",
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
        DrawChipBlock(frame, ref stack, "Race", PeopleFindBook.Races, find.Races, night);
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

        DrawFilterFoot(frame, stack.Take(frame.Units(36f)), find, night);
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

    private void DrawChipBlock(in AppletFrame frame, ref Stack stack, string title, string[] options,
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

    private static Rect Slice(Rect area, int index, float width) =>
        Rect.FromSize(new Vector2(area.Min.X + width * index, area.Min.Y), new Vector2(width, area.Height));

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
