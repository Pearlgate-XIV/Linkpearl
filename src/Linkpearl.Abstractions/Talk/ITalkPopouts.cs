namespace Linkpearl.Talk;

// Per-thread pop-out watch. Tells share one tabbed dock; other rooms keep their own window.
// Arming a thread keeps the button on until the user turns it off. Closing a tab or the
// dock hides the window but leaves the thread armed so the next inbound line brings it back.
public interface ITalkPopouts
{
    bool IsArmed(string threadId);

    void SetArmed(string threadId, bool armed);

    void Toggle(string threadId);
}
