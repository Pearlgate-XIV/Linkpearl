using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Linkpearl.Modules;

namespace Linkpearl.Applets.Life.Music;

internal sealed class MusicBook
{
    public const int MinSecret = 4;
    public const int MaxSecret = 64;
    private const int Rounds = 120_000;
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;

    public List<MusicSeat> Seats { get; } = new();

    public static MusicBook Load(HostPaths paths)
    {
        var book = new MusicBook();
        var path = paths.State("music-accounts.json");
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
            var path = paths.State("music-accounts.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, JsonSerializer.Serialize(new BookSave { Seats = Seats.ToArray() }));
        }
        catch (IOException)
        {
        }
    }

    public bool Holds(string id) =>
        id.Length > 0 && Seats.Exists(seat => string.Equals(seat.Id, id, StringComparison.Ordinal));

    public bool TrySeat(string id, out MusicSeat seat)
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

    public bool TryHandle(string handle, out MusicSeat seat)
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

    public MusicPass TryJoin(string name, string handle, string secret, string again)
    {
        var shown = name.Trim();
        if (shown.Length == 0)
        {
            return MusicPass.Fail("Add a display name.");
        }

        if (shown.Length > 24)
        {
            return MusicPass.Fail("Keep the display name under 24 characters.");
        }

        var tag = Tag(handle);
        if (tag.Length < 3)
        {
            return MusicPass.Fail("Handle needs 3 letters or numbers.");
        }

        if (TryHandle(tag, out _))
        {
            return MusicPass.Fail("That handle is already taken.");
        }

        if (secret.Length < MinSecret)
        {
            return MusicPass.Fail("Password needs " + MinSecret + " or more characters.");
        }

        if (secret.Length > MaxSecret)
        {
            return MusicPass.Fail("Keep the password under " + MaxSecret + " characters.");
        }

        if (!string.Equals(secret, again, StringComparison.Ordinal))
        {
            return MusicPass.Fail("Passwords do not match.");
        }

        Pack(secret, out var salt, out var hash);
        var seat = new MusicSeat
        {
            Id = Guid.NewGuid().ToString("N"),
            Handle = "@" + tag,
            DisplayName = shown,
            Salt = salt,
            Secret = hash,
            Rounds = Rounds,
            Born = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Face = new MusicFace { DisplayName = shown, Handle = "@" + tag },
        };
        Seats.Add(seat);
        return new MusicPass(true, string.Empty, seat);
    }

    public MusicPass TryEnter(string handle, string secret)
    {
        if (!TryHandle(handle, out var seat))
        {
            return MusicPass.Fail("That handle is not on this handset.");
        }

        if (!Matches(seat, secret))
        {
            return MusicPass.Fail("Password does not match.");
        }

        return new MusicPass(true, string.Empty, seat);
    }

    public void Keep(MusicState state)
    {
        if (!TrySeat(state.AccountId, out var seat))
        {
            return;
        }

        seat.DisplayName = state.DisplayName.Trim();
        seat.Handle = Tag(state.Handle).Length >= 3 ? "@" + Tag(state.Handle) : seat.Handle;
        seat.Face = MusicFace.From(state);
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

    private static void Pack(string secret, out string salt, out string hash)
    {
        var seed = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(secret), seed, Rounds,
            HashAlgorithmName.SHA256, KeyBytes);
        salt = Convert.ToBase64String(seed);
        hash = Convert.ToBase64String(key);
    }

    private static bool Matches(MusicSeat seat, string secret)
    {
        if (seat.Rounds < 10_000 || seat.Salt.Length == 0 || seat.Secret.Length == 0)
        {
            return false;
        }

        byte[] seed;
        byte[] expected;
        try
        {
            seed = Convert.FromBase64String(seat.Salt);
            expected = Convert.FromBase64String(seat.Secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(secret), seed, seat.Rounds,
            HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private sealed class BookSave
    {
        public MusicSeat[]? Seats { get; set; }
    }
}

internal sealed class MusicSeat
{
    public string Id { get; set; } = string.Empty;

    public string Handle { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Salt { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    public int Rounds { get; set; }

    public long Born { get; set; }

    public MusicFace Face { get; set; } = new();
}

internal sealed class MusicFace
{
    public string DisplayName { get; set; } = string.Empty;

    public string Handle { get; set; } = string.Empty;

    public string Honorific { get; set; } = string.Empty;

    public string Bio { get; set; } = string.Empty;

    public bool Onboarded { get; set; }

    public bool Listener { get; set; } = true;

    public bool Dj { get; set; }

    public bool Venue { get; set; }

    public bool UsesHandsetProfile { get; set; } = true;

    public bool UsesHandsetIdentity { get; set; } = true;

    public string DjName { get; set; } = string.Empty;

    public string StationName { get; set; } = string.Empty;

    public string StationId { get; set; } = string.Empty;

    public string StationBio { get; set; } = string.Empty;

    public string StationArtPath { get; set; } = string.Empty;

    public string ProfileFacePath { get; set; } = string.Empty;

    public string ProfileBannerPath { get; set; } = string.Empty;

    public float FaceZoom { get; set; } = 1f;

    public float FaceFocusX { get; set; } = 0.5f;

    public float FaceFocusY { get; set; } = 0.5f;

    public float BannerZoom { get; set; } = 1f;

    public float BannerFocusX { get; set; } = 0.5f;

    public float BannerFocusY { get; set; } = 0.5f;

    public string VenueName { get; set; } = string.Empty;

    public string VenuePlace { get; set; } = string.Empty;

    public string[]? Interests { get; set; }

    public string[]? StationTags { get; set; }

    public static MusicFace From(MusicState state) =>
        new()
        {
            DisplayName = state.DisplayName,
            Handle = state.Handle,
            Honorific = state.Honorific,
            Bio = state.Bio,
            Onboarded = state.Onboarded,
            Listener = state.Listener,
            Dj = state.Dj,
            Venue = state.Venue,
            UsesHandsetProfile = state.UsesHandsetProfile,
            UsesHandsetIdentity = state.UsesHandsetIdentity,
            DjName = state.DjName,
            StationName = state.StationName,
            StationId = state.StationId,
            StationBio = state.StationBio,
            StationArtPath = state.StationArtPath,
            ProfileFacePath = state.ProfileFacePath,
            ProfileBannerPath = state.ProfileBannerPath,
            FaceZoom = state.FaceZoom,
            FaceFocusX = state.FaceFocusX,
            FaceFocusY = state.FaceFocusY,
            BannerZoom = state.BannerZoom,
            BannerFocusX = state.BannerFocusX,
            BannerFocusY = state.BannerFocusY,
            VenueName = state.VenueName,
            VenuePlace = state.VenuePlace,
            Interests = state.Interests.ToArray(),
            StationTags = state.StationTags.ToArray(),
        };

    public void Apply(MusicState state)
    {
        state.DisplayName = DisplayName ?? string.Empty;
        state.Handle = Handle ?? string.Empty;
        state.Honorific = Honorific ?? string.Empty;
        state.Bio = Bio ?? string.Empty;
        state.Onboarded = Onboarded;
        state.Listener = Listener;
        state.Dj = Dj;
        state.Venue = Venue;
        state.UsesHandsetProfile = UsesHandsetProfile;
        state.UsesHandsetIdentity = UsesHandsetIdentity;
        state.DjName = DjName ?? string.Empty;
        state.StationName = StationName ?? string.Empty;
        state.StationId = StationId ?? string.Empty;
        state.StationBio = MusicState.ClampStationBio(StationBio);
        state.StationArtPath = StationArtPath ?? string.Empty;
        state.ProfileFacePath = ProfileFacePath ?? string.Empty;
        state.ProfileBannerPath = ProfileBannerPath ?? string.Empty;
        state.FaceZoom = FaceZoom > 0f ? FaceZoom : 1f;
        state.FaceFocusX = FaceFocusX;
        state.FaceFocusY = FaceFocusY;
        state.BannerZoom = BannerZoom > 0f ? BannerZoom : 1f;
        state.BannerFocusX = BannerFocusX;
        state.BannerFocusY = BannerFocusY;
        state.VenueName = VenueName ?? string.Empty;
        state.VenuePlace = VenuePlace ?? string.Empty;
        Copy(state.Interests, Interests);
        Copy(state.StationTags, StationTags);
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

internal readonly record struct MusicPass(bool Ok, string Note, MusicSeat? Seat)
{
    public static MusicPass Fail(string note) => new(false, note, null);
}
