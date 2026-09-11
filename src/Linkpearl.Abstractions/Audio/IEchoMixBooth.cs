namespace Linkpearl.Audio;

public interface IEchoMixBooth
{
    bool IsOpen { get; }

    bool Mixing { get; }

    void Open();

    void Close();
}

public interface IEchoMixStation
{
    string StationTitle { get; }

    string DjName { get; }

    string Genre { get; }

    bool Broadcasting { get; }

    string ListenUrl { get; }

    string Notice { get; }

    void Prepare();

    void GoLive();

    void EndLive();
}
