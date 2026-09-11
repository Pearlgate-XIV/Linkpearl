using System.Text.Json;
using Linkpearl.Applets;
using Linkpearl.Emoji;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Modules;
using Linkpearl.Painting;

namespace Linkpearl.Chat;

public sealed class ChatMarks
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly HostPaths paths;
    private readonly EmojiPick faces = new();
    private readonly Dictionary<string, List<string>> board = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ChatCite> cites = new(StringComparer.Ordinal);
    private bool loaded;
    private string pendingThread = string.Empty;
    private string pendingWho = string.Empty;
    private string pendingPreview = string.Empty;
    private bool menuOn;
    private bool menuSkip;
    private bool faceOn;
    private string menuKey = string.Empty;
    private string menuThread = string.Empty;
    private string menuWho = string.Empty;
    private string menuPreview = string.Empty;
    private Vector2 menuAt;

    public ChatMarks(HostPaths paths) => this.paths = paths;

    public string ReplyThread { get; private set; } = string.Empty;

    public string ReplyWho { get; private set; } = string.Empty;

    public string ReplyPreview { get; private set; } = string.Empty;

    public bool HasReply => ReplyThread.Length > 0;

    public bool Busy => menuOn || faceOn;

    public bool Dismiss()
    {
        if (faceOn)
        {
            faceOn = false;
            return true;
        }

        if (menuOn)
        {
            menuOn = false;
            return true;
        }

        if (HasReply)
        {
            ClearReply();
            return true;
        }

        return false;
    }

    public static string Key(string thread, string who, string when, string? body)
    {
        body ??= string.Empty;
        if (body.Length <= 96)
        {
            return thread + "\n" + who + "\n" + when + "\n" + body;
        }

        var take = 96;
        if (take < body.Length && char.IsLowSurrogate(body[take]))
        {
            take--;
        }

        if (take > 0 && char.IsHighSurrogate(body[take - 1]))
        {
            take--;
        }

        return thread + "\n" + who + "\n" + when + "\n" + body.AsSpan(0, Math.Max(0, take)).ToString();
    }

    public void BeginReact(string key)
    {
        menuKey = key;
        menuOn = false;
        faceOn = true;
        faces.Open();
    }

    public void BeginReply(string thread, string who, string preview)
    {
        menuOn = false;
        faceOn = false;
        ReplyThread = thread;
        ReplyWho = ChatBits.ReplyName(who);
        ReplyPreview = ChatBits.Snippet(preview);
    }

    public void Offer(string thread, string key, string who, string preview, Vector2 at)
    {
        menuOn = true;
        menuSkip = true;
        faceOn = false;
        menuThread = thread;
        menuKey = key;
        menuWho = ChatBits.ReplyName(who);
        menuPreview = preview;
        menuAt = at;
    }

    public IReadOnlyList<string> Faces(string key)
    {
        Load();
        return board.TryGetValue(key, out var list) ? list : [];
    }

    public float Band(in AppletFrame frame, string key) =>
        Faces(key).Count == 0 ? 0f : frame.Units(20f);

    public void DrawBand(in AppletFrame frame, Rect area, string key)
    {
        var marks = Faces(key);
        if (marks.Count == 0 || area.IsEmpty)
        {
            return;
        }

        var cell = frame.Units(18f);
        var gap = frame.Units(4f);
        var x = area.Min.X;
        for (var index = 0; index < marks.Count && x + cell <= area.Max.X; index++)
        {
            var box = Rect.FromSize(new Vector2(x, area.Min.Y), new Vector2(cell, area.Height));
            EmojiArt.DrawOrMark(frame, box, marks[index], Vector4.One);
            x += cell + gap;
        }
    }

    public float ComposerHeight(in AppletFrame frame, string thread) =>
        HasReply && string.Equals(ReplyThread, thread, StringComparison.Ordinal) ? frame.Units(48f) : 0f;

    public ChatCite Cite(string key, string thread, string body, bool mine = false)
    {
        Load();
        if (key.Length > 0 && cites.TryGetValue(key, out var byKey))
        {
            return byKey;
        }

        if (!mine || thread.Length == 0)
        {
            return default;
        }

        if (cites.TryGetValue(BodyKey(thread, body), out var byBody) ||
            cites.TryGetValue(thread + "\nbody\n" + (body ?? string.Empty), out byBody))
        {
            return byBody;
        }

        return default;
    }

    public bool DrawReply(in AppletFrame frame, Rect area, string thread, Vector4 ink, Vector4 mute, Vector4 card)
    {
        if (!HasReply || !string.Equals(ReplyThread, thread, StringComparison.Ordinal))
        {
            return false;
        }

        frame.Paint.Fill(area, card, frame.Units(10f));
        var drop = area.RightSlice(frame.Units(28f));
        ChatBits.DrawQuote(frame, area.Inset(new Edges(0f, 0f, frame.Units(30f), 0f)),
            "Replying to " + ReplyWho, ReplyPreview, ink, mute);
        frame.Text.DrawIn(drop, "×", new TextStyle(FontRole.BodyStrong, mute, TextAlign.Center));
        if (frame.Input.ConsumeClick(drop) || frame.Input.ConsumeClick(area, PointerButton.Secondary))
        {
            ClearReply();
        }

        return true;
    }

    public string Seal(string thread, string body)
    {
        if (body.Length == 0 || !HasReply || !string.Equals(ReplyThread, thread, StringComparison.Ordinal))
        {
            return body;
        }

        pendingThread = thread;
        pendingWho = ReplyWho;
        pendingPreview = ReplyPreview;
        ClearReply();
        return body;
    }

    public void CatchSent(string thread, string stampWho, string when, string body)
    {
        if (pendingThread.Length == 0 || !string.Equals(pendingThread, thread, StringComparison.Ordinal))
        {
            return;
        }

        Load();
        var cite = new ChatCite(pendingWho, pendingPreview);
        var mine = string.Equals(stampWho, "me", StringComparison.OrdinalIgnoreCase);
        if (mine && when.Length > 0)
        {
            cites[Key(thread, "me", when, body)] = cite;
        }

        if (mine)
        {
            cites[BodyKey(thread, body)] = cite;
        }
        pendingThread = string.Empty;
        pendingWho = string.Empty;
        pendingPreview = string.Empty;
        SaveCites();
    }

    public void ClearReply()
    {
        ReplyThread = string.Empty;
        ReplyWho = string.Empty;
        ReplyPreview = string.Empty;
    }

    public void DrawMenu(in AppletFrame frame, Rect bounds, Vector4 ink, Vector4 mute, Vector4 accent, Vector4 card)
    {
        if (faceOn)
        {
            DrawFaces(frame, bounds, ink, mute, accent, card);
            return;
        }

        if (!menuOn)
        {
            return;
        }

        var labels = new[] { "React", "Reply" };
        var width = frame.Units(148f);
        var rowH = frame.Units(34f);
        var height = rowH * labels.Length + frame.Units(8f);
        var left = Math.Clamp(menuAt.X, bounds.Min.X, bounds.Max.X - width);
        var top = Math.Clamp(menuAt.Y, bounds.Min.Y, bounds.Max.Y - height);
        var box = Rect.FromSize(new Vector2(left, top), new Vector2(width, height));
        frame.Paint.Fill(box, card with { W = 0.98f }, frame.Units(10f));
        frame.Paint.Stroke(box, accent with { W = 0.45f }, frame.Theme.Metrics.Hairline, frame.Units(10f));
        if (menuSkip)
        {
            menuSkip = false;
            frame.Input.Claim(box);
            return;
        }

        for (var index = 0; index < labels.Length; index++)
        {
            var row = Rect.FromSize(box.Min + new Vector2(0f, frame.Units(4f) + index * rowH),
                new Vector2(width, rowH)).Inset(new Edges(frame.Units(4f), 0f));
            var hover = frame.Input.IsHovering(row);
            if (hover)
            {
                frame.Paint.Fill(row, accent with { W = 0.18f }, frame.Units(8f));
            }

            frame.Text.DrawIn(row.Inset(new Edges(frame.Units(10f), 0f)), labels[index],
                new TextStyle(FontRole.CaptionStrong, hover ? accent : ink));
            if (!frame.Input.ConsumeClick(row))
            {
                continue;
            }

            if (index == 0)
            {
                faceOn = true;
                faces.Open();
                menuOn = false;
                return;
            }

            ReplyThread = menuThread;
            ReplyWho = menuWho;
            ReplyPreview = ChatBits.Snippet(menuPreview);
            menuOn = false;
            return;
        }

        frame.Input.Claim(box);
        if (frame.Input.ConsumeClick(bounds) || frame.Input.ConsumeClick(bounds, PointerButton.Secondary))
        {
            menuOn = false;
        }
    }

    private void DrawFaces(in AppletFrame frame, Rect bounds, Vector4 ink, Vector4 mute, Vector4 accent, Vector4 card)
    {
        var sheet = bounds.BottomSlice(frame.Units(248f)).Inset(new Edges(frame.Units(8f), 0f, frame.Units(8f),
            frame.Units(8f)));
        faces.Draw(frame, sheet, ink, mute, accent, card, glyph =>
        {
            Remember(menuKey, glyph);
            faceOn = false;
        });
        if (frame.Input.ConsumeClick(bounds) && !sheet.Contains(frame.Input.Pointer))
        {
            faceOn = false;
        }
    }

    private void Remember(string key, string glyph)
    {
        if (key.Length == 0 || glyph.Length == 0)
        {
            return;
        }

        Load();
        if (!board.TryGetValue(key, out var list))
        {
            list = new List<string>();
            board[key] = list;
        }

        for (var index = 0; index < list.Count; index++)
        {
            if (string.Equals(list[index], glyph, StringComparison.Ordinal))
            {
                return;
            }
        }

        if (list.Count >= 8)
        {
            list.RemoveAt(0);
        }

        list.Add(glyph);
        Save();
    }

    private void Load()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        LoadCites();
        var path = paths.State("chat-marks.json");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var save = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(path), Json);
            if (save is null)
            {
                return;
            }

            foreach (var pair in save)
            {
                if (pair.Key.Length == 0 || pair.Value is not { Length: > 0 })
                {
                    continue;
                }

                board[pair.Key] = new List<string>(pair.Value);
            }
        }
        catch
        {
            // Keep an empty board if the sidecar is damaged.
        }
    }

    private void Save()
    {
        var path = paths.State("chat-marks.json");
        var save = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var pair in board)
        {
            if (pair.Value.Count > 0)
            {
                save[pair.Key] = pair.Value.ToArray();
            }
        }

        File.WriteAllText(path, JsonSerializer.Serialize(save, Json));
    }

    private static string BodyKey(string thread, string body) => thread + "\nme\nbody\n" + (body ?? string.Empty);

    private void LoadCites()
    {
        var path = paths.State("chat-cites.json");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var save = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(path), Json);
            if (save is null)
            {
                return;
            }

            foreach (var pair in save)
            {
                if (pair.Key.Length == 0 || pair.Value is not { Length: >= 2 })
                {
                    continue;
                }

                cites[pair.Key] = new ChatCite(pair.Value[0], pair.Value[1]);
            }
        }
        catch
        {
            // Keep an empty cite board if the sidecar is damaged.
        }
    }

    private void SaveCites()
    {
        try
        {
            var path = paths.State("chat-cites.json");
            var save = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var pair in cites)
            {
                save[pair.Key] = [pair.Value.Who, pair.Value.Preview];
            }

            File.WriteAllText(path, JsonSerializer.Serialize(save, Json));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
