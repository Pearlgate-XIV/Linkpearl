using Linkpearl.Net;

namespace Linkpearl.Feedback;

public static class StaffReportDispatch
{
    public static void File(IPearlHub pearl, IFeedbackDesk? desk, string character, string world,
        string targetType, string targetId, string reason, string detail,
        IReadOnlyList<PearlReportLine>? messages = null)
    {
        if (messages is { Count: > 0 })
        {
            pearl.Report(targetType, targetId, reason, detail, messages);
        }
        else
        {
            pearl.Report(targetType, targetId, reason, detail);
        }

        if (desk is null)
        {
            return;
        }

        var body = targetType + " · " + targetId + "\n" + detail;
        desk.Send(new FeedbackNote(targetType + " report · " + reason, body, character, world));
    }
}
