using Linkpearl.Time;

namespace Linkpearl.Notices;

public interface INoticeTray
{
    void PostCalendar(string itemId, string occurrence, string title, string detail, IClock clock);

    void PostMusic(string stationId, string title, string detail, IClock clock);

    void PostVenue(string venueId, string occurrence, string title, string detail, IClock clock);
}
