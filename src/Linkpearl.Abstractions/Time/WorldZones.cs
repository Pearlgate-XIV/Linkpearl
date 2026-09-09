namespace Linkpearl.Time;

public readonly record struct WorldZone(string Id, string City, string Place);

public static class WorldZones
{
    public const string Auto = "";
    public const string Eorzea = "eorzea";

    public static readonly WorldZone[] Catalog =
    {
        new(Eorzea, "Eorzea", "The Source"),
        new("UTC", "UTC", "Everywhere"),
        new("Hawaiian Standard Time", "Honolulu", "United States"),
        new("Alaskan Standard Time", "Anchorage", "United States"),
        new("Pacific Standard Time", "Los Angeles", "United States"),
        new("US Mountain Standard Time", "Phoenix", "United States"),
        new("Mountain Standard Time", "Denver", "United States"),
        new("Central Standard Time", "Chicago", "United States"),
        new("Eastern Standard Time", "New York", "United States"),
        new("GMT Standard Time", "London", "United Kingdom"),
        new("Romance Standard Time", "Paris", "France"),
        new("W. Europe Standard Time", "Berlin", "Germany"),
        new("Russian Standard Time", "Moscow", "Russia"),
        new("India Standard Time", "Mumbai", "India"),
        new("China Standard Time", "Shanghai", "China"),
        new("Tokyo Standard Time", "Tokyo", "Japan"),
        new("Korea Standard Time", "Seoul", "Korea"),
        new("Singapore Standard Time", "Singapore", "Singapore"),
        new("AUS Eastern Standard Time", "Sydney", "Australia"),
        new("New Zealand Standard Time", "Auckland", "New Zealand"),
    };

    public static bool IsEorzea(string? id) =>
        string.Equals(id, Eorzea, StringComparison.OrdinalIgnoreCase);

    public static WorldZone? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        for (var index = 0; index < Catalog.Length; index++)
        {
            if (string.Equals(Catalog[index].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return Catalog[index];
            }
        }

        return null;
    }

    public static string Sanitize(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Auto;
        }

        var trimmed = id.Trim();
        if (IsEorzea(trimmed))
        {
            return Eorzea;
        }

        var known = Find(trimmed);
        if (known is not null)
        {
            return known.Value.Id;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(trimmed).Id;
        }
        catch (TimeZoneNotFoundException)
        {
            return Auto;
        }
        catch (InvalidTimeZoneException)
        {
            return Auto;
        }
    }

    public static string CityOf(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            var local = TimeZoneInfo.Local.Id;
            return Find(local)?.City ?? "Local";
        }

        if (IsEorzea(id))
        {
            return "Eorzea";
        }

        var known = Find(id);
        if (known is not null)
        {
            return known.Value.City;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id).Id;
        }
        catch (TimeZoneNotFoundException)
        {
            return id;
        }
        catch (InvalidTimeZoneException)
        {
            return id;
        }
    }

    public static string PickFor(string key)
    {
        const int skip = 2;
        var span = Catalog.Length - skip;
        if (span <= 0)
        {
            return Catalog[0].Id;
        }

        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in key)
            {
                hash = (hash ^ ch) * 16777619;
            }

            return Catalog[skip + (int)(hash % (uint)span)].Id;
        }
    }

    public static string ForPerson(string? stored, string? key)
    {
        var clean = Sanitize(stored);
        if (clean.Length > 0)
        {
            return clean;
        }

        if (key is { Length: > 0 } &&
            (key.StartsWith("demo:", StringComparison.OrdinalIgnoreCase) ||
             key.StartsWith("live:", StringComparison.OrdinalIgnoreCase)))
        {
            return PickFor(key);
        }

        return Auto;
    }
}
