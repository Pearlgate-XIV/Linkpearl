using System.Text.Json;
using Linkpearl.Modules;

namespace Linkpearl.Emoji;

public sealed class EmojiRecent
{
    public const int Cap = 36;

    private readonly List<Use> uses = new();
    private readonly Dictionary<string, string> tones = new(StringComparer.Ordinal);
    private string path = string.Empty;

    public IReadOnlyList<Use> Uses => uses;

    public void Bind(HostPaths paths)
    {
        if (path.Length > 0)
        {
            return;
        }

        path = paths.State("emoji-recent.json");
        Load();
    }

    public IReadOnlyList<string> Ranked()
    {
        var now = DateTimeOffset.UtcNow;
        return uses
            .OrderByDescending(use => Score(use, now))
            .ThenByDescending(use => use.LastUnix)
            .Take(Cap)
            .Select(use => use.Glyph)
            .ToArray();
    }

    public string ToneOf(string glyph) =>
        tones.TryGetValue(EmojiBits.BaseOf(glyph), out var tone) ? tone : string.Empty;

    public void Remember(string glyph)
    {
        if (glyph.Length == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (var index = 0; index < uses.Count; index++)
        {
            if (!string.Equals(uses[index].Glyph, glyph, StringComparison.Ordinal))
            {
                continue;
            }

            uses[index] = uses[index] with { Count = uses[index].Count + 1, LastUnix = now };
            Save();
            return;
        }

        uses.Add(new Use(glyph, 1, now));
        if (uses.Count > Cap * 2)
        {
            uses.RemoveRange(0, uses.Count - Cap);
        }

        Save();
    }

    public void RememberTone(string glyph, string tone)
    {
        tones[EmojiBits.BaseOf(glyph)] = tone;
        Save();
    }

    private static long Score(Use use, DateTimeOffset now)
    {
        var age = now.ToUnixTimeSeconds() - use.LastUnix;
        var recency = age < 86_400 ? 12 : age < 604_800 ? 6 : age < 2_592_000 ? 2 : 0;
        return use.Count * 4 + recency;
    }

    private void Load()
    {
        if (path.Length == 0 || !File.Exists(path))
        {
            return;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<RecentSave>(File.ReadAllText(path));
            if (dto?.Uses is { Length: > 0 })
            {
                uses.Clear();
                uses.AddRange(dto.Uses);
            }

            if (dto?.Tones is { Length: > 0 })
            {
                tones.Clear();
                foreach (var pair in dto.Tones)
                {
                    if (pair.Glyph is { Length: > 0 })
                    {
                        tones[pair.Glyph] = pair.Tone ?? string.Empty;
                    }
                }
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
    }

    private void Save()
    {
        if (path.Length == 0)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, JsonSerializer.Serialize(new RecentSave
            {
                Uses = uses.ToArray(),
                Tones = tones.Select(pair => new ToneSave { Glyph = pair.Key, Tone = pair.Value }).ToArray(),
            }));
        }
        catch (IOException)
        {
        }
    }

    public readonly record struct Use(string Glyph, int Count, long LastUnix);

    private sealed class RecentSave
    {
        public Use[]? Uses { get; set; }

        public ToneSave[]? Tones { get; set; }
    }

    private sealed class ToneSave
    {
        public string? Glyph { get; set; }

        public string? Tone { get; set; }
    }
}
