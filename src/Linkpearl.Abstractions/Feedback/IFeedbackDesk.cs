namespace Linkpearl.Feedback;

public readonly record struct FeedbackNote(string Category, string Body, string Character, string World);

public interface IFeedbackDesk
{
    bool Ready { get; }

    bool Busy { get; }

    string Status { get; }

    void Send(FeedbackNote note);

    void OpenCommunity();
}
