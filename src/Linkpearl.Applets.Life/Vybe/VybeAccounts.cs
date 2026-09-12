using System.Text.Json;
using Linkpearl.Modules;

namespace Linkpearl.Applets.Life.Vybe;

internal sealed class VybeBook
{
    public List<VybeSeat> Seats { get; } = new();

    public static VybeBook Load(HostPaths paths)
    {
        var book = new VybeBook();
        var path = paths.State("vybe-accounts.json");
        if (!File.Exists(path))
        {
            return book;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<BookSave>(File.ReadAllText(path));
            if (dto?.Seats is { Length: > 0 })
            {
                foreach (var seat in dto.Seats)
                {
                    if (seat.Id is { Length: > 0 } && seat.Handle is { Length: > 0 })
                    {
                        book.Seats.Add(seat);
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

        return book;
    }

    public void Save(HostPaths paths)
    {
        try
        {
            var path = paths.State("vybe-accounts.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, JsonSerializer.Serialize(new BookSave { Seats = Seats.ToArray() }));
        }
        catch (IOException)
        {
        }
    }

    public bool Holds(string id) =>
        id.Length > 0 && Seats.Exists(seat => string.Equals(seat.Id, id, StringComparison.Ordinal));

    public bool TrySeat(string id, out VybeSeat seat)
    {
        foreach (var entry in Seats)
        {
            if (string.Equals(entry.Id, id, StringComparison.Ordinal))
            {
                seat = entry;
                return true;
            }
        }

        seat = default!;
        return false;
    }

    public bool TryHandle(string handle, out VybeSeat seat)
    {
        var tag = Tag(handle);
        foreach (var entry in Seats)
        {
            if (string.Equals(Tag(entry.Handle), tag, StringComparison.Ordinal))
            {
                seat = entry;
                return true;
            }
        }

        seat = default!;
        return false;
    }

    public VybePass TryJoin(string name, string handle)
    {
        var shown = name.Trim();
        if (shown.Length == 0)
        {
            return VybePass.Fail("Add a display name.");
        }

        if (shown.Length > 24)
        {
            return VybePass.Fail("Keep the display name under 24 characters.");
        }

        var tag = Tag(handle);
        if (tag.Length < 3)
        {
            return VybePass.Fail("Handle needs 3 letters or numbers.");
        }

        if (TryHandle(tag, out _))
        {
            return VybePass.Fail("That handle is already taken.");
        }

        var seat = new VybeSeat
        {
            Id = Guid.NewGuid().ToString("N"),
            Handle = "@" + tag,
            DisplayName = shown,
            Born = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Face = new VybeFace { DisplayName = shown, Handle = "@" + tag },
        };
        Seats.Add(seat);
        return new VybePass(true, string.Empty, seat);
    }

    public VybePass TryEnter(string handle, string secret)
    {
        if (!TryHandle(handle, out var seat))
        {
            return VybePass.Fail("That handle is not on this handset.");
        }

        return new VybePass(true, string.Empty, seat);
    }

    public void Keep(VybeState state)
    {
        if (!TrySeat(state.AccountId, out var seat))
        {
            return;
        }

        seat.DisplayName = state.DisplayName.Trim();
        seat.Handle = Tag(state.Handle).Length >= 3 ? "@" + Tag(state.Handle) : seat.Handle;
        seat.Face = VybeFace.From(state);
    }

    public void Drop(string id)
    {
        Seats.RemoveAll(seat => string.Equals(seat.Id, id, StringComparison.Ordinal));
    }

    public static string Tag(string handle)
    {
        var raw = handle.Trim();
        if (raw.StartsWith('@'))
        {
            raw = raw[1..];
        }

        var chars = new char[raw.Length];
        var count = 0;
        foreach (var glyph in raw)
        {
            if (char.IsAsciiLetterOrDigit(glyph) || glyph == '_')
            {
                chars[count++] = char.ToLowerInvariant(glyph);
            }
        }

        return count > 20 ? new string(chars, 0, 20) : new string(chars, 0, count);
    }

    public static string ShownHandle(string handle)
    {
        var tag = Tag(handle);
        return tag.Length == 0 ? string.Empty : "@" + tag;
    }

    private sealed class BookSave
    {
        public VybeSeat[]? Seats { get; set; }
    }
}

internal sealed class VybeSeat
{
    public string Id { get; set; } = string.Empty;

    public string Handle { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Salt { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    public int Rounds { get; set; }

    public long Born { get; set; }

    public VybeFace Face { get; set; } = new();
}

internal sealed class VybeFace
{
    public string DisplayName { get; set; } = string.Empty;

    public string Handle { get; set; } = string.Empty;

    public string Honorific { get; set; } = string.Empty;

    public string Pronouns { get; set; } = string.Empty;

    public string About { get; set; } = string.Empty;

    public bool Onboarded { get; set; }

    public bool Discoverable { get; set; } = true;

    public bool PlusAgreed { get; set; }

    public bool Night { get; set; }

    public bool PickedMode { get; set; }

    public bool UsesHandsetProfile { get; set; } = true;

    public bool UsesHandsetIdentity { get; set; } = true;

    public string ProfileFacePath { get; set; } = string.Empty;

    public string ProfileBannerPath { get; set; } = string.Empty;

    public float FaceZoom { get; set; } = 1f;

    public float FaceFocusX { get; set; } = 0.5f;

    public float FaceFocusY { get; set; } = 0.5f;

    public float BannerZoom { get; set; } = 1f;

    public float BannerFocusX { get; set; } = 0.5f;

    public float BannerFocusY { get; set; } = 0.5f;

    public string Relationship { get; set; } = "Rather not say";

    public string Race { get; set; } = string.Empty;

    public bool DmsOpen { get; set; } = true;

    public string[]? Genders { get; set; }

    public string[]? Sexualities { get; set; }

    public string[]? Intents { get; set; }

    public string[]? Tags { get; set; }

    public bool LaneDone { get; set; }

    public int[]? LaneMarks { get; set; }

    public static VybeFace From(VybeState state) =>
        new()
        {
            DisplayName = state.DisplayName,
            Handle = state.Handle,
            Honorific = state.Honorific,
            Pronouns = state.Pronouns,
            About = state.About,
            Onboarded = state.Onboarded,
            Discoverable = state.Discoverable,
            PlusAgreed = state.PlusAgreed,
            Night = state.Night,
            PickedMode = state.PickedMode,
            UsesHandsetProfile = state.UsesHandsetProfile,
            UsesHandsetIdentity = state.UsesHandsetIdentity,
            ProfileFacePath = state.ProfileFacePath,
            ProfileBannerPath = state.ProfileBannerPath,
            FaceZoom = state.FaceZoom,
            FaceFocusX = state.FaceFocusX,
            FaceFocusY = state.FaceFocusY,
            BannerZoom = state.BannerZoom,
            BannerFocusX = state.BannerFocusX,
            BannerFocusY = state.BannerFocusY,
            Relationship = state.Relationship,
            Race = state.Race,
            DmsOpen = state.DmsOpen,
            Genders = state.Genders.ToArray(),
            Sexualities = state.Sexualities.ToArray(),
            Intents = state.Intents.ToArray(),
            Tags = state.Tags.ToArray(),
            LaneDone = state.LaneDone,
            LaneMarks = VybeLaneMap.Fit(state.LaneMarks),
        };

    public void Apply(VybeState state)
    {
        state.DisplayName = DisplayName ?? string.Empty;
        state.Handle = Handle ?? string.Empty;
        state.Honorific = Honorific ?? string.Empty;
        state.Pronouns = Pronouns ?? string.Empty;
        state.About = About ?? string.Empty;
        state.Onboarded = Onboarded;
        state.Discoverable = Discoverable;
        state.PlusAgreed = PlusAgreed;
        state.Consented = PlusAgreed;
        state.Mode = PlusAgreed && Night ? SocialMode.AfterDark : SocialMode.Daylight;
        state.PickedMode = PickedMode;
        state.UsesHandsetProfile = UsesHandsetProfile;
        state.UsesHandsetIdentity = UsesHandsetIdentity;
        state.ProfileFacePath = ProfileFacePath ?? string.Empty;
        state.ProfileBannerPath = ProfileBannerPath ?? string.Empty;
        state.FaceZoom = FaceZoom > 0f ? FaceZoom : 1f;
        state.FaceFocusX = FaceFocusX;
        state.FaceFocusY = FaceFocusY;
        state.BannerZoom = BannerZoom > 0f ? BannerZoom : 1f;
        state.BannerFocusX = BannerFocusX;
        state.BannerFocusY = BannerFocusY;
        state.Relationship = string.IsNullOrWhiteSpace(Relationship) ? "Rather not say" : Relationship;
        state.Race = SceneBook.NamedRace(0, Race ?? string.Empty);
        state.DmsOpen = DmsOpen;
        Copy(state.Genders, Genders);
        Copy(state.Sexualities, Sexualities);
        Copy(state.Intents, Intents);
        Copy(state.Tags, Tags);
        state.LaneDone = LaneDone;
        state.LaneMarks = VybeLaneMap.Fit(LaneMarks);
    }

    private static void Copy(List<string> target, string[]? source)
    {
        target.Clear();
        if (source is not { Length: > 0 })
        {
            return;
        }

        foreach (var part in source)
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                target.Add(part.Trim());
            }
        }
    }
}

internal readonly record struct VybePass(bool Ok, string Note, VybeSeat? Seat)
{
    public static VybePass Fail(string note) => new(false, note, null);
}
