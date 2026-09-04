using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Linkpearl.Diagnostics;
using Linkpearl.Feedback;
using Linkpearl.Modules;
using Linkpearl.Time;

namespace Linkpearl.Host.Feedback;

public sealed class DiscordFeedbackDesk : IFeedbackDesk, IDisposable
{
    public const string CommunityUrl = "https://discord.gg/KBf4wrzS6F";
    private const string ChannelHook =
        "https://discord.com/api/webhooks/1545314343615205416/_MFB8lnIqJXRadIrlrl4aStnCxeAueos-_ecBh4TCAZFfat0NzEmmFXLDmcp8KchHA3C";
    private const int CooldownSeconds = 15;
    private const int EmbedDescriptionCap = 4096;

    private readonly HostEnvironment environment;
    private readonly IClock clock;
    private readonly IFrameLoop loop;
    private readonly ILinkpearlLog log;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private DateTimeOffset nextSend = DateTimeOffset.MinValue;
    private bool busy;
    private string status = "Choose a category and write what you want us to see.";

    public DiscordFeedbackDesk(HostEnvironment environment, IClock clock, IFrameLoop loop, ILinkpearlLog log)
    {
        this.environment = environment;
        this.clock = clock;
        this.loop = loop;
        this.log = log.Scoped("feedback");
    }

    public bool Ready => true;

    public bool Busy => busy;

    public string Status => status;

    public void Send(FeedbackNote note)
    {
        if (busy)
        {
            return;
        }

        var wait = nextSend - clock.UtcNow;
        if (wait.TotalSeconds > 0)
        {
            status = "Wait " + Math.Ceiling(wait.TotalSeconds) + "s before sending another note.";
            return;
        }

        var body = (note.Body ?? string.Empty).Trim();
        if (body.Length == 0)
        {
            status = "Write something before sending.";
            return;
        }

        if (body.Length > EmbedDescriptionCap)
        {
            body = body[..EmbedDescriptionCap];
        }

        var category = string.IsNullOrWhiteSpace(note.Category) ? "Other" : note.Category.Trim();
        var who = string.IsNullOrWhiteSpace(note.Character) ? "Not logged in" : note.Character.Trim();
        var world = string.IsNullOrWhiteSpace(note.World) ? "—" : note.World.Trim();
        busy = true;
        status = "Sending…";
        var version = environment.Version;
        _ = Task.Run(() => Post(category, body, who, world, version));
    }

    public void OpenCommunity()
    {
        try
        {
            Process.Start(new ProcessStartInfo(CommunityUrl) { UseShellExecute = true });
        }
        catch (Exception failure)
        {
            log.Write(LogSeverity.Warning, failure, "Could not open Discord.");
            status = "Could not open Discord from here.";
        }
    }

    public void Dispose() => http.Dispose();

    private async Task Post(string category, string body, string who, string world, string version)
    {
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                username = "Linkpearl Feedback",
                embeds = new[]
                {
                    new
                    {
                        title = category,
                        description = body,
                        color = ColorFor(category),
                        footer = new { text = who + " · " + world + " · " + version },
                    },
                },
            });
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var reply = await http.PostAsync(ChannelHook, content).ConfigureAwait(false);
            var code = (int)reply.StatusCode;
            loop.Post(() => Finish(code));
        }
        catch (Exception failure)
        {
            log.Write(LogSeverity.Warning, failure, "Feedback webhook failed.");
            loop.Post(() => Finish(-1));
        }
    }

    private void Finish(int statusCode)
    {
        busy = false;
        if (statusCode is 200 or 204)
        {
            nextSend = clock.UtcNow.AddSeconds(CooldownSeconds);
            status = "Sent to the Discord feedback channel.";
            return;
        }

        if (statusCode == 429)
        {
            status = "Discord asked us to slow down. Try again in a minute.";
            return;
        }

        status = statusCode < 0
            ? "Could not reach Discord. Check the network and try again."
            : "Discord rejected the note (" + statusCode + ").";
    }

    private static int ColorFor(string category) => category switch
    {
        "Bug" => 0xE24B4A,
        "Feature" => 0x5B8DEF,
        "UI" => 0xC084FC,
        "Audio" => 0x34D399,
        "Social" => 0xF59E0B,
        _ => 0xE8C86A,
    };
}
