using System.IO;
using Linkpearl.Applets;
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
    private static readonly Vector4 GroupField = new(0.10f, 0.10f, 0.12f, 1f);

    private void DrawGroups(in AppletFrame frame, Rect area)
    {
        VybeGroups.Seed(state);
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        DrawGroupsHead(frame, stack.Take(frame.Units(28f)), tone, night);
        DrawGroupsHunt(frame, stack.Take(frame.Units(36f)), tone);
        DrawGroupLanes(frame, stack.Take(frame.Units(34f)));
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

    private void DrawGroupsHead(in AppletFrame frame, Rect area, NightPalette tone, bool night)
    {
        var back = area.LeftSlice(frame.Units(28f));
        frame.Text.DrawIn(back, "‹", new TextStyle(FontRole.Title, tone.Ink, TextAlign.Center));
        frame.Text.DrawIn(area, "Groups", new TextStyle(FontRole.Title, tone.Ink, TextAlign.Center));
        var hunt = area.RightSlice(frame.Units(28f));
        DrawSearchGlyph(frame, hunt.Center, frame.Units(8f),
            state.GroupHuntOpen ? tone.Accent : tone.Ink);
        if (frame.Input.ConsumeClick(back) || frame.Input.ConsumeClick(area.LeftSlice(frame.Units(40f))))
        {
            state.DiscoverPane = 0;
            state.Scroll = 0f;
        }

        if (frame.Input.ConsumeClick(hunt))
        {
            state.GroupHuntOpen = !state.GroupHuntOpen;
            if (!state.GroupHuntOpen)
            {
                state.GroupQuery = string.Empty;
            }
        }
    }

    private void DrawGroupsHunt(in AppletFrame frame, Rect area, NightPalette tone)
    {
        frame.Paint.Fill(area, GroupField, area.Height * 0.5f);
        DrawSearchGlyph(frame, new Vector2(area.Min.X + frame.Units(16f), area.Center.Y), frame.Units(6.5f),
            tone.Mute);
        state.GroupQuery = frame.TextField.Draw("vybe-groups-q",
            area.Inset(new Edges(frame.Units(32f), frame.Units(4f), frame.Units(10f), frame.Units(4f))),
            state.GroupQuery, "Search groups...");
    }

    private void DrawGroupLanes(in AppletFrame frame, Rect area)
    {
        var gap = frame.Units(6f);
        var cell = (area.Width - gap * (VybeGroups.Lanes.Length - 1)) / VybeGroups.Lanes.Length;
        for (var index = 0; index < VybeGroups.Lanes.Length; index++)
        {
            var lane = (GroupLane)index;
            var on = state.GroupLane == lane;
            var dest = Rect.FromSize(new Vector2(area.Min.X + (cell + gap) * index, area.Min.Y),
                new Vector2(cell, area.Height));
            DrawGroupPill(frame, dest, VybeGroups.Lanes[index], on);
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
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(20f)), group.Name,
            new TextStyle(FontRole.BodyStrong, tone.Ink));
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)),
            VybeGroups.Crowd(group.Members) + " · " + group.Tag,
            new TextStyle(FontRole.Caption, tone.Mute));
        var joined = VybeGroups.Joined(state, group.Id);
        DrawGroupPill(frame, go, joined ? "Joined" : "Join", !joined);
        if (frame.Input.ConsumeClick(go))
        {
            VybeGroups.Toggle(state, group.Id, paths);
        }
    }

    private static void DrawGroupFace(in AppletFrame frame, Rect area, SceneGroup group)
    {
        var radius = frame.Units(12f);
        var path = VybeGroups.Face(frame.Paths, group.File);
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
        GroupLane.Mine => "Join a group and it lands here.",
        GroupLane.Nearby => "No nearby groups right now.",
        _ => "No groups match that search.",
    };
}
