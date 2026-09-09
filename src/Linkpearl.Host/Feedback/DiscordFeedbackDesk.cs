using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
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
    private const long FileCapBytes = 8L * 1024 * 1024;
    private const int FileCapCount = 8;

    private readonly HostEnvironment environment;
    private readonly IClock clock;
    private readonly IFrameLoop loop;
    private readonly ILinkpearlLog log;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(60) };
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

    public IReadOnlyList<CrashPick> RecentCrashes() => CrashReportHunt.Recent();

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
        var files = Collect(note.Attachments);
        busy = true;
        status = "Sending…";
        var version = environment.Version;
        _ = Task.Run(() => Post(category, body, who, world, version, files));
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

    private async Task Post(string category, string body, string who, string world, string version,
        IReadOnlyList<string> files)
    {
        var skipped = new List<string>();
        var streams = new List<FileStream>();
        try
        {
            var extra = string.Empty;
            using var form = new MultipartFormDataContent();
            var kept = new List<string>();
            for (var index = 0; index < files.Count && kept.Count < FileCapCount; index++)
            {
                var path = files[index];
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    continue;
                }

                if (info.Length > FileCapBytes)
                {
                    skipped.Add(Path.GetFileName(path) + " too large");
                    continue;
                }

                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                streams.Add(stream);
                var part = new StreamContent(stream);
                part.Headers.ContentType = new MediaTypeHeaderValue(Mime(path));
                form.Add(part, "files[" + kept.Count + "]", Path.GetFileName(path));
                kept.Add(Path.GetFileName(path));
            }

            if (skipped.Count > 0)
            {
                extra = "\n\nSkipped: " + string.Join(", ", skipped);
            }

            if (kept.Count > 0)
            {
                extra += "\nAttached: " + string.Join(", ", kept);
            }

            var json = JsonSerializer.Serialize(new
            {
                username = "Linkpearl Feedback",
                embeds = new[]
                {
                    new
                    {
                        title = category,
                        description = TrimBody(body + extra),
                        color = ColorFor(category),
                        footer = new { text = who + " · " + world + " · " + version },
                    },
                },
            });
            form.Add(new StringContent(json, Encoding.UTF8, "application/json"), "payload_json");
            using var reply = await http.PostAsync(ChannelHook, form).ConfigureAwait(false);
            var code = (int)reply.StatusCode;
            loop.Post(() => Finish(code));
        }
        catch (Exception failure)
        {
            log.Write(LogSeverity.Warning, failure, "Feedback webhook failed.");
            loop.Post(() => Finish(-1));
        }
        finally
        {
            for (var index = 0; index < streams.Count; index++)
            {
                streams[index].Dispose();
            }
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

    private static string TrimBody(string body) =>
        body.Length <= EmbedDescriptionCap ? body : body[..EmbedDescriptionCap];

    private static List<string> Collect(IReadOnlyList<string>? attachments)
    {
        var files = new List<string>();
        if (attachments is null)
        {
            return files;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < attachments.Count; index++)
        {
            var path = attachments[index];
            if (path.Length == 0 || !File.Exists(path) || !seen.Add(path))
            {
                continue;
            }

            files.Add(path);
        }

        return files;
    }

    private static string Mime(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".json" => "application/json",
            ".dmp" or ".tspack" => "application/octet-stream",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "text/plain",
        };
    }

    private static int ColorFor(string category) => category switch
    {
        "Bug" => 0xE24B4A,
        "Crash" => 0xB91C1C,
        "Feature" => 0x5B8DEF,
        "UI" => 0xC084FC,
        "Audio" => 0x34D399,
        "Social" => 0xF59E0B,
        _ => 0xE8C86A,
    };
}
