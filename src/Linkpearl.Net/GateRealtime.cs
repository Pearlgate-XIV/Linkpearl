using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Linkpearl.Diagnostics;

namespace Linkpearl.Net;

internal sealed class GateRealtime : IDisposable
{
    public const string Path = "/rt";

    private static readonly TimeSpan PingEvery = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    private readonly GateClient client;
    private readonly ILinkpearlLog log;
    private readonly Action<GateRealtimeDto> onEvent;
    private readonly Action onUnauthorized;
    private readonly object gate = new();
    private CancellationTokenSource? run;
    private bool disposed;

    public GateRealtime(GateClient client, ILinkpearlLog log, Action<GateRealtimeDto> onEvent, Action onUnauthorized)
    {
        this.client = client;
        this.log = log;
        this.onEvent = onEvent;
        this.onUnauthorized = onUnauthorized;
    }

    public void SyncSession()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            if (client.Bearer.Length == 0)
            {
                CancelLocked();
                return;
            }

            CancelLocked();
            run = new CancellationTokenSource();
            var token = run.Token;
            _ = Task.Run(() => ListenAsync(token), token);
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            CancelLocked();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            CancelLocked();
        }
    }

    private void CancelLocked()
    {
        if (run is null)
        {
            return;
        }

        try
        {
            run.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        run.Dispose();
        run = null;
    }

    private async Task ListenAsync(CancellationToken token)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (!token.IsCancellationRequested)
        {
            try
            {
                await ConnectOnceAsync(token).ConfigureAwait(false);
                delay = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception failure) when (IsUnauthorized(failure))
            {
                log.Write(LogSeverity.Warning, "Pearlgate realtime refused the session.");
                onUnauthorized();
                return;
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                log.Write(LogSeverity.Warning, failure, "Pearlgate realtime dropped");
            }

            if (token.IsCancellationRequested || client.Bearer.Length == 0)
            {
                return;
            }

            try
            {
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            delay = delay >= MaxBackoff ? MaxBackoff : TimeSpan.FromSeconds(Math.Min(MaxBackoff.TotalSeconds, delay.TotalSeconds * 2));
        }
    }

    private async Task ConnectOnceAsync(CancellationToken token)
    {
        var bearer = client.Bearer;
        if (bearer.Length == 0)
        {
            return;
        }

        using var socket = new ClientWebSocket();
        socket.Options.HttpVersion = HttpVersion.Version11;
        socket.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        socket.Options.SetRequestHeader("Authorization", "Bearer " + bearer);
        socket.Options.SetRequestHeader("User-Agent", "Linkpearl/0.1.0");

        await socket.ConnectAsync(SocketUri(client.BaseAddress, bearer), token).ConfigureAwait(false);
        using var ping = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, ping.Token);
        var pump = PumpAsync(socket, linked.Token);
        try
        {
            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var payload = JsonSerializer.SerializeToUtf8Bytes(new RealtimePingDto("chat.ping"),
                    GateJson.Default.RealtimePingDto);
                await socket.SendAsync(payload, WebSocketMessageType.Text, true, token).ConfigureAwait(false);
                await Task.Delay(PingEvery, token).ConfigureAwait(false);
            }
        }
        finally
        {
            ping.Cancel();
            try
            {
                await pump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    private async Task PumpAsync(ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[16 * 1024];
        var message = new MemoryStream();
        while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            var slice = new ArraySegment<byte>(buffer);
            var result = await socket.ReceiveAsync(slice, token).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
            {
                continue;
            }

            var json = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            message.SetLength(0);
            GateRealtimeDto? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize(json, GateJson.Default.GateRealtimeDto);
            }
            catch (JsonException failure)
            {
                log.Write(LogSeverity.Warning, failure, "Pearlgate realtime frame was not JSON");
                continue;
            }

            if (parsed is not null)
            {
                onEvent(parsed);
            }
        }
    }

    internal static Uri SocketUri(Uri restBase, string bearer)
    {
        var builder = new UriBuilder(restBase)
        {
            Scheme = string.Equals(restBase.Scheme, "http", StringComparison.OrdinalIgnoreCase) ? "ws" : "wss",
            Path = TrimSlash(restBase.AbsolutePath) + Path,
            Query = "access_token=" + Uri.EscapeDataString(bearer),
        };
        return builder.Uri;
    }

    private static bool IsUnauthorized(Exception failure)
    {
        for (var inner = failure; inner is not null; inner = inner.InnerException)
        {
            if (inner.Message.Contains("401", StringComparison.Ordinal) ||
                inner.Message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string TrimSlash(string path) => path.TrimEnd('/');
}
