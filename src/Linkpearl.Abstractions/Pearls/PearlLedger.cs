using System.Globalization;
using System.Text.Json;
using Linkpearl.Modules;
using Linkpearl.Time;

namespace Linkpearl.Pearls;

public enum PearlKind : byte
{
    Earned = 0,
    Spent = 1,
    Gift = 2,
}

public readonly struct PearlTx
{
    public required long AtUnix { get; init; }

    public required int Amount { get; init; }

    public required PearlKind Kind { get; init; }

    public required string Title { get; init; }

    public required string Detail { get; init; }
}

public sealed class PearlLedger
{
    public static readonly int[] CheckInRewards = [50, 50, 75, 50, 75, 50, 150];
    public const int WelcomeAmount = 200;
    public const int DevTestAmount = 100_000;
    public const int SpinCap = 100;
    public const int ShellCost = 25;
    public const int ShellWin = 60;
    public const int ShellsPerDay = 3;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly int[] SpinWeights = [18, 22, 28, 16, 12, 4];
    public static readonly float[] SpinMultipliers = [0f, 0.5f, 1f, 1.5f, 2f, 5f];

    private readonly string path;
    private readonly IClock clock;
    private readonly List<PearlTx> history = new();
    private readonly HashSet<string> claimed = new(StringComparer.Ordinal);
    private int balance;
    private int lifetimeEarned;
    private int checkIns;
    private int streak;
    private string lastCheckDay = string.Empty;
    private string playDay = string.Empty;
    private int spinWagered;
    private int shellsToday;
    private readonly Random rng = new();

    private PearlLedger(string path, IClock clock)
    {
        this.path = path;
        this.clock = clock;
    }

    public int Balance => balance;

    public int LifetimeEarned => lifetimeEarned;

    public int LifetimeSpent
    {
        get
        {
            var spent = 0;
            for (var index = 0; index < history.Count; index++)
            {
                if (history[index].Amount < 0)
                {
                    spent -= history[index].Amount;
                }
            }

            return spent;
        }
    }

    public int EarnedToday
    {
        get
        {
            var start = new DateTimeOffset(clock.Now.Date, clock.Now.Offset).ToUnixTimeSeconds();
            var earned = 0;
            for (var index = 0; index < history.Count; index++)
            {
                var row = history[index];
                if (row.AtUnix >= start && row.Amount > 0)
                {
                    earned += row.Amount;
                }
            }

            return earned;
        }
    }

    public int Streak => streak;

    public int CheckIns => checkIns;

    public IReadOnlyList<PearlTx> History => history;

    public bool Claimed(string id) => claimed.Contains(id);

    public static PearlLedger Load(HostPaths paths, IClock clock)
    {
        var path = paths.State("pearls.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? paths.StateDirectory);
        var book = new PearlLedger(path, clock);
        if (!File.Exists(path))
        {
            book.Credit(WelcomeAmount, "Welcome", "First lighting of the pearl purse.", PearlKind.Earned, "welcome");
            return book;
        }

        try
        {
            var save = JsonSerializer.Deserialize<Save>(File.ReadAllText(path));
            if (save is null)
            {
                book.Credit(WelcomeAmount, "Welcome", "First lighting of the pearl purse.", PearlKind.Earned,
                    "welcome");
                return book;
            }

            book.balance = Math.Max(0, save.Balance);
            book.lifetimeEarned = Math.Max(0, save.LifetimeEarned);
            book.checkIns = Math.Max(0, save.CheckIns);
            book.streak = Math.Max(0, save.Streak);
            book.lastCheckDay = save.LastCheckDay ?? string.Empty;
            book.playDay = save.PlayDay ?? string.Empty;
            book.spinWagered = Math.Max(0, save.SpinWagered);
            book.shellsToday = Math.Max(0, save.ShellsToday);
            if (save.Claimed is not null)
            {
                foreach (var id in save.Claimed)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        book.claimed.Add(id);
                    }
                }
            }

            if (save.History is not null)
            {
                foreach (var row in save.History)
                {
                    if (row is null || string.IsNullOrWhiteSpace(row.Title))
                    {
                        continue;
                    }

                    book.history.Add(new PearlTx
                    {
                        AtUnix = row.AtUnix,
                        Amount = row.Amount,
                        Kind = row.Kind,
                        Title = row.Title,
                        Detail = row.Detail ?? string.Empty,
                    });
                }
            }

            if (!book.claimed.Contains("welcome") && book.history.Count == 0)
            {
                book.Credit(WelcomeAmount, "Welcome", "First lighting of the pearl purse.", PearlKind.Earned,
                    "welcome");
            }
        }
        catch (JsonException)
        {
            book.Credit(WelcomeAmount, "Welcome", "First lighting of the pearl purse.", PearlKind.Earned, "welcome");
        }
        catch (IOException)
        {
        }

        book.RollPlayDay();
        return book;
    }

    public string TodayKey() => clock.Now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public bool CanCheckIn() => !string.Equals(lastCheckDay, TodayKey(), StringComparison.Ordinal);

    public int NextCheckReward()
    {
        var next = CanCheckIn() ? (streak % 7) : Math.Max(0, (streak - 1) % 7);
        return CheckInRewards[next];
    }

    public bool TryCheckIn(out int awarded)
    {
        awarded = 0;
        if (!CanCheckIn())
        {
            return false;
        }

        var today = TodayKey();
        if (lastCheckDay.Length > 0)
        {
            var previous = DateTime.ParseExact(lastCheckDay, "yyyy-MM-dd", CultureInfo.InvariantCulture).Date;
            streak = previous.AddDays(1) == clock.Now.Date ? streak + 1 : 1;
        }
        else
        {
            streak = 1;
        }

        lastCheckDay = today;
        checkIns++;
        awarded = CheckInRewards[(streak - 1) % 7];
        Credit(awarded, "Daily check-in", "Day " + streak.ToString(CultureInfo.InvariantCulture) + " of the streak.",
            PearlKind.Earned, "daily-" + today);
        if (streak == 1)
        {
            Credit(100, "First check-in", "The first morning on the glass.", PearlKind.Earned, "ach-first-checkin");
        }

        if (streak > 0 && streak % 7 == 0)
        {
            Credit(200, "Week streak", "Seven mornings, kept.", PearlKind.Earned, "ach-week-" + streak);
        }

        if (checkIns == 5)
        {
            Credit(100, "Regular", "Five days on the communicator.", PearlKind.Earned, "ach-regular");
        }

        return true;
    }

    public void Refund(int amount, string title, string detail) =>
        Credit(amount, title, detail, PearlKind.Earned, null);

    public bool TrySpend(int amount, string title, string detail)
    {
        if (amount <= 0 || balance < amount)
        {
            return false;
        }

        balance -= amount;
        history.Insert(0, new PearlTx
        {
            AtUnix = clock.UtcNow.ToUnixTimeSeconds(),
            Amount = -amount,
            Kind = PearlKind.Spent,
            Title = title,
            Detail = detail,
        });
        Persist();
        return true;
    }

    public bool GrantDevTestPurse() =>
        Credit(DevTestAmount, "Test purse", "Local development grant.", PearlKind.Gift, "dev-test-purse");

    public bool TryGiftClaim(int amount, string title, string detail, string flag)
    {
        if (claimed.Contains(flag))
        {
            return false;
        }

        return Credit(amount, title, detail, PearlKind.Gift, flag);
    }

    public bool TryClaim(int amount, string title, string detail, string flag, PearlKind kind = PearlKind.Earned)
    {
        if (claimed.Contains(flag))
        {
            return false;
        }

        return Credit(amount, title, detail, kind, flag);
    }

    public void NotePurchase()
    {
        TryClaim(75, "First cosmetic", "A mark bought with Pearls.", "ach-first-buy");
        if (lifetimeEarned >= 1000)
        {
            TryClaim(150, "Earner", "A thousand Pearls earned.", "ach-earner");
        }
    }

    public int SpinRemaining()
    {
        RollPlayDay();
        return Math.Max(0, SpinCap - spinWagered);
    }

    public int ShellsRemaining()
    {
        RollPlayDay();
        return Math.Max(0, ShellsPerDay - shellsToday);
    }

    public bool TrySpin(int wager, out int payout, out int slice)
    {
        payout = 0;
        slice = 0;
        RollPlayDay();
        if (wager is not (25 or 50 or 100) || wager > SpinRemaining() || !TrySpend(wager, "Pearl spin",
                "Wagered " + wager.ToString(CultureInfo.InvariantCulture) + " Pearls."))
        {
            return false;
        }

        spinWagered += wager;
        var pick = rng.Next(SpinWeights.Sum());
        var walk = 0;
        for (var index = 0; index < SpinWeights.Length; index++)
        {
            walk += SpinWeights[index];
            if (pick < walk)
            {
                slice = index;
                break;
            }
        }

        payout = (int)MathF.Round(wager * SpinMultipliers[slice]);
        if (payout > 0)
        {
            Credit(payout, "Spin return",
                SpinMultipliers[slice].ToString("0.#", CultureInfo.InvariantCulture) + "× on " +
                wager.ToString(CultureInfo.InvariantCulture), PearlKind.Earned, null);
        }
        else
        {
            Persist();
        }

        return true;
    }

    public bool TryShell(int pick, out bool won)
    {
        won = false;
        RollPlayDay();
        if (pick is < 0 or > 2 || ShellsRemaining() <= 0 ||
            !TrySpend(ShellCost, "Lucky shell", "Chose a shell."))
        {
            return false;
        }

        shellsToday++;
        var prize = rng.Next(3);
        won = pick == prize;
        if (won)
        {
            Credit(ShellWin, "Lucky shell", "The pearl was in that one.", PearlKind.Earned, null);
        }
        else
        {
            Persist();
        }

        return true;
    }

    public IEnumerable<PearlTx> Filtered(int filter)
    {
        for (var index = 0; index < history.Count; index++)
        {
            var row = history[index];
            if (filter == 1 && row.Kind != PearlKind.Earned)
            {
                continue;
            }

            if (filter == 2 && row.Kind != PearlKind.Spent)
            {
                continue;
            }

            if (filter == 3 && row.Kind != PearlKind.Gift)
            {
                continue;
            }

            yield return row;
        }
    }

    private bool Credit(int amount, string title, string detail, PearlKind kind, string? flag)
    {
        if (amount <= 0)
        {
            return false;
        }

        if (flag is { Length: > 0 } && !claimed.Add(flag))
        {
            return false;
        }

        balance += amount;
        lifetimeEarned += amount;
        history.Insert(0, new PearlTx
        {
            AtUnix = clock.UtcNow.ToUnixTimeSeconds(),
            Amount = amount,
            Kind = kind,
            Title = title,
            Detail = detail,
        });
        if (lifetimeEarned >= 1000)
        {
            claimed.Add("ready-earner");
        }

        Persist();
        return true;
    }

    private void RollPlayDay()
    {
        var today = TodayKey();
        if (string.Equals(playDay, today, StringComparison.Ordinal))
        {
            return;
        }

        playDay = today;
        spinWagered = 0;
        shellsToday = 0;
        Persist();
    }

    private void Persist()
    {
        var save = new Save
        {
            Balance = balance,
            LifetimeEarned = lifetimeEarned,
            CheckIns = checkIns,
            Streak = streak,
            LastCheckDay = lastCheckDay,
            PlayDay = playDay,
            SpinWagered = spinWagered,
            ShellsToday = shellsToday,
            Claimed = claimed.ToArray(),
            History = history.Take(80).Select(row => new TxSave
            {
                AtUnix = row.AtUnix,
                Amount = row.Amount,
                Kind = row.Kind,
                Title = row.Title,
                Detail = row.Detail,
            }).ToArray(),
        };

        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(save, Json));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class Save
    {
        public int Balance { get; set; }

        public int LifetimeEarned { get; set; }

        public int CheckIns { get; set; }

        public int Streak { get; set; }

        public string? LastCheckDay { get; set; }

        public string? PlayDay { get; set; }

        public int SpinWagered { get; set; }

        public int ShellsToday { get; set; }

        public string[]? Claimed { get; set; }

        public TxSave[]? History { get; set; }
    }

    private sealed class TxSave
    {
        public long AtUnix { get; set; }

        public int Amount { get; set; }

        public PearlKind Kind { get; set; }

        public string? Title { get; set; }

        public string? Detail { get; set; }
    }
}
