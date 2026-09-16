using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Painting;

namespace Linkpearl.Feedback;

public enum ReportSheetResult : byte
{
    None = 0,
    Submit = 1,
    Cancel = 2,
}

public static class HandsetReportSheet
{
    public static ReportSheetResult Draw(in AppletFrame frame, Rect area, string heading, string fieldId,
        ref int reason, ref string detail, ref bool fresh)
    {
        frame.Paint.Fill(area, new Vector4(0f, 0f, 0f, 0.62f));
        var card = Rect.FromSize(
            new Vector2(area.Min.X + frame.Units(10f), area.Center.Y - frame.Units(168f)),
            new Vector2(area.Width - frame.Units(20f), frame.Units(300f)));
        var radius = frame.Units(16f);
        frame.Paint.Fill(card, frame.Theme.Palette.SurfaceRaised, radius);
        var stack = new LayoutFlow(card.Inset(frame.Units(14f)), StackAxis.Vertical, frame.Units(8f));
        frame.Text.DrawIn(stack.Take(frame.Units(26f)), heading,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Select reason",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var reasonRow = stack.Take(frame.Units(40f));
        frame.Paint.Fill(reasonRow, frame.Theme.Palette.Surface, frame.Units(10f));
        reason = frame.TextField.Combo(fieldId + "-reason", reasonRow.Inset(frame.Units(6f)),
            StaffReports.Reasons, reason);
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Add additional information (optional)",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var note = stack.Take(frame.Units(72f));
        frame.Paint.Fill(note, frame.Theme.Palette.Surface, frame.Units(10f));
        detail = frame.TextField.Write(fieldId + "-detail", note.Inset(frame.Units(8f)), detail,
            "Add additional information (optional)", 800);
        var row = stack.Take(frame.Units(40f));
        var cancel = row.LeftSlice(row.Width * 0.48f);
        var send = row.RightSlice(row.Width * 0.48f);
        frame.Paint.Fill(cancel, frame.Theme.Palette.Surface, frame.Units(12f));
        frame.Text.DrawIn(cancel, "Cancel",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink, TextAlign.Center));
        var ready = reason > 0;
        frame.Paint.Fill(send, ready ? frame.Theme.Palette.Accent : frame.Theme.Palette.Surface, frame.Units(12f));
        frame.Text.DrawIn(send, "Submit",
            new TextStyle(FontRole.CaptionStrong,
                ready ? frame.Theme.Palette.AccentInk : frame.Theme.Palette.InkMuted, TextAlign.Center));
        var dismiss = !fresh &&
            (frame.Input.WasClicked(cancel) ||
             (!card.Contains(frame.Input.Cursor) && frame.Input.WasClicked(area)));
        fresh = false;
        if (dismiss)
        {
            frame.TextField.Release();
            return ReportSheetResult.Cancel;
        }

        if (ready && frame.Input.ConsumeClick(send))
        {
            return ReportSheetResult.Submit;
        }

        return ReportSheetResult.None;
    }
}
