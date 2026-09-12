using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Linkpearl.Net;

internal sealed class GateClient : IDisposable
{
    public const string DefaultBaseUrl = "https://pearlgate.194.113.211.29.sslip.io";

    private readonly HttpClient http;
    private string bearer = string.Empty;

    public GateClient(string baseUrl)
    {
        http = new HttpClient
        {
            BaseAddress = new Uri(TrimSlash(baseUrl) + "/"),
            Timeout = TimeSpan.FromSeconds(20),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Linkpearl/0.1.0");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void SetBearer(string? token) => bearer = token ?? string.Empty;

    public Task<(T? Body, int Status)> GetAsync<T>(string path, JsonTypeInfo<T> info, CancellationToken token) =>
        SendAsync(HttpMethod.Get, path, null, info, token);

    public Task<(T? Body, int Status)> PostAsync<T>(string path, HttpContent? content, JsonTypeInfo<T> info,
        CancellationToken token) =>
        SendAsync(HttpMethod.Post, path, content, info, token);

    public async Task<int> PostAsync(string path, HttpContent? content, CancellationToken token)
    {
        using var request = Build(HttpMethod.Post, path, content);
        using var response = await http.SendAsync(request, token).ConfigureAwait(false);
        return (int)response.StatusCode;
    }

    public async Task<int> PutAsync(string path, HttpContent? content, CancellationToken token)
    {
        using var request = Build(HttpMethod.Put, path, content);
        using var response = await http.SendAsync(request, token).ConfigureAwait(false);
        return (int)response.StatusCode;
    }

    public async Task<int> DeleteAsync(string path, CancellationToken token)
    {
        using var request = Build(HttpMethod.Delete, path, null);
        using var response = await http.SendAsync(request, token).ConfigureAwait(false);
        return (int)response.StatusCode;
    }

    public async Task<(byte[]? Body, int Status)> GetBytesAsync(string path, CancellationToken token)
    {
        using var request = Build(HttpMethod.Get, path, null);
        using var response = await http.SendAsync(request, token).ConfigureAwait(false);
        var status = (int)response.StatusCode;
        if (status < 200 || status >= 300)
        {
            return (null, status);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
        return (bytes, status);
    }

    public Task<(T? Body, int Status)> PostFileAsync<T>(string path, string field, string fileName, byte[] bytes,
        string contentType, JsonTypeInfo<T> info, CancellationToken token)
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { part, field, fileName } };
        return SendAsync(HttpMethod.Post, path, form, info, token);
    }

    public static StringContent JsonBody<T>(T value, JsonTypeInfo<T> info) =>
        new(JsonSerializer.Serialize(value, info), Encoding.UTF8, "application/json");

    public void Dispose() => http.Dispose();

    private async Task<(T? Body, int Status)> SendAsync<T>(HttpMethod method, string path, HttpContent? content,
        JsonTypeInfo<T> info, CancellationToken token)
    {
        using var request = Build(method, path, content);
        using var response = await http.SendAsync(request, token).ConfigureAwait(false);
        var status = (int)response.StatusCode;
        if (status < 200 || status >= 300)
        {
            return (default, status);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        var body = await JsonSerializer.DeserializeAsync(stream, info, token).ConfigureAwait(false);
        return (body, status);
    }

    private HttpRequestMessage Build(HttpMethod method, string path, HttpContent? content)
    {
        var request = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new HttpRequestMessage(method, new Uri(path, UriKind.Absolute))
            : new HttpRequestMessage(method, path.TrimStart('/'));
        if (content is not null)
        {
            request.Content = content;
        }

        if (bearer.Length > 0)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return request;
    }

    private static string TrimSlash(string url) => url.TrimEnd('/');
}
