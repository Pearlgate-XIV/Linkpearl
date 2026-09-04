namespace Linkpearl.Audio;

public readonly record struct PublicStation(
    string Id,
    string Title,
    string Genre,
    string Place,
    string StreamUrl,
    int Bitrate,
    string ArtUrl = "",
    string AlternateUrl = "");

public interface IPublicRadio
{
    IReadOnlyList<string> Genres { get; }

    bool Busy { get; }

    string Notice { get; }

    IReadOnlyList<PublicStation> Stations(string genre);

    void Ensure(string genre);

    IReadOnlyList<string> PlayUrls(PublicStation station);
}
