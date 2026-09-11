namespace Linkpearl.Net;

public sealed class MockStreamDesk : IStreamDesk
{
    private StreamProviderAccount own = new(StreamProviderKind.Twitch, string.Empty, string.Empty, string.Empty,
        false, false);
    private readonly StreamInfo[] live =
    [
        new("twitch:alyxfm", StreamProviderKind.Twitch, StreamStatus.Live, "1001", "alyxfm", "Alyx",
            "Late crystal set", "https://www.twitch.tv/alyxfm", string.Empty, 18, "Electronic",
            "Mock Twitch row for development.", "The Pearl Room · Balmung", string.Empty, "mock"),
        new("twitch:lunawave", StreamProviderKind.Twitch, StreamStatus.Live, "1002", "lunawave", "Luna",
            "Chill night", "https://www.twitch.tv/lunawave", string.Empty, 7, "Chill",
            "Mock Twitch row for development.", string.Empty, string.Empty, "mock"),
        new("twitch:nyxdeck", StreamProviderKind.Twitch, StreamStatus.Offline, "1003", "nyxdeck", "Nyx",
            string.Empty, "https://www.twitch.tv/nyxdeck", string.Empty, 0, "Bass",
            "Mock Twitch row for development.", string.Empty, string.Empty, "mock"),
    ];

    public IReadOnlyList<StreamInfo> Live => live.Where(static row => row.Status == StreamStatus.Live).ToArray();

    public IReadOnlyList<StreamScheduleMark> CommunitySchedule { get; } = [];

    public StreamProviderAccount Own => own;

    public string Notice { get; private set; } = "Mock Twitch desk. Development only.";

    public bool Busy => false;

    public void Refresh()
    {
    }

    public void RequestConnect()
    {
        own = new StreamProviderAccount(StreamProviderKind.Twitch, "dev", "linkpearl", "Linkpearl", true, true);
        Notice = "Mock Twitch connected. Pearlgate will own the real link.";
    }

    public void Disconnect()
    {
        own = new StreamProviderAccount(StreamProviderKind.Twitch, string.Empty, string.Empty, string.Empty, false,
            false);
        Notice = "Mock Twitch disconnected.";
    }

    public StreamInfo? Find(string idOrLogin)
    {
        var key = StreamChrome.LoginOf(idOrLogin);
        if (key.Length == 0)
        {
            return null;
        }

        for (var index = 0; index < live.Length; index++)
        {
            var row = live[index];
            if (string.Equals(row.Id, idOrLogin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.Username, key, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return null;
    }

    public void Dispose()
    {
    }
}
