
namespace Linkpearl.Audio;

internal static class HttpWire
{
    public static HttpResponseMessage Get(HttpClient http, string url, TimeSpan wait)
    {
        using var timeout = new CancellationTokenSource(wait);
        return http.SendAsync(new HttpRequestMessage(HttpMethod.Get, url),
                HttpCompletionOption.ResponseHeadersRead, timeout.Token)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    public static Stream Body(HttpResponseMessage response) =>
        response.Content.ReadAsStreamAsync().ConfigureAwait(false).GetAwaiter().GetResult();
}
