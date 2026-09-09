using Linkpearl.Time;

namespace Linkpearl.Notices;

public interface INoticeTray
{
    void PostCalendar(string itemId, string title, string detail, IClock clock);

    void PostMusic(string stationId, string title, string detail, IClock clock);
}
