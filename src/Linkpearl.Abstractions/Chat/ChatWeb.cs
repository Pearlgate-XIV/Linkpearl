using System.Diagnostics;

namespace Linkpearl.Chat;

public static class ChatWeb
{
    public static bool Open(string url)
    {
        if (!ChatLinks.Safe(url, out var href))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(href) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
