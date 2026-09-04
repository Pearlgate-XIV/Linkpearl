namespace Linkpearl.Talk;

// Per-thread pop-out watch. Armed stays on after the window is closed; the next incoming
// line reopens that window at its last place. No cap on how many threads can be armed.
public interface ITalkPopouts
{
    bool IsArmed(string threadId);

    void SetArmed(string threadId, bool armed);

    void Toggle(string threadId);
}
