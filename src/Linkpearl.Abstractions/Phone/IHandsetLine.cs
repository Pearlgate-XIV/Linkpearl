namespace Linkpearl.Phone;

public enum LineState : byte
{
    Idle = 0,
    Dialing = 1,
    Live = 2,
}

public readonly record struct LineContact(
    string Name,
    string Number,
    bool FromGate,
    string Race = "",
    string World = "");

public readonly record struct LineThread(string Number, string Title, string Preview, long LastUnix, int Unread);

public readonly record struct LineNote(bool Mine, string Body, long AtUnix);

public readonly record struct LineRecent(string Number, string Title, long AtUnix, bool Outgoing);

public interface IHandsetLine
{
    string Dial { get; set; }

    LineState State { get; }

    string PeerNumber { get; }

    string PeerName { get; }

    float Elapsed { get; }

    bool SpeakerMuted { get; }

    bool MicMuted { get; }

    string OwnNumber { get; }

    IReadOnlyList<LineContact> Contacts { get; }

    IReadOnlyList<LineThread> Threads { get; }

    IReadOnlyList<LineRecent> Recents { get; }

    IReadOnlyList<LineNote> Notes(string number);

    int UnreadTotal { get; }

    void AppendDigit(char digit);

    void Backspace();

    void ClearDial();

    void Paste(string raw);

    bool Call(string? number = null);

    void HangUp();

    void ToggleSpeakerMute();

    void ToggleMicMute();

    void Tick(float deltaSeconds);

    void SaveContact(string name, string number, string race = "", string world = "");

    LineContact SuggestContact(string number);

    void DropContact(string number);

    void SendNote(string number, string body);

    void MarkRead(string number);

    string TitleOf(string number);
}
