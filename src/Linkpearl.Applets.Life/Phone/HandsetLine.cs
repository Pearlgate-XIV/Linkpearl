using System.Text.Json;
using Linkpearl.Audio;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Phone;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Phone;

public sealed class HandsetLine : IHandsetLine
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IPearlHub pearl;
    private readonly IGameSession game;
    private readonly IClock clock;
    private readonly IBroadcastSense sense;
    private readonly DisplayPreferences display;
    private readonly string path;
    private readonly object gate = new();
    private readonly List<LineContact> book = [];
    private readonly List<SavedThread> threads = [];
    private readonly List<LineRecent> recents = [];
    private string ownNumber = string.Empty;
    private string dial = string.Empty;
    private LineState state;
    private string peerNumber = string.Empty;
    private string peerName = string.Empty;
    private float elapsed;
    private float ringFor;
    private bool speakerMuted;
    private bool micMuted;

    public HandsetLine(IPearlHub pearl, IGameSession game, IClock clock, IBroadcastSense sense,
        DisplayPreferences display, HostPaths paths)
    {
        this.pearl = pearl;
        this.game = game;
        this.clock = clock;
        this.sense = sense;
        this.display = display;
        path = paths.State("handset-line.json");
        Load();
    }

    public string Dial
    {
        get => dial;
        set => dial = Sanitize(value, 22);
    }

    public LineState State => state;

    public string PeerNumber => peerNumber;

    public string PeerName => peerName;

    public float Elapsed => elapsed;

    public bool SpeakerMuted => speakerMuted;

    public bool MicMuted => micMuted;

    public string OwnNumber
    {
        get
        {
            EnsureOwnNumber();
            return ownNumber;
        }
    }

    public IReadOnlyList<LineContact> Contacts
    {
        get
        {
            var list = new List<LineContact>(book.Count);
            for (var index = 0; index < book.Count; index++)
            {
                list.Add(Clean(book[index]));
            }

            var people = pearl.Current.People;
            for (var index = 0; index < people.Length; index++)
            {
                var person = people[index];
                var number = LineOf(person.PhoneNumber);
                if (number.Length == 0)
                {
                    continue;
                }

                var name = TextOf(person.DisplayName.Length > 0 ? person.DisplayName : person.Handle);
                var race = TextOf(person.Race);
                var world = TextOf(person.World);
                var slot = IndexOf(list, number);
                if (slot >= 0)
                {
                    var row = list[slot];
                    list[slot] = new LineContact(
                        row.Name.Length > 0 ? row.Name : name,
                        number,
                        true,
                        row.Race.Length > 0 ? row.Race : race,
                        row.World.Length > 0 ? row.World : world);
                    continue;
                }

                list.Add(new LineContact(name, number, true, race, world));
            }

            list.Sort(static (left, right) =>
                string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }
    }

    public IReadOnlyList<LineThread> Threads
    {
        get
        {
            var list = new List<LineThread>(threads.Count);
            for (var index = 0; index < threads.Count; index++)
            {
                var row = threads[index];
                list.Add(new LineThread(row.Number, TitleOf(row.Number), row.Preview, row.LastUnix, row.Unread));
            }

            list.Sort(static (left, right) => right.LastUnix.CompareTo(left.LastUnix));
            return list;
        }
    }

    public IReadOnlyList<LineRecent> Recents => recents;

    public int UnreadTotal
    {
        get
        {
            var total = 0;
            for (var index = 0; index < threads.Count; index++)
            {
                total += threads[index].Unread;
            }

            return total;
        }
    }

    public IReadOnlyList<LineNote> Notes(string number)
    {
        var key = LineOf(number);
        for (var index = 0; index < threads.Count; index++)
        {
            if (threads[index].Number == key)
            {
                return threads[index].Notes;
            }
        }

        return [];
    }

    public void AppendDigit(char digit)
    {
        if (dial.Length >= 22 || !Allowed(digit, dial.Length == 0))
        {
            return;
        }

        dial += digit;
    }

    public void Backspace()
    {
        if (dial.Length > 0)
        {
            dial = dial[..^1];
        }
    }

    public void ClearDial() => dial = string.Empty;

    public void Paste(string raw)
    {
        var next = Sanitize(raw, 22);
        if (next.Length > 0)
        {
            dial = next;
        }
    }

    public bool PlaceCall(string? number = null)
    {
        var target = LineOf(number ?? dial);
        if (target.Length == 0 || state != LineState.Idle)
        {
            return false;
        }

        lock (gate)
        {
            dial = target;
            peerNumber = target;
            peerName = TitleOf(target);
            state = LineState.Dialing;
            elapsed = 0f;
            ringFor = 1.15f;
            speakerMuted = false;
            micMuted = false;
            recents.Insert(0, new LineRecent(target, peerName, clock.Now.ToUnixTimeSeconds(), true));
            if (recents.Count > 40)
            {
                recents.RemoveRange(40, recents.Count - 40);
            }

            Save();
        }

        ApplyAudio();
        sense.Beep();
        return true;
    }

    public void HangUp()
    {
        lock (gate)
        {
            state = LineState.Idle;
            peerNumber = string.Empty;
            peerName = string.Empty;
            elapsed = 0f;
            ringFor = 0f;
        }

        sense.Halt();
        sense.StopMonitor();
    }

    public void ToggleSpeakerMute()
    {
        speakerMuted = !speakerMuted;
        ApplyAudio();
    }

    public void ToggleMicMute()
    {
        micMuted = !micMuted;
        ApplyAudio();
    }

    public void Tick(float deltaSeconds)
    {
        EnsureOwnNumber();
        if (state == LineState.Idle)
        {
            return;
        }

        elapsed += Math.Max(0f, deltaSeconds);
        if (state == LineState.Dialing)
        {
            ringFor -= deltaSeconds;
            if (ringFor <= 0f)
            {
                state = LineState.Live;
                ApplyAudio();
            }
        }
    }

    public void SaveContact(string name, string number, string race = "", string world = "")
    {
        var digits = LineOf(number);
        var label = name.Trim();
        var tribe = race.Trim();
        var home = world.Trim();
        if (digits.Length == 0)
        {
            return;
        }

        if (label.Length == 0)
        {
            label = digits;
        }

        for (var index = 0; index < book.Count; index++)
        {
            if (LineNumbers.Same(book[index].Number, digits))
            {
                book[index] = new LineContact(label, digits, false, tribe, home);
                Save();
                return;
            }
        }

        book.Add(new LineContact(label, digits, false, tribe, home));
        Save();
    }

    public LineContact SuggestContact(string number)
    {
        var digits = LineOf(number);
        if (digits.Length == 0)
        {
            return default;
        }

        var snap = pearl.Current;
        if (snap.SignedIn && LineNumbers.Same(OwnNumber, digits))
        {
            var self = snap.MeName.Length > 0 ? snap.MeName : game.Character.Name;
            var home = snap.MeWorld.Length > 0 ? snap.MeWorld : game.Character.WorldName;
            return new LineContact(self, OwnNumber, true, game.RaceName, home);
        }

        var people = snap.People;
        for (var index = 0; index < people.Length; index++)
        {
            var person = people[index];
            if (!LineNumbers.Same(person.PhoneNumber, digits))
            {
                continue;
            }

            var name = person.DisplayName.Length > 0 ? person.DisplayName : person.Handle;
            return new LineContact(name, LineOf(person.PhoneNumber), true, person.Race, person.World);
        }

        for (var index = 0; index < book.Count; index++)
        {
            if (LineNumbers.Same(book[index].Number, digits))
            {
                return book[index];
            }
        }

        return new LineContact(string.Empty, digits, false);
    }

    public void DropContact(string number)
    {
        var digits = LineOf(number);
        book.RemoveAll(row => LineNumbers.Same(row.Number, digits));
        Save();
    }

    public void SendNote(string number, string body)
    {
        var digits = LineOf(number);
        var text = body.Trim();
        if (digits.Length == 0 || text.Length == 0)
        {
            return;
        }

        var thread = ThreadOf(digits);
        thread.Notes.Add(new LineNote(true, text, clock.Now.ToUnixTimeSeconds()));
        thread.Preview = text;
        thread.LastUnix = clock.Now.ToUnixTimeSeconds();
        Save();
    }

    public void MarkRead(string number)
    {
        var digits = LineOf(number);
        for (var index = 0; index < threads.Count; index++)
        {
            if (LineNumbers.Same(threads[index].Number, digits))
            {
                threads[index].Unread = 0;
                Save();
                return;
            }
        }
    }

    public string TitleOf(string number)
    {
        var digits = LineOf(number);
        var contacts = Contacts;
        for (var index = 0; index < contacts.Count; index++)
        {
            if (LineNumbers.Same(contacts[index].Number, digits))
            {
                return contacts[index].Name;
            }
        }

        return digits;
    }

    private SavedThread ThreadOf(string number)
    {
        for (var index = 0; index < threads.Count; index++)
        {
            if (LineNumbers.Same(threads[index].Number, number))
            {
                return threads[index];
            }
        }

        var created = new SavedThread { Number = number };
        threads.Add(created);
        return created;
    }

    private void ApplyAudio()
    {
        sense.RoutePhone(display.ActiveCallSpeaker, display.ActiveCallMicrophone);
        sense.MicGain = display.MicVolume * 2f;
        if (state == LineState.Idle)
        {
            sense.Halt();
            sense.StopMonitor();
            return;
        }

        if (micMuted)
        {
            sense.Halt();
        }
        else
        {
            var mic = display.ActiveCallMicrophone;
            if (mic.Length > 0)
            {
                sense.Choose(mic.StartsWith("in:", StringComparison.Ordinal) ||
                             mic.StartsWith("wavein:", StringComparison.Ordinal)
                    ? mic
                    : "in:" + mic);
            }

            sense.Start();
        }

        if (speakerMuted)
        {
            sense.StopMonitor();
        }
    }

    private void Load()
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var file = JsonSerializer.Deserialize<ShelfFile>(File.ReadAllText(path), Json);
            if (file is null)
            {
                return;
            }

            book.Clear();
            if (file.Contacts is not null)
            {
                for (var index = 0; index < file.Contacts.Length; index++)
                {
                    book.Add(Clean(file.Contacts[index]));
                }
            }

            threads.Clear();
            if (file.Threads is not null)
            {
                threads.AddRange(file.Threads);
            }

            recents.Clear();
            if (file.Recents is not null)
            {
                recents.AddRange(file.Recents);
            }

            ownNumber = LineOf(file.OwnNumber);
        }
        catch (Exception)
        {
        }
    }

    private void Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(path);
            if (folder is { Length: > 0 })
            {
                Directory.CreateDirectory(folder);
            }
            var file = new ShelfFile
            {
                Contacts = book.ToArray(),
                Threads = threads.ToArray(),
                Recents = recents.ToArray(),
                OwnNumber = ownNumber,
            };
            File.WriteAllText(path, JsonSerializer.Serialize(file, Json));
        }
        catch (Exception)
        {
        }
    }

    private void EnsureOwnNumber()
    {
        var snap = pearl.Current;
        if (!snap.SignedIn)
        {
            return;
        }

        var country = game.PhoneCountry;
        var gate = LineNumbers.Canonical(snap.MyNumber, country);
        if (gate.Length > 0)
        {
            if (!string.Equals(ownNumber, gate, StringComparison.Ordinal))
            {
                ownNumber = gate;
                Save();
            }

            return;
        }

        if (ownNumber.Length > 0)
        {
            var shaped = LineNumbers.Canonical(ownNumber, country);
            if (!string.Equals(ownNumber, shaped, StringComparison.Ordinal) && shaped.Length > 0)
            {
                ownNumber = shaped;
                Save();
            }

            return;
        }

        var seed = snap.MeId.Length > 0
            ? snap.MeId
            : game.Character.ContentId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (seed.Length == 0 || seed == "0")
        {
            return;
        }

        ownNumber = IssueNumber(seed, country);
        Save();
    }

    private static string IssueNumber(string seed, int country)
    {
        var hash = 2166136261u;
        var key = "linkpearl-line:" + seed;
        for (var index = 0; index < key.Length; index++)
        {
            hash ^= key[index];
            hash *= 16777619u;
        }

        var digits = new char[7];
        digits[0] = (char)('2' + (hash % 8));
        for (var index = 1; index < digits.Length; index++)
        {
            hash ^= (uint)(index * 33);
            hash *= 16777619u;
            digits[index] = (char)('0' + (hash % 10));
        }

        return LineNumbers.Canonical(new string(digits), country);
    }

    private static int IndexOf(List<LineContact> list, string number)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (LineNumbers.Same(list[index].Number, number))
            {
                return index;
            }
        }

        return -1;
    }

    private static LineContact Clean(LineContact row) =>
        new(TextOf(row.Name), LineOf(row.Number), row.FromGate, TextOf(row.Race), TextOf(row.World));

    private static string LineOf(string? value) => LineNumbers.Canonical(value);

    private static string TextOf(string? value) => value ?? string.Empty;

    private static string Sanitize(string? value, int cap)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var built = new char[Math.Min(value.Length, cap)];
        var count = 0;
        for (var index = 0; index < value.Length && count < cap; index++)
        {
            var ch = value[index];
            if (Allowed(ch, count == 0))
            {
                built[count++] = ch;
            }
        }

        return new string(built, 0, count);
    }

    private static bool Allowed(char ch, bool first) =>
        char.IsDigit(ch) || ch is '*' or '#' || (first && ch == '+');

    private sealed class SavedThread
    {
        public string Number { get; set; } = string.Empty;

        public string Preview { get; set; } = string.Empty;

        public long LastUnix { get; set; }

        public int Unread { get; set; }

        public List<LineNote> Notes { get; set; } = [];
    }

    private sealed class ShelfFile
    {
        public LineContact[]? Contacts { get; set; }

        public SavedThread[]? Threads { get; set; }

        public LineRecent[]? Recents { get; set; }

        public string? OwnNumber { get; set; }
    }
}
