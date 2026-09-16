using System.Globalization;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Vybe;

public sealed partial class VybeApplet
{
    private static readonly Vector4 GroupHot = new(0.92f, 0.22f, 0.58f, 1f);
    private static readonly Vector4 GroupViolet = new(0.56f, 0.18f, 0.92f, 1f);
    private static readonly Vector4 GroupPill = new(0.12f, 0.12f, 0.14f, 1f);

    private void DrawGroups(in AppletFrame frame, ref LayoutFlow stack, bool night)
    {
        VybeGroups.Seed(state);
        var tone = VybeChrome.Tone(night);
        DrawGroupLanes(frame, stack.Take(frame.Units(34f)), night);
        if (VybeChrome.Chip(frame, stack.Take(frame.Units(34f)),
                night && CanPostPlus() ? "Create a VYBE or VYBE+ group" : "Create a group", false, night))
        {
            BeginClubCreate(night && state.GroupLane == GroupLane.Plus);
        }
        var rows = VybeGroups.Shown(state);
        if (rows.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), EmptyGroups(), night);
            return;
        }

        for (var index = 0; index < rows.Count; index++)
        {
            DrawGroupRow(frame, stack.Take(frame.Units(68f)), rows[index], tone);
        }
    }

    private void DrawGroupLanes(in AppletFrame frame, Rect area, bool night)
    {
        var labels = VybeGroups.LanesOf(night);
        var gap = frame.Units(6f);
        var cell = (area.Width - gap * (labels.Length - 1)) / labels.Length;
        for (var index = 0; index < labels.Length; index++)
        {
            var lane = (GroupLane)index;
            var on = state.GroupLane == lane;
            var dest = Rect.FromSize(new Vector2(area.Min.X + (cell + gap) * index, area.Min.Y),
                new Vector2(cell, area.Height));
            DrawGroupPill(frame, dest, labels[index], on);
            if (frame.Input.ConsumeClick(dest))
            {
                state.GroupLane = lane;
                state.Scroll = 0f;
            }
        }
    }

    private void DrawGroupRow(in AppletFrame frame, Rect area, SceneGroup group, NightPalette tone)
    {
        var face = area.LeftSlice(frame.Units(56f)).Inset(new Edges(0f, frame.Units(8f)));
        DrawGroupFace(frame, face, group);
        var go = area.RightSlice(frame.Units(78f)).Inset(new Edges(0f, frame.Units(18f), 0f, frame.Units(18f)));
        var copy = area.Inset(new Edges(frame.Units(64f), frame.Units(14f), frame.Units(86f), frame.Units(12f)));
        var nameRow = copy.TopSlice(frame.Units(20f));
        if (group.PlusOnly)
        {
            var tagW = MathF.Min(VybeChrome.PlusTagWidth(frame), nameRow.Width * 0.42f);
            frame.Text.DrawEllipsized(nameRow.Inset(new Edges(0f, 0f, tagW + frame.Units(4f), 0f)), group.Name,
                new TextStyle(FontRole.BodyStrong, tone.Ink));
            VybeChrome.PlusTag(frame, nameRow.RightSlice(tagW));
        }
        else
        {
            frame.Text.DrawEllipsized(nameRow, group.Name,
                new TextStyle(FontRole.BodyStrong, tone.Ink));
        }
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)),
            VybeGroups.Crowd(group.Members) + " · " + group.Tag,
            new TextStyle(FontRole.Caption, tone.Mute));
        var joined = VybeGroups.Joined(state, group.Id);
        var owned = VybeClubs.Owns(state, group.Id);
        DrawGroupPill(frame, go, owned ? "Yours" : joined ? "Joined" : "Join", !joined && !owned);
        if (!owned && frame.Input.ConsumeClick(go))
        {
            VybeGroups.Toggle(state, group.Id, paths);
            return;
        }

        if (frame.Input.ConsumeClick(area))
        {
            OpenClub(group.Id);
        }
    }

    private static void DrawGroupFace(in AppletFrame frame, Rect area, SceneGroup group)
    {
        var radius = frame.Units(12f);
        var path = File.Exists(group.File) ? group.File : VybeGroups.Face(frame.Paths, group.File);
        if (File.Exists(path))
        {
            var texture = frame.Textures.FromFile(path);
            if (texture is { IsReady: true })
            {
                var uv = CoverFit.Uv(texture.Size, area.Size);
                frame.Paint.ImageRounded(texture, area, uv.Min, uv.Max, Vector4.One, radius);
                return;
            }
        }

        frame.Paint.Fill(area, GroupPill, radius);
    }

    private static void DrawGroupPill(in AppletFrame frame, Rect area, string label, bool hot)
    {
        var radius = area.Height * 0.5f;
        if (hot)
        {
            frame.Paint.Fill(area, GroupHot, radius);
            frame.Paint.Fill(area.RightSlice(area.Width * 0.55f), GroupViolet with { W = 0.55f }, radius);
        }
        else
        {
            frame.Paint.Fill(area, GroupPill, radius);
        }

        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
    }

    private string EmptyGroups() => state.GroupLane switch
    {
        GroupLane.Mine => "Create a group or join one and it lands here.",
        GroupLane.Nearby => "No nearby groups right now.",
        GroupLane.Plus => "No VYBE+ groups yet.",
        _ => "No groups match that search.",
    };

    private void BeginClubCreate(bool plus)
    {
        state.DraftClubName = string.Empty;
        state.DraftClubAbout = string.Empty;
        state.DraftClubFace = string.Empty;
        state.DraftClubPlus = plus && CanPostPlus();
        state.PickingClubFace = false;
        state.Open(NightPage.ClubCreate);
    }

    private void OpenClub(string id)
    {
        if (id.Length == 0)
        {
            return;
        }

        state.ClubKey = id;
        state.ClubPane = 0;
        state.Open(NightPage.Club);
    }

    private void DrawClubPage(in AppletFrame frame, Rect area, string id, bool night)
    {
        var found = VybeClubs.SceneOf(state, id);
        if (found is not { } group)
        {
            state.ActingAsClubId = string.Empty;
            if (state.Page == NightPage.Club)
            {
                state.Back();
            }

            return;
        }

        var owned = VybeClubs.Owns(state, id);
        var tone = VybeChrome.Tone(night);
        var stack = new LayoutFlow(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), group.Name, night))
        {
            if (state.Page == NightPage.Club)
            {
                state.Back();
            }
            else
            {
                state.ActingAsClubId = string.Empty;
                state.Save(paths);
            }

            return;
        }
        var hero = stack.Take(frame.Units(88f));
        VybeChrome.Plate(frame, hero, frame.Units(14f), night);
        var face = hero.LeftSlice(frame.Units(88f)).Inset(frame.Units(12f));
        DrawGroupFace(frame, face, group);
        var copy = hero.Inset(new Edges(frame.Units(92f), frame.Units(12f), frame.Units(10f), frame.Units(12f)));
        if (group.PlusOnly)
        {
            var tagW = MathF.Min(VybeChrome.PlusTagWidth(frame), copy.Width * 0.4f);
            frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(22f)).Inset(new Edges(0f, 0f, tagW + frame.Units(4f), 0f)),
                group.Name, new TextStyle(FontRole.BodyStrong, tone.Ink));
            VybeChrome.PlusTag(frame, copy.TopSlice(frame.Units(22f)).RightSlice(tagW));
        }
        else
        {
            VybeChrome.Title(frame, copy.TopSlice(frame.Units(22f)), group.Name, night);
        }

        VybeChrome.Mute(frame, copy.Inset(new Edges(0f, frame.Units(24f), 0f, frame.Units(22f))),
            VybeGroups.Crowd(group.Members) + " · " + (group.PlusOnly ? "VYBE+" : "VYBE"), night);
        if (owned)
        {
            var usingClub = string.Equals(state.ActingAsClubId, id, StringComparison.Ordinal);
            if (VybeChrome.Chip(frame, copy.BottomSlice(frame.Units(22f)),
                    usingClub ? "Using this profile" : "Use this profile", usingClub, night))
            {
                state.ActingAsClubId = usingClub ? string.Empty : id;
                state.Save(paths);
            }
        }
        else
        {
            var joined = VybeGroups.Joined(state, id);
            if (VybeChrome.Chip(frame, copy.BottomSlice(frame.Units(22f)), joined ? "Joined" : "Join", !joined,
                    night))
            {
                VybeGroups.Toggle(state, id, paths);
            }
        }

        if (VybeClubs.TryFind(state, id, out var club) && club.About.Length > 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(32f)), club.About, night);
        }

        var posts = VybeClubs.PostsFor(state, id, groupWall);
        var likes = 0;
        for (var index = 0; index < posts.Length; index++)
        {
            likes += Math.Max(0, posts[index].Likes);
        }

        var followers = group.Members;
        if (VybeGroups.Joined(state, id) && followers < 1)
        {
            followers = 1;
        }

        DrawClubStats(frame, stack.Take(frame.Units(44f)), night,
            CompactCount(posts.Length), CompactCount(followers), CompactCount(likes));

        if (owned)
        {
            var acts = stack.Take(frame.Units(36f));
            var post = acts.LeftSlice((acts.Width - frame.Units(8f)) * 0.5f);
            var ev = acts.RightSlice((acts.Width - frame.Units(8f)) * 0.5f);
            if (VybeChrome.Chip(frame, post, "Post", true, night))
            {
                state.ActingAsClubId = id;
                state.Save(paths);
                BeginCompose(group.PlusOnly);
                return;
            }

            if (VybeChrome.Chip(frame, ev, "New event", false, night))
            {
                state.ClubKey = id;
                state.DraftEventTitle = string.Empty;
                state.DraftEventWhen = 0;
                state.Open(NightPage.ClubEvent);
                return;
            }
        }

        var panes = stack.Take(frame.Units(34f));
        var labels = new[] { "Posts", "Gallery", "Events" };
        var pane = VybeChrome.TextTabs(frame, panes, labels, Math.Clamp(state.ClubPane, 0, 2), night);
        if (pane != state.ClubPane)
        {
            state.ClubPane = pane;
        }

        if (state.ClubPane == 1)
        {
            DrawPhotoGrid(frame, stack, posts, night);
            return;
        }

        if (state.ClubPane == 2)
        {
            DrawClubEvents(frame, ref stack, id, owned, night);
            return;
        }

        if (posts.Length == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                owned ? "Posts from this group land here." : "No posts in this group yet.", night);
            return;
        }

        foreach (var wallPost in posts)
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
        }
    }

    private static void DrawClubStats(in AppletFrame frame, Rect area, bool night, string posts, string followers,
        string likes)
    {
        var w = area.Width / 3f;
        DrawStat(frame, Rect.FromSize(area.Min, new Vector2(w, area.Height)), posts, "posts", night);
        DrawStat(frame, Rect.FromSize(new Vector2(area.Min.X + w, area.Min.Y), new Vector2(w, area.Height)),
            followers, "followers", night);
        DrawStat(frame, Rect.FromSize(new Vector2(area.Min.X + w * 2f, area.Min.Y), new Vector2(w, area.Height)),
            likes, "likes", night);
    }

    private void DrawClubEvents(in AppletFrame frame, ref LayoutFlow stack, string clubId, bool owned, bool night)
    {
        var events = VybeClubs.Events(calendar, clubId);
        if (owned && VybeChrome.Chip(frame, stack.Take(frame.Units(34f)), "Create event", true, night))
        {
            state.ClubKey = clubId;
            state.DraftEventTitle = string.Empty;
            state.DraftEventWhen = 0;
            state.Open(NightPage.ClubEvent);
            return;
        }

        if (events.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                owned ? "Schedule a VYBE event and it lands on Calendar." : "No upcoming events.", night);
            return;
        }

        for (var index = 0; index < events.Count; index++)
        {
            var item = events[index];
            var row = stack.Take(frame.Units(52f));
            VybeChrome.Plate(frame, row, frame.Units(12f), night);
            var inner = row.Inset(new Edges(frame.Units(10f), frame.Units(8f)));
            VybeChrome.Title(frame, inner.TopSlice(frame.Units(18f)), item.Title, night);
            VybeChrome.Mute(frame, inner.BottomSlice(frame.Units(16f)),
                item.StartsAt.ToLocalTime().ToString("ddd d MMM · h:mm tt", CultureInfo.CurrentCulture), night);
        }
    }

    private void DrawClubCreate(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new LayoutFlow(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "New group", night))
        {
            state.Back();
            return;
        }

        VybeChrome.Note(frame, stack.Take(frame.Units(32f)),
            "This group gets its own profile. Switch to it to post, run a gallery, and host events.",
            night);
        state.DraftClubName = frame.TextField.Draw("vybe-club-name", stack.Take(frame.Units(36f)),
            state.DraftClubName, "Group name");
        state.DraftClubAbout = frame.TextField.Draw("vybe-club-about", stack.Take(frame.Units(48f)),
            state.DraftClubAbout, "What is this group about?");
        if (VybeChrome.Chip(frame, stack.Take(frame.Units(34f)),
                state.DraftClubFace.Length > 0 ? "Photo attached" : "Add a photo",
                state.DraftClubFace.Length > 0, night))
        {
            state.PickingClubFace = true;
            state.PickingAvatar = false;
            state.PickingBanner = false;
            state.Open(NightPage.PhotoPick);
            return;
        }

        if (CanPostPlus())
        {
            var lane = stack.Take(frame.Units(36f));
            var sfw = lane.LeftSlice((lane.Width - frame.Units(8f)) * 0.5f);
            var plus = lane.RightSlice((lane.Width - frame.Units(8f)) * 0.5f);
            if (VybeChrome.Chip(frame, sfw, "VYBE", !state.DraftClubPlus, night))
            {
                state.DraftClubPlus = false;
            }

            if (VybeChrome.Chip(frame, plus, "VYBE+", state.DraftClubPlus, night))
            {
                state.DraftClubPlus = true;
            }
        }
        else
        {
            state.DraftClubPlus = false;
            VybeChrome.Mute(frame, stack.Take(frame.Units(24f)), "This group posts on VYBE.", night);
        }

        var ready = state.DraftClubName.Trim().Length > 0;
        var go = stack.Take(frame.Units(44f));
        VybeChrome.Primary(frame, go, ready ? "Create group" : "Name the group", night);
        if (!ready || !frame.Input.ConsumeClick(go))
        {
            return;
        }

        var club = new VybeClub
        {
            Id = "club-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            Name = state.DraftClubName.Trim(),
            About = state.DraftClubAbout.Trim(),
            FacePath = state.DraftClubFace,
            PlusOnly = state.DraftClubPlus && CanPostPlus(),
            Members = 1,
            BornUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        state.Clubs.Insert(0, club);
        state.JoinedGroups.Add(club.Id);
        state.ActingAsClubId = club.Id;
        state.ClubKey = club.Id;
        state.Save(paths);
        state.Open(NightPage.Club);
    }

    private void DrawClubEvent(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new LayoutFlow(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "New event", night))
        {
            state.Back();
            return;
        }

        if (!VybeClubs.Owns(state, state.ClubKey))
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), "Only the group profile can host events.", night);
            return;
        }

        state.DraftEventTitle = frame.TextField.Draw("vybe-club-event", stack.Take(frame.Units(36f)),
            state.DraftEventTitle, "Event name");
        var when = stack.Take(frame.Units(34f));
        var cell = when.Width / 3f;
        var labels = new[] { "Tonight", "Tomorrow", "Weekend" };
        for (var index = 0; index < labels.Length; index++)
        {
            var hit = Rect.FromSize(new Vector2(when.Min.X + cell * index, when.Min.Y),
                new Vector2(cell - frame.Units(4f), when.Height));
            if (VybeChrome.Chip(frame, hit, labels[index], state.DraftEventWhen == index, night))
            {
                state.DraftEventWhen = index;
            }
        }

        VybeChrome.Note(frame, stack.Take(frame.Units(28f)),
            "This lands on Calendar and on the group's Events tab.", night);
        var go = stack.Take(frame.Units(44f));
        var ready = state.DraftEventTitle.Trim().Length > 0;
        VybeChrome.Primary(frame, go, ready ? "Create event" : "Name the event", night);
        if (!ready || !frame.Input.ConsumeClick(go))
        {
            return;
        }

        VybeClubs.PutEvent(calendar, state.ClubKey, state.DraftEventTitle.Trim(), ClubEventStart(state.DraftEventWhen));
        state.DraftEventTitle = string.Empty;
        state.ClubPane = 2;
        state.Back();
    }

    private static DateTimeOffset ClubEventStart(int pick)
    {
        var local = DateTime.Now.Date.AddHours(20);
        if (pick == 1)
        {
            local = local.AddDays(1);
        }
        else if (pick == 2)
        {
            var days = ((int)DayOfWeek.Saturday - (int)local.DayOfWeek + 7) % 7;
            local = local.AddDays(days == 0 ? 7 : days);
        }

        return new DateTimeOffset(local);
    }
}
