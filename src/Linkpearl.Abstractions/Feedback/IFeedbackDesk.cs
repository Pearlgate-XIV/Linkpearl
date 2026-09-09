namespace Linkpearl.Feedback;

public readonly record struct FeedbackNote(
    string Category,
    string Body,
    string Character,
    string World,
    IReadOnlyList<string>? Attachments = null);

public readonly record struct CrashPick(string Label, string Detail, IReadOnlyList<string> Paths);

public interface IFeedbackDesk
{
    bool Ready { get; }

    bool Busy { get; }

    string Status { get; }

    IReadOnlyList<CrashPick> RecentCrashes();

    void Send(FeedbackNote note);

    void OpenCommunity();
}
