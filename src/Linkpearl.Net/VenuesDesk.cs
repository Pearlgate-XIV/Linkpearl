using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Linkpearl.Modules;

namespace Linkpearl.Net;

public sealed class VenuesDesk : IVenuesDesk, IDisposable
{
    public const string DirectoryUrl = "https://ffxivvenues.com";
    public const string Brand = "FFXIV Venues";

    private const string IndexUrl = "https://api.ffxivvenues.com/venue";
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan MinGap = TimeSpan.FromSeconds(12);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient http = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = true,
        ConnectTimeout = TimeSpan.FromSeconds(20),
    })
    {
        Timeout = TimeSpan.FromSeconds(90),
        DefaultRequestVersion = HttpVersion.Version11,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
    };

    private readonly string artFolder;
    private readonly string indexPath;
    private readonly object gate = new();
    private readonly Dictionary<string, string> banners = new(StringComparer.Ordinal);
    private readonly HashSet<string> bannerWanted = new(StringComparer.Ordinal);
    private readonly HashSet<string> bannerFailed = new(StringComparer.Ordinal);
    private VenueSpot[] spots = [];
    private string[] centers = [];
    private bool busy;
    private string notice = "Nightlife from FFXIV Venues.";
    private DateTimeOffset? fetchedAt;
    private DateTimeOffset lastAttempt;
    private int revision;
    private int fetch;
    private int artInFlight;

    public VenuesDesk(HostPaths paths)
    {
        artFolder = paths.Cache("venues");
        indexPath = Path.Combine(artFolder, "index.json");
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Linkpearl/0.1.0 (+https://github.com/Pearlgate-XIV/Linkpearl)");
        try
        {
            Directory.CreateDirectory(artFolder);
        }
        catch
        {
            // Banners stay optional if the cache folder cannot be created.
        }

        TryLoadCache();
    }

    public IReadOnlyList<VenueSpot> Spots
    {
        get
        {
            lock (gate)
            {
                return spots;
            }
        }
    }

    public IReadOnlyList<string> DataCenters
    {
        get
        {
            lock (gate)
            {
                return centers;
            }
        }
    }

    public bool Busy
    {
        get
        {
            lock (gate)
            {
                return busy;
            }
        }
    }

    public string Notice
    {
        get
        {
            lock (gate)
            {
                return notice;
            }
        }
    }

    public DateTimeOffset? FetchedAt
    {
        get
        {
            lock (gate)
            {
                return fetchedAt;
            }
        }
    }

    public int Revision
    {
        get
        {
            lock (gate)
            {
                return revision;
            }
        }
    }

    public void Refresh(bool force = false)
    {
        var now = DateTimeOffset.UtcNow;
        lock (gate)
        {
            if (busy)
            {
                if (now - lastAttempt > TimeSpan.FromSeconds(95))
                {
                    busy = false;
                    notice = "FFXIV Venues timed out. Tap Refresh.";
                }

                return;
            }

            if (!force && fetchedAt is { } at && now - at < FreshFor)
            {
                return;
            }

            if (!force && now - lastAttempt < MinGap)
            {
                return;
            }

            busy = true;
            lastAttempt = now;
        }

        var ticket = Interlocked.Increment(ref fetch);
        _ = Task.Run(() => LoadAsync(ticket));
    }

    public VenueSpot? Find(string id)
    {
        if (id.Length == 0)
        {
            return null;
        }

        lock (gate)
        {
            for (var index = 0; index < spots.Length; index++)
            {
                if (string.Equals(spots[index].Id, id, StringComparison.Ordinal))
                {
                    return spots[index];
                }
            }
        }

        return null;
    }

    public void PrefetchBanner(string url)
    {
        if (!TryArtKey(url, out var key, out var path))
        {
            return;
        }

        lock (gate)
        {
            if (banners.ContainsKey(url) || bannerFailed.Contains(url) || bannerWanted.Contains(url))
            {
                return;
            }
        }

        if (File.Exists(path))
        {
            lock (gate)
            {
                banners[url] = path;
            }

            return;
        }

        lock (gate)
        {
            if (banners.ContainsKey(url) || bannerFailed.Contains(url) || bannerWanted.Contains(url))
            {
                return;
            }

            if (artInFlight + bannerWanted.Count >= 4)
            {
                return;
            }

            bannerWanted.Add(url);
            artInFlight++;
        }

        _ = Task.Run(() => LoadBannerAsync(url, key, path));
    }

    public string? BannerPath(string url)
    {
        lock (gate)
        {
            return banners.TryGetValue(url, out var cached) ? cached : null;
        }
    }

    public void Dispose() => http.Dispose();

    private async Task LoadAsync(int ticket)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            using var request = new HttpRequestMessage(HttpMethod.Get, IndexUrl)
            {
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var reply = await http
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token)
                .ConfigureAwait(false);
            var bytes = await reply.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false);
            if (!reply.IsSuccessStatusCode)
            {
                Fail(ticket, "FFXIV Venues is busy (" + (int)reply.StatusCode + "). Try again in a moment.");
                return;
            }

            if (!TryRead(bytes, out var mapped, out var dc))
            {
                Fail(ticket, "FFXIV Venues sent a page instead of the venue list. Try Refresh.");
                return;
            }

            try
            {
                await File.WriteAllBytesAsync(indexPath, bytes, timeout.Token).ConfigureAwait(false);
            }
            catch
            {
                // Live list still shows if the cache write is refused.
            }

            Finish(ticket, mapped, dc, "venues · " + Brand);
        }
        catch (OperationCanceledException)
        {
            Fail(ticket, "FFXIV Venues timed out. Tap Refresh.");
        }
        catch (HttpRequestException)
        {
            Fail(ticket, "Could not reach FFXIV Venues. Check the network and tap Refresh.");
        }
        catch (JsonException)
        {
            Fail(ticket, "Could not read the FFXIV Venues list. Tap Refresh.");
        }
        catch
        {
            Fail(ticket, "Could not load FFXIV Venues. Tap Refresh.");
        }
    }

    private void TryLoadCache()
    {
        try
        {
            if (!File.Exists(indexPath))
            {
                return;
            }

            if (!TryRead(File.ReadAllBytes(indexPath), out var mapped, out var dc))
            {
                return;
            }

            lock (gate)
            {
                spots = mapped;
                centers = [.. dc];
                revision++;
                notice = mapped.Length.ToString(CultureInfo.InvariantCulture) + " cached venues · updating…";
            }
        }
        catch
        {
            // Live fetch still runs if the cache file is unreadable.
        }
    }

    private static bool TryRead(byte[] bytes, out VenueSpot[] mapped, out SortedSet<string> dc)
    {
        mapped = [];
        dc = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        if (bytes.Length < 2 || bytes[0] != (byte)'[')
        {
            return false;
        }

        using var doc = JsonDocument.Parse(bytes);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var list = new List<VenueSpot>(doc.RootElement.GetArrayLength());
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            try
            {
                var spot = Map(item.Deserialize<VenueWire>(Json), now);
                if (spot is null)
                {
                    continue;
                }

                list.Add(spot);
                if (spot.DataCenter.Length > 0)
                {
                    dc.Add(spot.DataCenter);
                }
            }
            catch
            {
                // One bad row must not wipe the rest of the index.
            }
        }

        if (list.Count == 0)
        {
            return false;
        }

        list.Sort(Compare);
        mapped = list.ToArray();
        return true;
    }

    private void Finish(int ticket, VenueSpot[] mapped, SortedSet<string> dc, string suffix)
    {
        lock (gate)
        {
            if (ticket != fetch)
            {
                return;
            }

            spots = mapped;
            centers = [.. dc];
            fetchedAt = DateTimeOffset.UtcNow;
            busy = false;
            revision++;
            notice = mapped.Length.ToString(CultureInfo.InvariantCulture) + " " + suffix;
        }
    }

    private void Fail(int ticket, string text)
    {
        lock (gate)
        {
            if (ticket != fetch)
            {
                return;
            }

            busy = false;
            notice = text;
        }
    }

    private async Task LoadBannerAsync(string url, string key, string path)
    {
        _ = key;
        try
        {
            using var reply = await http.GetAsync(url).ConfigureAwait(false);
            if (!reply.IsSuccessStatusCode)
            {
                RememberBanner(url, failed: true);
                return;
            }

            var bytes = await reply.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            if (bytes.Length < 32)
            {
                RememberBanner(url, failed: true);
                return;
            }

            await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
            lock (gate)
            {
                banners[url] = path;
                bannerWanted.Remove(url);
                artInFlight = Math.Max(0, artInFlight - 1);
            }
        }
        catch
        {
            RememberBanner(url, failed: true);
        }
    }

    private void RememberBanner(string url, bool failed)
    {
        lock (gate)
        {
            bannerWanted.Remove(url);
            artInFlight = Math.Max(0, artInFlight - 1);
            if (failed)
            {
                bannerFailed.Add(url);
            }
        }
    }

    private bool TryArtKey(string url, out string key, out string path)
    {
        key = string.Empty;
        path = string.Empty;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "images.ffxivvenues.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        key = SafeName(parts[0]) + "_" + SafeName(parts[1]);
        path = Path.Combine(artFolder, key);
        return key.Length > 2;
    }

    private static string SafeName(string value)
    {
        var chars = value.ToCharArray();
        for (var index = 0; index < chars.Length; index++)
        {
            if (!char.IsLetterOrDigit(chars[index]) && chars[index] != '-')
            {
                chars[index] = '_';
            }
        }

        return new string(chars);
    }

    private static VenueSpot? Map(VenueWire? wire, DateTimeOffset now)
    {
        if (wire is null || string.IsNullOrWhiteSpace(wire.Id) || string.IsNullOrWhiteSpace(wire.Name))
        {
            return null;
        }

        if (wire.Approved == false)
        {
            return null;
        }

        var place = wire.Location;
        var hours = Resolve(wire, now);
        var open = hours is { } live && live.OpenNow;
        return new VenueSpot
        {
            Id = wire.Id.Trim(),
            Name = wire.Name.Trim(),
            BannerUrl = Text(wire.BannerUri),
            DataCenter = Text(place?.DataCenter),
            World = Text(place?.World),
            District = Text(place?.District),
            Ward = place?.Ward ?? 0,
            Plot = place?.Plot ?? 0,
            Apartment = place?.Apartment ?? 0,
            Room = place?.Room ?? 0,
            Subdivision = place?.Subdivision ?? false,
            Address = Address(place),
            Description = Join(wire.Description),
            Website = Text(wire.Website),
            Discord = Text(wire.Discord),
            DirectoryUrl = DirectoryUrl.TrimEnd('/') + "/venue/" + Uri.EscapeDataString(wire.Id.Trim()),
            Sfw = wire.Sfw,
            OpenHouse = wire.OpenHouse,
            OpenNow = open,
            OpenUntil = open ? hours?.End : null,
            NextOpen = open ? null : hours?.Start,
            HoursLine = Line(hours, open, now),
            Tags = Tags(wire.Tags),
            Week = WeekOf(wire),
        };
    }

    private static VenueHours? Resolve(VenueWire wire, DateTimeOffset now)
    {
        if (ClosedNow(wire.ScheduleOverrides, now))
        {
            return Next(wire, now, skipCurrent: true);
        }

        return Next(wire, now, skipCurrent: false);
    }

    private static bool ClosedNow(OverrideWire[]? overrides, DateTimeOffset now)
    {
        if (overrides is null)
        {
            return false;
        }

        for (var index = 0; index < overrides.Length; index++)
        {
            var mark = overrides[index];
            if (mark.Open || mark.Start is null || mark.End is null)
            {
                continue;
            }

            if (now >= mark.Start.Value && now < mark.End.Value)
            {
                return true;
            }
        }

        return false;
    }

    private static VenueHours? Next(VenueWire wire, DateTimeOffset now, bool skipCurrent)
    {
        VenueHours? pick = null;
        Consider(ref pick, wire.Resolution, now, skipCurrent);

        var overrides = wire.ScheduleOverrides ?? [];
        for (var index = 0; index < overrides.Length; index++)
        {
            var mark = overrides[index];
            if (!mark.Open)
            {
                continue;
            }

            Consider(ref pick, new OpeningWire { Start = mark.Start, End = mark.End, IsNow = mark.IsNow }, now,
                skipCurrent);
        }

        var slots = wire.Schedule ?? [];
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            Consider(ref pick, slot.Resolution, now, skipCurrent);
            if (slot.Resolution is { Start: not null, End: not null })
            {
                continue;
            }

            if (!TryWindow(slot, now, out var start, out var end))
            {
                continue;
            }

            Consider(ref pick, new VenueHours(start, end, now >= start && now < end), skipCurrent);
        }

        return pick;
    }

    private static void Consider(ref VenueHours? pick, OpeningWire? opening, DateTimeOffset now, bool skipCurrent)
    {
        if (opening?.Start is null || opening.End is null)
        {
            return;
        }

        var start = opening.Start.Value;
        var end = opening.End.Value;
        if (end <= start)
        {
            return;
        }

        Consider(ref pick, new VenueHours(start, end, opening.IsNow || now >= start && now < end), skipCurrent);
    }

    private static void Consider(ref VenueHours? pick, VenueHours hours, bool skipCurrent)
    {
        if (hours.OpenNow)
        {
            if (!skipCurrent)
            {
                pick = hours;
            }

            return;
        }

        if (pick is { OpenNow: true } || hours.Start <= DateTimeOffset.UtcNow)
        {
            return;
        }

        if (pick is null || hours.Start < pick.Value.Start)
        {
            pick = hours;
        }
    }

    private static bool TryWindow(ScheduleWire slot, DateTimeOffset now, out DateTimeOffset start,
        out DateTimeOffset end)
    {
        start = default;
        end = default;
        if (slot.Start is null)
        {
            return false;
        }

        if (slot.Commencing is { } from && now < from)
        {
            return false;
        }

        if (!TryZone(slot.Start.TimeZone ?? slot.End?.TimeZone, out var zone))
        {
            return false;
        }

        var local = TimeZoneInfo.ConvertTime(now, zone);
        var day = DayOf(slot.Day);
        VenueHours? best = null;
        for (var week = -1; week <= 8; week++)
        {
            var delta = ((int)day - (int)local.DayOfWeek + 7) % 7 + week * 7;
            var date = local.Date.AddDays(delta);
            if (slot.Commencing is { } commence && date.Date < commence.LocalDateTime.Date)
            {
                continue;
            }

            if (!FitsInterval(slot, date))
            {
                continue;
            }

            var begin = Stamp(date, slot.Start, slot.Start.NextDay);
            var close = slot.End is null
                ? begin.AddHours(3)
                : Stamp(date, slot.End, slot.End.NextDay || Minutes(slot.End) <= Minutes(slot.Start));
            if (close <= begin)
            {
                close = close.AddDays(1);
            }

            DateTimeOffset beginAt;
            DateTimeOffset closeAt;
            try
            {
                var beginUtc = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(begin, DateTimeKind.Unspecified), zone);
                var closeUtc = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(close, DateTimeKind.Unspecified), zone);
                beginAt = new DateTimeOffset(beginUtc, TimeSpan.Zero);
                closeAt = new DateTimeOffset(closeUtc, TimeSpan.Zero);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (closeAt <= now && week >= 0)
            {
                continue;
            }

            Consider(ref best, new VenueHours(beginAt, closeAt, now >= beginAt && now < closeAt), skipCurrent: false);
            if (best is { OpenNow: true })
            {
                break;
            }
        }

        if (best is null)
        {
            return false;
        }

        start = best.Value.Start;
        end = best.Value.End;
        return end > start;
    }

    private static bool FitsInterval(ScheduleWire slot, DateTime date)
    {
        var every = slot.Interval?.IntervalArgument ?? 1;
        if (every <= 1 || slot.Commencing is null)
        {
            return true;
        }

        var origin = slot.Commencing.Value.LocalDateTime.Date;
        var weeks = (int)Math.Floor((date.Date - origin).TotalDays / 7d);
        return weeks >= 0 && weeks % every == 0;
    }

    private static DateTime Stamp(DateTime date, TimeWire time, bool nextDay)
    {
        var hour = Math.Clamp(time.Hour, 0, 23);
        var minute = Math.Clamp(time.Minute, 0, 59);
        var stamp = date.Date.AddHours(hour).AddMinutes(minute);
        return nextDay ? stamp.AddDays(1) : stamp;
    }

    private static int Minutes(TimeWire time) => time.Hour * 60 + time.Minute;

    private static bool TryZone(string? id, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static DayOfWeek DayOf(JsonElement day)
    {
        if (day.ValueKind == JsonValueKind.Number && day.TryGetInt32(out var number))
        {
            // Public index: Monday = 0 … Sunday = 6.
            var index = Math.Clamp(number, 0, 6);
            return index == 6 ? DayOfWeek.Sunday : (DayOfWeek)(index + 1);
        }

        if (day.ValueKind == JsonValueKind.String)
        {
            var name = day.GetString() ?? string.Empty;
            return Enum.TryParse<DayOfWeek>(name, true, out var parsed) ? parsed : DayOfWeek.Friday;
        }

        return DayOfWeek.Friday;
    }

    private static VenueWeekMark[] WeekOf(VenueWire wire)
    {
        var slots = wire.Schedule ?? [];
        if (slots.Length == 0)
        {
            return [];
        }

        var marks = new List<VenueWeekMark>(slots.Length);
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            if (slot.Start is null)
            {
                continue;
            }

            var end = slot.End;
            marks.Add(new VenueWeekMark(
                DayOf(slot.Day),
                slot.Start.Hour,
                slot.Start.Minute,
                slot.Start.NextDay,
                end?.Hour ?? Math.Min(23, slot.Start.Hour + 3),
                end?.Minute ?? 0,
                end?.NextDay ?? false,
                Text(slot.Start.TimeZone ?? end?.TimeZone),
                slot.Interval?.IntervalArgument ?? 1,
                slot.Commencing));
        }

        return marks.ToArray();
    }

    private static string Address(LocationWire? place)
    {
        if (place is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(place.Override))
        {
            return place.Override.Trim();
        }

        var parts = new List<string>(6);
        if (!string.IsNullOrWhiteSpace(place.World))
        {
            parts.Add(place.World.Trim());
        }

        if (!string.IsNullOrWhiteSpace(place.District))
        {
            parts.Add(place.District.Trim());
        }

        if (place.Ward > 0)
        {
            parts.Add("Ward " + place.Ward + (place.Subdivision ? " Sub" : string.Empty));
        }

        if (place.Plot > 0)
        {
            parts.Add("Plot " + place.Plot);
        }
        else if (place.Apartment > 0)
        {
            parts.Add("Apt " + place.Apartment);
        }

        if (place.Room > 0)
        {
            parts.Add("Room " + place.Room);
        }

        return string.Join(" · ", parts);
    }

    private static string Line(VenueHours? hours, bool open, DateTimeOffset now)
    {
        if (hours is null)
        {
            return "Hours not listed";
        }

        var local = hours.Value.Start.ToLocalTime();
        var until = hours.Value.End.ToLocalTime();
        if (open)
        {
            return "Open now · until " + until.ToString("h:mm tt", CultureInfo.CurrentCulture);
        }

        var when = local.Date == now.ToLocalTime().Date
            ? local.ToString("h:mm tt", CultureInfo.CurrentCulture)
            : local.ToString("ddd h:mm tt", CultureInfo.CurrentCulture);
        return "Opens " + when;
    }

    private static string Join(string[]? lines)
    {
        if (lines is null || lines.Length == 0)
        {
            return string.Empty;
        }

        return string.Join("\n", lines.Select(line => line.Trim()).Where(line => line.Length > 0));
    }

    private static string[] Tags(string[]? tags)
    {
        if (tags is null || tags.Length == 0)
        {
            return [];
        }

        var kept = new List<string>(tags.Length);
        for (var index = 0; index < tags.Length; index++)
        {
            var tag = (tags[index] ?? string.Empty).Trim();
            if (tag.Length == 0)
            {
                continue;
            }

            kept.Add(tag);
        }

        return kept.ToArray();
    }

    private static string Text(string? value) => (value ?? string.Empty).Trim();

    private static int Compare(VenueSpot left, VenueSpot right)
    {
        if (left.OpenNow != right.OpenNow)
        {
            return left.OpenNow ? -1 : 1;
        }

        var leftAt = left.OpenNow ? left.OpenUntil : left.NextOpen;
        var rightAt = right.OpenNow ? right.OpenUntil : right.NextOpen;
        var time = Nullable.Compare(leftAt, rightAt);
        return time != 0 ? time : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class VenueWire
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? BannerUri { get; set; }
        public string[]? Description { get; set; }
        public LocationWire? Location { get; set; }
        public string? Website { get; set; }
        public string? Discord { get; set; }
        public bool OpenHouse { get; set; }
        public bool Sfw { get; set; } = true;
        public bool? Approved { get; set; }
        public string[]? Tags { get; set; }
        public ScheduleWire[]? Schedule { get; set; }
        public OverrideWire[]? ScheduleOverrides { get; set; }
        public OpeningWire? Resolution { get; set; }
    }

    private sealed class LocationWire
    {
        public string? DataCenter { get; set; }
        public string? World { get; set; }
        public string? District { get; set; }
        public int Ward { get; set; }
        public int Plot { get; set; }
        public int Apartment { get; set; }
        public int Room { get; set; }
        public bool Subdivision { get; set; }
        public string? Override { get; set; }
    }

    private sealed class ScheduleWire
    {
        public DateTimeOffset? Commencing { get; set; }
        public JsonElement Day { get; set; }
        public TimeWire? Start { get; set; }
        public TimeWire? End { get; set; }
        public IntervalWire? Interval { get; set; }
        public OpeningWire? Resolution { get; set; }
    }

    private sealed class TimeWire
    {
        public int Hour { get; set; }
        public int Minute { get; set; }
        public string? TimeZone { get; set; }
        public bool NextDay { get; set; }
    }

    private sealed class IntervalWire
    {
        public int IntervalArgument { get; set; } = 1;
    }

    private sealed class OpeningWire
    {
        public DateTimeOffset? Start { get; set; }
        public DateTimeOffset? End { get; set; }
        public bool IsNow { get; set; }
    }

    private sealed class OverrideWire
    {
        public bool Open { get; set; }
        public bool IsNow { get; set; }
        public DateTimeOffset? Start { get; set; }
        public DateTimeOffset? End { get; set; }
    }
}
