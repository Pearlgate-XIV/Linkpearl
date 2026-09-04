using System.Text.Json;
using Linkpearl.Modules;

namespace Linkpearl.Applets.Life.Music;

internal enum MusicTab : byte
{
    Home = 0,
    Discover = 1,
    Library = 2,
    Profile = 3,
}

internal enum MusicPage : byte
{
    Tabs = 0,
    Onboard = 1,
    SetupListener = 2,
    SetupDj = 3,
    SetupVenue = 4,
    Unified = 5,
    Player = 6,
    DjDash = 7,
    VenueDash = 8,
    Roles = 9,
    Switcher = 10,
    EditProfile = 11,
}

internal sealed class MusicState
{
    public bool Onboarded { get; set; }

    public bool Listener { get; set; } = true;

    public bool Dj { get; set; }

    public bool Venue { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Handle { get; set; } = string.Empty;

    public string DjName { get; set; } = string.Empty;

    public string StationName { get; set; } = string.Empty;

    public string StationId { get; set; } = string.Empty;

    public string StationBio { get; set; } = string.Empty;

    public string StationArtPath { get; set; } = string.Empty;

    public string StationMount { get; set; } = string.Empty;

    public string IcecastHost { get; set; } = string.Empty;

    public string IcecastPassword { get; set; } = string.Empty;

    public string CaptureId { get; set; } = string.Empty;

    public string CaptureName { get; set; } = string.Empty;

    public string CaptureApp { get; set; } = "sound";

    public string Bio { get; set; } = string.Empty;

    public string VenueName { get; set; } = string.Empty;

    public string VenuePlace { get; set; } = string.Empty;

    public string Search { get; set; } = string.Empty;

    public List<string> Interests { get; } = new();

    public HashSet<string> Favorites { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Following { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string ViewingId { get; set; } = string.Empty;

    public string PeopleQuery { get; set; } = string.Empty;

    public MusicTab Tab { get; set; } = MusicTab.Home;

    public bool DiscoverLive { get; set; }

    public MusicPage Page { get; set; } = MusicPage.Onboard;

    public MusicPage ReturnTo { get; set; }

    public int GenreIndex { get; set; }

    public float Scroll { get; set; }

    public float SheetHeight { get; set; }

    public static readonly string[] Genres =
    {
        "Lo-Fi", "Rock", "Metal", "Electronic", "Bass", "Dubstep", "Techno", "Chill", "House", "Ambient",
    };

    public string Genre => Genres[Math.Clamp(GenreIndex, 0, Genres.Length - 1)];

    public static string NormalizeCapture(string? app) =>
        string.Equals(app, "mic", StringComparison.OrdinalIgnoreCase) ? "mic" : "sound";

    public static string SlugMount(string value)
    {
        var text = value.Trim().ToLowerInvariant();
        if (text.StartsWith('/'))
        {
            text = text[1..];
        }

        var slug = new char[Math.Min(text.Length, 32)];
        var count = 0;
        var dash = false;
        for (var index = 0; index < text.Length && count < slug.Length; index++)
        {
            var glyph = text[index];
            if (glyph is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                slug[count++] = glyph;
                dash = false;
                continue;
            }

            if (count > 0 && !dash)
            {
                slug[count++] = '-';
                dash = true;
            }
        }

        return count == 0 ? string.Empty : new string(slug, 0, count).Trim('-');
    }

    public static MusicState Load(HostPaths paths, string fallbackName)
    {
        var state = new MusicState();
        var path = paths.State("music.json");
        if (File.Exists(path))
        {
            try
            {
                var dto = JsonSerializer.Deserialize<MusicSave>(File.ReadAllText(path));
                if (dto is not null)
                {
                    state.Onboarded = dto.Onboarded;
                    state.Listener = dto.Listener;
                    state.Dj = dto.Dj;
                    state.Venue = dto.Venue;
                    state.DisplayName = dto.DisplayName ?? string.Empty;
                    state.Handle = dto.Handle ?? string.Empty;
                    state.DjName = dto.DjName ?? string.Empty;
                    state.StationName = dto.StationName ?? string.Empty;
                    state.StationId = dto.StationId ?? string.Empty;
                    state.StationBio = dto.StationBio ?? string.Empty;
                    state.StationArtPath = dto.StationArtPath ?? string.Empty;
                    state.StationMount = SlugMount(dto.StationMount ?? string.Empty);
                    state.IcecastHost = (dto.IcecastHost ?? string.Empty).Trim();
                    state.IcecastPassword = dto.IcecastPassword ?? string.Empty;
                    state.CaptureId = dto.CaptureId ?? string.Empty;
                    state.CaptureName = dto.CaptureName ?? string.Empty;
                    state.CaptureApp = NormalizeCapture(dto.CaptureApp);
                    state.Bio = dto.Bio ?? string.Empty;
                    state.VenueName = dto.VenueName ?? string.Empty;
                    state.VenuePlace = dto.VenuePlace ?? string.Empty;
                    state.GenreIndex = Math.Clamp(dto.GenreIndex, 0, Genres.Length - 1);
                    if (dto.Interests is { Length: > 0 })
                    {
                        state.Interests.AddRange(dto.Interests);
                    }

                    if (dto.Favorites is { Length: > 0 })
                    {
                        foreach (var id in dto.Favorites)
                        {
                            if (!string.IsNullOrWhiteSpace(id))
                            {
                                state.Favorites.Add(id.Trim());
                            }
                        }
                    }

                    if (dto.Following is { Length: > 0 })
                    {
                        foreach (var id in dto.Following)
                        {
                            if (!string.IsNullOrWhiteSpace(id))
                            {
                                state.Following.Add(id.Trim());
                            }
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

        if (state.DisplayName.Length == 0)
        {
            state.DisplayName = fallbackName.Length > 0 ? fallbackName : "Listener";
        }

        if (state.Handle.Length == 0)
        {
            state.Handle = "@" + state.DisplayName.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        }

        if (state.Interests.Count == 0)
        {
            state.Interests.Add(Genres[0]);
        }

        state.Page = state.Onboarded ? MusicPage.Tabs : MusicPage.Onboard;
        return state;
    }

    public void Save(HostPaths paths)
    {
        try
        {
            var path = paths.State("music.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, JsonSerializer.Serialize(new MusicSave
            {
                Onboarded = Onboarded,
                Listener = Listener,
                Dj = Dj,
                Venue = Venue,
                DisplayName = DisplayName,
                Handle = Handle,
                DjName = DjName,
                StationName = StationName,
                StationId = StationId,
                StationBio = StationBio,
                StationArtPath = StationArtPath,
                StationMount = StationMount,
                IcecastHost = IcecastHost,
                IcecastPassword = IcecastPassword,
                CaptureId = CaptureId,
                CaptureName = CaptureName,
                CaptureApp = CaptureApp,
                Bio = Bio,
                VenueName = VenueName,
                VenuePlace = VenuePlace,
                GenreIndex = GenreIndex,
                Interests = Interests.ToArray(),
                Favorites = Favorites.ToArray(),
                Following = Following.ToArray(),
            }));
        }
        catch (IOException)
        {
        }
    }

    public void Open(MusicPage page)
    {
        ReturnTo = Page;
        Page = page;
        Scroll = 0f;
        SheetHeight = 0f;
    }

    public void Back()
    {
        Page = ReturnTo == Page ? MusicPage.Tabs : ReturnTo;
        ReturnTo = MusicPage.Tabs;
        Scroll = 0f;
        SheetHeight = 0f;
    }

    public void OpenProfile(string id)
    {
        ViewingId = id;
        Open(MusicPage.Unified);
    }

    public bool ToggleFollow(string id)
    {
        if (id.Length == 0)
        {
            return false;
        }

        if (!Following.Remove(id))
        {
            Following.Add(id);
            return true;
        }

        return false;
    }

    public void ToggleFavorite(string id)
    {
        if (id.Length == 0)
        {
            return;
        }

        if (!Favorites.Remove(id))
        {
            Favorites.Add(id);
        }
    }

    private sealed class MusicSave
    {
        public bool Onboarded { get; set; }

        public bool Listener { get; set; }

        public bool Dj { get; set; }

        public bool Venue { get; set; }

        public string? DisplayName { get; set; }

        public string? Handle { get; set; }

        public string? DjName { get; set; }

        public string? StationName { get; set; }

        public string? StationId { get; set; }

        public string? StationBio { get; set; }

        public string? StationArtPath { get; set; }

        public string? StationMount { get; set; }

        public string? IcecastHost { get; set; }

        public string? IcecastPassword { get; set; }

        public string? CaptureId { get; set; }

        public string? CaptureName { get; set; }

        public string? CaptureApp { get; set; }

        public string? Bio { get; set; }

        public string? VenueName { get; set; }

        public string? VenuePlace { get; set; }

        public int GenreIndex { get; set; }

        public string[]? Interests { get; set; }

        public string[]? Favorites { get; set; }

        public string[]? Following { get; set; }
    }
}
