using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Vybe;

internal static class VybeLaneMap
{
    public static readonly string[] Lanes =
    {
        "Dominant", "Submissive", "Switch", "Sadist", "Masochist", "Brat", "Caregiver", "Rope", "Primal",
        "Exhibitionist", "Voyeur", "Owner", "Pet",
    };

    public static readonly string[] Scale = { "No", "Not really", "Mixed", "Mostly", "Yes" };

    public static readonly string[] Asks =
    {
        "I like setting the pace and deciding what happens next.",
        "I relax more when someone else is clearly in charge.",
        "I enjoy leading some nights and following on others.",
        "Giving intense sensation is something I look for.",
        "Receiving intense sensation is something I look for.",
        "Pushing back and being chased is part of the fun for me.",
        "I want to look after someone and keep them steady.",
        "Ties, wraps, and being held in rope appeal to me.",
        "Raw, instinct-first play is how I like a scene to feel.",
        "Being seen in a charged moment turns me on.",
        "Watching someone else in a charged moment turns me on.",
        "A lasting claim — collar, title, or being called mine — matters to me.",
        "I like giving clear rules and expecting them to be kept.",
        "A protocol or a simple set of rules helps me drop into a scene.",
        "I get restless if I am only ever on one side of the dynamic.",
        "I enjoy giving sting, ache, or impact that someone asked me for.",
        "I want sensation that hurts in a way I chose, then I want aftercare.",
        "I like being a handful on purpose and seeing who can handle it.",
        "I like catching that pushback and turning it back into order.",
        "Check-ins, aftercare, and keeping someone steady are the scene for me.",
        "I want to be the one tying, wrapping, and holding someone in rope.",
        "I want to be the one in the rope, held still and looked after.",
        "Hunting, pinning, or stalking a consented scene appeals to me.",
        "Being hunted, caught, or overpowered in a consented scene appeals to me.",
        "I like being put on display, even if only for one person.",
        "I like sitting back and taking someone in, without being the one shown.",
        "I want someone who is mine to keep.",
        "I want to belong to someone, and be kept.",
        "Praise, guidance, and being someone's safe place matter more to me than intensity.",
        "Service, kneeling, or making myself useful is how I show I am in.",
        "I like taking responsibility for another person's scene, limits, and come-down.",
        "Teasing, talking back, and earning a correction is how I like to play.",
    };

    public const int Count = 13;

    public static int AskCount => Asks.Length;

    public static bool Ready(int[]? marks) => marks is { Length: > 0 } && Peak(marks) > 0;

    public static int[] Blank() => new int[Count];

    public static int[] Fit(int[]? marks)
    {
        var next = new int[Count];
        if (marks is not { Length: > 0 })
        {
            return next;
        }

        var n = Math.Min(Count, marks.Length);
        for (var index = 0; index < n; index++)
        {
            next[index] = Math.Clamp(marks[index], 0, 100);
        }

        return next;
    }

    public static int[] Score(int[] picks)
    {
        var raw = new float[Count];
        var cap = new float[Count];
        var n = Math.Min(Asks.Length, picks.Length);
        for (var ask = 0; ask < n; ask++)
        {
            var lean = Math.Clamp(picks[ask], 0, 4) / 4f;
            var map = Weight(ask);
            for (var hit = 0; hit < map.Length; hit++)
            {
                var lane = map[hit].Lane;
                var weight = map[hit].Weight;
                raw[lane] += lean * weight;
                cap[lane] += weight;
            }
        }

        var marks = new int[Count];
        for (var lane = 0; lane < Count; lane++)
        {
            marks[lane] = cap[lane] <= 0.01f ? 0 : (int)MathF.Round(100f * raw[lane] / cap[lane]);
        }

        return marks;
    }

    public static int[] Seed(string key) => key switch
    {
        "luna" or "ember" => Pack(28, 82, 44, 18, 61, 74, 22, 36, 40, 70, 24, 16, 68),
        "ace" => Pack(36, 34, 78, 22, 20, 18, 30, 14, 26, 32, 66, 12, 10),
        "kairo" => Pack(72, 18, 40, 48, 16, 12, 24, 20, 58, 22, 30, 54, 8),
        "nyx" => Pack(40, 46, 80, 28, 34, 42, 36, 48, 44, 38, 40, 22, 26),
        "raven" => Pack(86, 12, 28, 74, 14, 20, 18, 32, 50, 26, 44, 70, 6),
        "vex" => Pack(34, 52, 76, 24, 38, 60, 28, 42, 36, 54, 48, 16, 40),
        "echo" => Pack(22, 48, 36, 10, 16, 14, 64, 18, 12, 20, 22, 14, 30),
        "novale" or "velvet" => Pack(58, 30, 46, 20, 24, 16, 72, 28, 18, 34, 26, 62, 24),
        "sol" => Pack(64, 20, 38, 36, 12, 10, 26, 16, 44, 18, 28, 48, 8),
        "wren" => Pack(18, 40, 32, 8, 12, 10, 54, 22, 14, 16, 20, 12, 28),
        "iris" => Pack(24, 56, 42, 12, 28, 22, 48, 34, 18, 26, 24, 14, 36),
        "jett" => Pack(70, 16, 34, 52, 14, 18, 20, 24, 46, 30, 36, 58, 10),
        "hex" => Pack(48, 38, 72, 40, 32, 36, 26, 54, 62, 44, 50, 30, 28),
        "noir" => Pack(32, 64, 58, 22, 48, 44, 30, 66, 38, 52, 46, 18, 56),
        _ => FromKey(key),
    };

    public static (string Name, int Percent)[] Ranked(int[]? marks, int floor = 8)
    {
        var fit = Fit(marks);
        var rows = new List<(string Name, int Percent)>(Count);
        for (var index = 0; index < Count; index++)
        {
            if (fit[index] >= floor)
            {
                rows.Add((Lanes[index], fit[index]));
            }
        }

        rows.Sort((left, right) => right.Percent.CompareTo(left.Percent));
        return rows.ToArray();
    }

    public static int Peak(int[]? marks)
    {
        var fit = Fit(marks);
        var peak = 0;
        for (var index = 0; index < fit.Length; index++)
        {
            if (fit[index] > peak)
            {
                peak = fit[index];
            }
        }

        return peak;
    }

    public static int ActiveWants(int[]? want)
    {
        var fit = Fit(want);
        var count = 0;
        for (var index = 0; index < fit.Length; index++)
        {
            if (fit[index] >= 10)
            {
                count++;
            }
        }

        return count;
    }

    public static int Match(int[]? theirs, int[]? want, int[]? mine)
    {
        var card = Fit(theirs);
        var need = Fit(want);
        var own = Fit(mine);
        var parts = 0;
        var sum = 0;
        for (var index = 0; index < Count; index++)
        {
            if (need[index] < 10)
            {
                continue;
            }

            parts++;
            sum += 100 - Math.Abs(card[index] - need[index]);
        }

        if (parts > 0)
        {
            return Math.Clamp(sum / parts, 0, 100);
        }

        if (Peak(own) == 0 || Peak(card) == 0)
        {
            return 0;
        }

        var fit = 0;
        var weight = 0;
        for (var index = 0; index < Count; index++)
        {
            var pair = PairOf(index);
            var same = Math.Min(own[index], card[index]);
            var flip = Math.Min(own[index], card[pair]);
            var take = Math.Max(same, flip);
            if (take < 12)
            {
                continue;
            }

            fit += take;
            weight++;
        }

        return weight == 0 ? 0 : Math.Clamp(fit / weight, 0, 100);
    }

    public static void DrawBars(in AppletFrame frame, ref LayoutFlow stack, int[]? marks, bool night)
    {
        var rows = Ranked(marks);
        if (rows.Length == 0)
        {
            return;
        }

        var tone = VybeChrome.Tone(night);
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "ROLE MAP", night);
        for (var index = 0; index < rows.Length; index++)
        {
            DrawBar(frame, stack.Take(frame.Units(28f)), rows[index].Name, rows[index].Percent, tone);
        }
    }

    public static bool DrawFold(in AppletFrame frame, ref LayoutFlow stack, int[]? marks, bool night, bool open)
    {
        var rows = Ranked(marks);
        if (rows.Length == 0)
        {
            return false;
        }

        var tone = VybeChrome.Tone(night);
        var head = stack.Take(frame.Units(32f));
        var radius = frame.Units(10f);
        frame.Paint.Fill(head, open ? tone.Accent with { W = 0.18f } : tone.CardHi, radius);
        frame.Paint.Stroke(head, tone.Accent with { W = open ? 0.70f : 0.35f }, frame.Units(1.1f), radius);
        var pad = head.Inset(new Edges(frame.Units(10f), 0f, frame.Units(10f), 0f));
        frame.Text.DrawEllipsized(pad.LeftSlice(pad.Width * 0.42f), "ROLE MAP",
            new TextStyle(FontRole.CaptionStrong, tone.Accent));
        var hint = rows[0].Name + "  " +
                   rows[0].Percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
        frame.Text.DrawEllipsized(
            pad.Inset(new Edges(pad.Width * 0.42f, 0f, frame.Units(16f), 0f)), hint,
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Right));
        frame.Text.DrawIn(pad.RightSlice(frame.Units(14f)), open ? "▾" : "▸",
            new TextStyle(FontRole.CaptionStrong, tone.Accent, TextAlign.Right));
        if (open)
        {
            for (var index = 0; index < rows.Length; index++)
            {
                DrawBar(frame, stack.Take(frame.Units(28f)), rows[index].Name, rows[index].Percent, tone);
            }
        }

        return frame.Input.ConsumeClick(head);
    }

    public static void DrawBar(in AppletFrame frame, Rect area, string name, int percent, NightPalette tone)
    {
        percent = Math.Clamp(percent, 0, 100);
        var label = area.TopSlice(frame.Units(13f));
        frame.Text.DrawEllipsized(label.LeftSlice(area.Width * 0.72f), name,
            new TextStyle(FontRole.Caption, tone.Ink));
        frame.Text.DrawIn(label.RightSlice(area.Width * 0.28f),
            percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%",
            new TextStyle(FontRole.CaptionStrong, tone.Accent, TextAlign.Right));
        var track = area.BottomSlice(frame.Units(8f)).Inset(new Edges(0f, frame.Units(1f), 0f, frame.Units(1f)));
        frame.Paint.Fill(track, tone.Faint with { W = 0.55f }, track.Height * 0.5f);
        var fill = track.LeftSlice(MathF.Max(track.Height, track.Width * (percent / 100f)));
        frame.Paint.Fill(fill, tone.Accent with { W = 0.92f }, track.Height * 0.5f);
    }

    public static bool DrawWant(in AppletFrame frame, Rect area, string name, int value, bool night,
        string drag, Action<int> set, out string nextDrag)
    {
        var tone = VybeChrome.Tone(night);
        DrawBar(frame, area.TopSlice(frame.Units(28f)), name, value, tone);
        var track = area.BottomSlice(frame.Units(14f)).Inset(new Edges(0f, frame.Units(4f), 0f, frame.Units(2f)));
        frame.Paint.Fill(track, tone.Faint with { W = 0.55f }, track.Height * 0.5f);
        var fill = track.LeftSlice(MathF.Max(track.Height, track.Width * (value / 100f)));
        frame.Paint.Fill(fill, tone.Accent with { W = 0.88f }, track.Height * 0.5f);
        var thumb = new System.Numerics.Vector2(track.Min.X + track.Width * (value / 100f), track.Center.Y);
        frame.Paint.FillCircle(thumb, track.Height * 0.72f, tone.Accent);
        nextDrag = drag;
        if (frame.Input.WasPressed(area))
        {
            nextDrag = name;
        }

        if (nextDrag == name && frame.Input.IsHeld())
        {
            set((int)Math.Clamp(MathF.Round(100f * (frame.Input.Cursor.X - track.Min.X) / MathF.Max(track.Width, 1f)),
                0f, 100f));
            return true;
        }

        if (!frame.Input.IsHeld())
        {
            nextDrag = string.Empty;
        }

        return false;
    }

    private static int PairOf(int lane) => lane switch
    {
        0 => 1,
        1 => 0,
        3 => 4,
        4 => 3,
        5 => 0,
        6 => 12,
        9 => 10,
        10 => 9,
        11 => 12,
        12 => 11,
        _ => lane,
    };

    private static (int Lane, float Weight)[] Weight(int ask) => ask switch
    {
        0 => [(0, 3f), (11, 1.4f)],
        1 => [(1, 3f), (12, 1.4f)],
        2 => [(2, 3f), (0, 0.6f), (1, 0.6f)],
        3 => [(3, 3f)],
        4 => [(4, 3f)],
        5 => [(5, 3f), (1, 0.8f)],
        6 => [(6, 3f), (11, 0.8f)],
        7 => [(7, 3f)],
        8 => [(8, 3f)],
        9 => [(9, 3f)],
        10 => [(10, 3f)],
        11 => [(11, 2.2f), (12, 2.2f)],
        12 => [(0, 3f), (11, 1.6f)],
        13 => [(1, 3f), (12, 1.4f)],
        14 => [(2, 3.2f)],
        15 => [(3, 3.2f)],
        16 => [(4, 3.2f)],
        17 => [(5, 3.2f), (1, 0.6f)],
        18 => [(0, 2.8f), (5, 0.5f)],
        19 => [(6, 3.2f)],
        20 => [(7, 3f), (0, 0.8f)],
        21 => [(7, 3f), (1, 0.8f)],
        22 => [(8, 3f), (0, 1.2f)],
        23 => [(8, 3f), (1, 1.2f)],
        24 => [(9, 3.2f)],
        25 => [(10, 3.2f)],
        26 => [(11, 3.4f)],
        27 => [(12, 3.4f)],
        28 => [(6, 3.2f)],
        29 => [(1, 2.2f), (12, 2f)],
        30 => [(0, 1.6f), (6, 2f), (11, 1.4f)],
        31 => [(5, 2.8f), (1, 1.2f)],
        _ => [],
    };

    private static int[] Pack(params int[] marks) => Fit(marks);

    private static int[] FromKey(string key)
    {
        var marks = new int[Count];
        var seed = string.IsNullOrEmpty(key) ? 17 : key.GetHashCode(StringComparison.Ordinal);
        var spin = new Random(seed);
        for (var index = 0; index < Count; index++)
        {
            marks[index] = spin.Next(8, 78);
        }

        var peak = spin.Next(Count);
        marks[peak] = Math.Max(marks[peak], 72);
        marks[PairOf(peak)] = Math.Max(marks[PairOf(peak)], 48);
        return marks;
    }
}
