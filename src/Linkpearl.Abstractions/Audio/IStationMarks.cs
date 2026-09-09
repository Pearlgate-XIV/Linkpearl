namespace Linkpearl.Audio;

public interface IStationMarks
{
    bool Liked(string id);

    int LikeCount(HandsetTune now);

    void ToggleLike(HandsetTune now);

    bool Saved(string id);

    void ToggleSave(HandsetTune now);

    bool Followed(string id);

    void ToggleFollow(HandsetTune now);

    bool CanFollow(HandsetTune now) => now.Live && (now.Id ?? string.Empty).Length > 0;
}
