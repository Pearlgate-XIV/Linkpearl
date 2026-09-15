using Linkpearl.Net;

namespace Linkpearl.Talk;

internal static class PearlSend
{
    public static bool CanSend(PearlSnapshot snapshot) =>
        snapshot.SignedIn && !snapshot.Muted && !snapshot.Banned;

    public static string BlockedHint(PearlSnapshot snapshot)
    {
        if (snapshot.Banned)
        {
            return "This account is suspended.";
        }

        if (snapshot.Muted)
        {
            return "Staff muted this handset.";
        }

        if (snapshot.Notice.Contains("Session expired", StringComparison.Ordinal))
        {
            return "Session expired. Sign in again.";
        }

        if (!snapshot.SignedIn)
        {
            return "Sign in from You to send.";
        }

        return "Chat is not available right now";
    }
}
