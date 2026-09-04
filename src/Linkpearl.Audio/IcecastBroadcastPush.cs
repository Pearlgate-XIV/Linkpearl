using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using NAudio.Lame;
using NAudio.Wave;

namespace Linkpearl.Audio;

// Encodes the phone's captured PCM and pushes MP3 to an Icecast SOURCE,
// the same job BUTT does on the desktop.
public sealed class IcecastBroadcastPush : IBroadcastPush
{
    private readonly object gate = new();
    private TcpClient? client;
    private Stream? wire;
    private LameMP3FileWriter? lame;
    private int sampleRate;
    private string ingest = string.Empty;
    private string stationName = string.Empty;
    private string genre = string.Empty;
    private bool sending;
    private int generation;
    private string notice = "Not sending to Icecast yet.";

    public bool Sending
    {
        get
        {
            lock (gate)
            {
                return sending;
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

    public void Start(string ingestUrl, string stationName, string genre)
    {
        lock (gate)
        {
            if (sending &&
                string.Equals(ingest, ingestUrl, StringComparison.Ordinal) &&
                string.Equals(this.stationName, stationName, StringComparison.Ordinal) &&
                string.Equals(this.genre, genre, StringComparison.Ordinal))
            {
                return;
            }
        }

        Stop();
        int ticket;
        lock (gate)
        {
            ingest = ingestUrl.Trim();
            this.stationName = stationName.Trim();
            this.genre = genre.Trim();
            sending = ingest.Length > 0;
            ticket = ++generation;
            notice = sending ? "Connecting to Icecast…" : "No ingest URL. Save a host or wait for Pearlgate.";
        }

        if (ingest.Length == 0)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                Open(ticket);
            }
            catch (Exception error)
            {
                lock (gate)
                {
                    if (ticket != generation)
                    {
                        return;
                    }

                    sending = false;
                    notice = "Could not reach Icecast" +
                             (error.Message.Length > 0 ? " (" + error.Message + ")" : ".");
                }

                CloseQuiet();
            }
        });
    }

    public void Stop()
    {
        lock (gate)
        {
            generation++;
            sending = false;
            notice = "Off the Icecast mount.";
            sampleRate = 0;
        }

        CloseQuiet();
    }

    public void WritePcm(byte[] pcm16Stereo, int bytes, int rate)
    {
        if (pcm16Stereo.Length == 0 || bytes <= 0 || rate <= 0)
        {
            return;
        }

        LameMP3FileWriter? encoder;
        lock (gate)
        {
            if (!sending || wire is null)
            {
                return;
            }

            if (lame is null || sampleRate != rate)
            {
                try
                {
                    lame?.Dispose();
                    lame = new LameMP3FileWriter(wire, new WaveFormat(rate, 16, 2), 128);
                    sampleRate = rate;
                    notice = "Sending MP3 to Icecast.";
                }
                catch (Exception error)
                {
                    sending = false;
                    notice = "Could not encode for Icecast" +
                             (error.Message.Length > 0 ? " (" + error.Message + ")" : ".");
                    return;
                }
            }

            encoder = lame;
        }

        try
        {
            encoder.Write(pcm16Stereo, 0, bytes);
        }
        catch (Exception error)
        {
            lock (gate)
            {
                sending = false;
                notice = "Icecast dropped the source" +
                         (error.Message.Length > 0 ? " (" + error.Message + ")" : ".");
            }

            CloseQuiet();
        }
    }

    public void Dispose() => Stop();

    private void Open(int ticket)
    {
        Uri uri;
        lock (gate)
        {
            if (ticket != generation)
            {
                return;
            }

            uri = new Uri(ingest);
        }

        var port = uri.IsDefaultPort ? (uri.Scheme == Uri.UriSchemeHttps ? 443 : 8000) : uri.Port;
        var tcp = new TcpClient { NoDelay = true };
        tcp.Connect(uri.Host, port);
        Stream stream = tcp.GetStream();
        stream.ReadTimeout = 8000;
        stream.WriteTimeout = 8000;
        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            var tls = new SslStream(stream, false);
            tls.AuthenticateAsClient(uri.Host);
            stream = tls;
        }

        var user = "source";
        var password = string.Empty;
        if (uri.UserInfo.Length > 0)
        {
            var parts = uri.UserInfo.Split(':', 2);
            user = Uri.UnescapeDataString(parts[0]);
            password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        }

        var mount = uri.AbsolutePath;
        if (mount.Length == 0 || mount == "/")
        {
            mount = "/live";
        }

        string name;
        string kind;
        lock (gate)
        {
            name = stationName.Length > 0 ? stationName : "Linkpearl";
            kind = genre.Length > 0 ? genre : "live";
        }

        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes(user + ":" + password));
        var handshake =
            "SOURCE " + mount + " HTTP/1.0\r\n" +
            "Host: " + uri.Host + ":" + port + "\r\n" +
            "Authorization: Basic " + token + "\r\n" +
            "User-Agent: Linkpearl/0.1\r\n" +
            "Content-Type: audio/mpeg\r\n" +
            "Ice-Name: " + SanitizeHeader(name) + "\r\n" +
            "Ice-Genre: " + SanitizeHeader(kind) + "\r\n" +
            "Ice-Public: 1\r\n" +
            "Ice-Audio-Info: ice-samplerate=48000;ice-bitrate=128;ice-channels=2\r\n" +
            "\r\n";
        var bytes = Encoding.ASCII.GetBytes(handshake);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();

        var reply = ReadHeaders(stream);
        if (reply.IndexOf(" 200", StringComparison.Ordinal) < 0 &&
            reply.IndexOf("200 OK", StringComparison.OrdinalIgnoreCase) < 0)
        {
            tcp.Dispose();
            throw new IOException(reply.Length > 0 ? FirstLine(reply) : "Icecast refused SOURCE.");
        }

        lock (gate)
        {
            if (ticket != generation)
            {
                tcp.Dispose();
                return;
            }

            client = tcp;
            wire = stream;
            notice = "Icecast accepted SOURCE. Waiting for audio.";
        }
    }

    private void CloseQuiet()
    {
        LameMP3FileWriter? encoder;
        Stream? stream;
        TcpClient? tcp;
        lock (gate)
        {
            encoder = lame;
            stream = wire;
            tcp = client;
            lame = null;
            wire = null;
            client = null;
        }

        try
        {
            encoder?.Dispose();
        }
        catch (Exception)
        {
        }

        try
        {
            stream?.Dispose();
        }
        catch (Exception)
        {
        }

        try
        {
            tcp?.Dispose();
        }
        catch (Exception)
        {
        }
    }

    private static string ReadHeaders(Stream stream)
    {
        var buffer = new byte[16];
        var text = new StringBuilder();
        while (text.Length < 2048)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                break;
            }

            text.Append(Encoding.ASCII.GetString(buffer, 0, read));
            if (text.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                break;
            }
        }

        return text.ToString();
    }

    private static string FirstLine(string reply)
    {
        var end = reply.IndexOf('\r');
        return end > 0 ? reply[..end] : reply.Trim();
    }

    private static string SanitizeHeader(string value)
    {
        var trimmed = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return trimmed.Length > 64 ? trimmed[..64] : trimmed;
    }
}
