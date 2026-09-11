using System.Globalization;
using Linkpearl.Applets.Life.Calendar;
using Linkpearl.Net;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Venues;

public sealed class VenuesDiary : IDisposable
{
    public const string Source = "venues";

    private readonly VenuesBook book;
    private readonly CalendarBook calendar;
    private readonly IVenuesDesk desk;
    private readonly IClock clock;
    private readonly IFrameLoop frames;
    private double lastSync;
    private int lastRevision = -1;

    public VenuesDiary(VenuesBook book, CalendarBook calendar, IVenuesDesk desk, IClock clock, IFrameLoop frames)
    {
        this.book = book;
        this.calendar = calendar;
        this.desk = desk;
        this.clock = clock;
        this.frames = frames;
        frames.Tick += OnTick;
    }

    public bool Notifies(string id) => book.WatchesId(id);

    public bool Schedules(string id) => book.PinsId(id);

    public string ToggleNotify(VenueSpot spot)
    {
        if (spot.Id.Length == 0)
        {
            return string.Empty;
        }

        if (book.WatchesId(spot.Id))
        {
            calendar.Drop(WatchId(spot.Id));
            book.SetWatch(spot.Id, false);
            return "Notify removed.";
        }

        if (spot.OpenNow)
        {
            return "Notify is only for venues that are not open yet.";
        }

        if (!TryNextOpen(spot, out var open))
        {
            return "This venue has no upcoming open time to notify.";
        }

        PutWatch(spot, open);
        book.SetWatch(spot.Id, true);
        return "You'll get a reminder before " +
               open.ToLocalTime().ToString("ddd h:mm tt", CultureInfo.CurrentCulture) + ".";
    }

    public string ToggleSchedule(VenueSpot spot)
    {
        if (spot.Id.Length == 0)
        {
            return string.Empty;
        }

        if (book.PinsId(spot.Id))
        {
            calendar.DropPrefixed(PinPrefix(spot.Id));
            book.SetPin(spot.Id, false);
            return "Removed from calendar.";
        }

        if (!WriteHorizon(spot))
        {
            return "This venue has no weekly hours to put on the calendar.";
        }

        book.SetPin(spot.Id, true);
        return "Open days and times are on your calendar. Tap Scheduled to remove them.";
    }

    public void ForgetCalendar(string itemId)
    {
        if (itemId.StartsWith("vn:", StringComparison.Ordinal))
        {
            var id = itemId[3..];
            if (id.Length == 0)
            {
                return;
            }

            calendar.Drop(WatchId(id));
            book.SetWatch(id, false);
            return;
        }

        if (!itemId.StartsWith("vs:", StringComparison.Ordinal))
        {
            return;
        }

        var rest = itemId[3..];
        var cut = rest.LastIndexOf(':');
        var venueId = cut > 0 ? rest[..cut] : rest;
        if (venueId.Length == 0)
        {
            return;
        }

        calendar.DropPrefixed(PinPrefix(venueId));
        book.SetPin(venueId, false);
    }

    public void Dispose() => frames.Tick -= OnTick;

    private void OnTick(float _)
    {
        if (clock.Now.ToUnixTimeSeconds() - lastSync < 30 && desk.Revision == lastRevision)
        {
            return;
        }

        lastSync = clock.Now.ToUnixTimeSeconds();
        lastRevision = desk.Revision;
        SyncWatches();
        SyncPins();
    }

    private void SyncWatches()
    {
        var ids = book.Watches;
        for (var index = 0; index < ids.Count; index++)
        {
            var id = ids[index];
            var spot = desk.Find(id);
            if (spot is null)
            {
                continue;
            }

            if (spot.OpenNow)
            {
                calendar.Drop(WatchId(id));
                book.SetWatch(id, false);
                continue;
            }

            if (TryNextOpen(spot, out var open))
            {
                PutWatch(spot, open);
            }
        }
    }

    private void SyncPins()
    {
        var ids = book.Pins;
        for (var index = 0; index < ids.Count; index++)
        {
            var spot = desk.Find(ids[index]);
            if (spot is not null)
            {
                WriteHorizon(spot);
            }
        }
    }

    private bool WriteHorizon(VenueSpot spot)
    {
        var now = clock.UtcNow;
        var opens = VenueTimes.Opens(spot.Week, now, 8);
        if (opens.Count == 0 && TryNextOpen(spot, out var next))
        {
            opens = [next];
        }

        if (opens.Count == 0)
        {
            return false;
        }

        var keep = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < opens.Count; index++)
        {
            var open = opens[index];
            var id = PinId(spot.Id, open);
            keep.Add(id);
            var prior = calendar.Find(id);
            calendar.Put(new CalendarItem
            {
                Id = id,
                Kind = CalendarKind.Event,
                Title = spot.Name,
                StartsAt = open.ToLocalTime(),
                RemindMinutes = 15,
                LastFired = prior?.LastFired ?? string.Empty,
                Source = Source,
            });
        }

        calendar.KeepFuturePrefixed(PinPrefix(spot.Id), now, keep);
        return true;
    }

    private void PutWatch(VenueSpot spot, DateTimeOffset open)
    {
        var local = open.ToLocalTime();
        var lead = local - clock.Now > TimeSpan.FromMinutes(15) ? 15 : 0;
        var id = WatchId(spot.Id);
        var prior = calendar.Find(id);
        calendar.Put(new CalendarItem
        {
            Id = id,
            Kind = CalendarKind.Reminder,
            Title = spot.Name + " opens",
            StartsAt = local,
            RemindMinutes = lead,
            LastFired = prior?.LastFired ?? string.Empty,
            Source = Source,
        });
    }

    private static bool TryNextOpen(VenueSpot spot, out DateTimeOffset open)
    {
        if (spot.NextOpen is { } next && next > DateTimeOffset.UtcNow)
        {
            open = next;
            return true;
        }

        var soon = VenueTimes.Opens(spot.Week, DateTimeOffset.UtcNow, 4);
        if (soon.Count == 0)
        {
            open = default;
            return false;
        }

        open = soon[0];
        return true;
    }

    private static string WatchId(string venueId) => "vn:" + venueId;

    private static string PinPrefix(string venueId) => "vs:" + venueId + ":";

    private static string PinId(string venueId, DateTimeOffset open) =>
        "vs:" + venueId + ":" + open.ToUniversalTime().ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
}
