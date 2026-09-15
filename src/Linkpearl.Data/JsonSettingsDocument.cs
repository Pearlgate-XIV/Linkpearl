using System.Text.Json;
using Linkpearl.Persistence;

namespace Linkpearl.Data;

internal sealed class JsonSettingsDocument : ISettingsDocument
{
    private readonly Dictionary<string, JsonElement> fields = new(StringComparer.Ordinal);

    public static JsonSettingsDocument Parse(string json)
    {
        var document = new JsonSettingsDocument();
        using var parsed = JsonDocument.Parse(json);
        if (parsed.RootElement.ValueKind != JsonValueKind.Object)
        {
            return document;
        }

        foreach (var property in parsed.RootElement.EnumerateObject())
        {
            document.fields[property.Name] = property.Value.Clone();
        }

        return document;
    }

    public bool TryRead<TValue>(string key, out TValue value)
    {
        if (fields.TryGetValue(key, out var element))
        {
            var parsed = element.Deserialize<TValue>();
            if (parsed is not null || default(TValue) is null)
            {
                value = parsed!;
                return true;
            }
        }

        value = default!;
        return false;
    }

    public void Write<TValue>(string key, TValue value)
    {
        fields[key] = JsonSerializer.SerializeToElement(value);
    }

    public void Remove(string key) => fields.Remove(key);

    public bool Contains(string key) => fields.ContainsKey(key);

    public void Rename(string fromKey, string toKey)
    {
        if (!fields.TryGetValue(fromKey, out var element))
        {
            return;
        }

        fields.Remove(fromKey);
        fields[toKey] = element;
    }

    public string ToJson(JsonSerializerOptions options)
    {
        var buffer = new Dictionary<string, JsonElement>(fields, StringComparer.Ordinal);
        return JsonSerializer.Serialize(buffer, options);
    }
}
