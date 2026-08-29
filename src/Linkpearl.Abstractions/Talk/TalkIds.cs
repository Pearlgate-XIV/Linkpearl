using System.Globalization;

namespace Linkpearl.Talk;

public static class TalkIds
{
    public const string Party = "party";
    public const string Alliance = "alliance";
    public const string FreeCompany = "fc";
    public const string Novice = "novice";

    public static string Linkshell(int slot) => "ls:" + slot.ToString(CultureInfo.InvariantCulture);

    public static string CrossWorld(int slot) => "cwls:" + slot.ToString(CultureInfo.InvariantCulture);

    public static string Pearl(string id) => "pearl:" + id;

    public static string Tell(string name, string world)
    {
        var trimmedName = name.Trim();
        var trimmedWorld = world.Trim();
        return trimmedWorld.Length == 0 ? "tell:" + trimmedName : "tell:" + trimmedName + "@" + trimmedWorld;
    }

    public static string Person(string userId) => "person:" + userId.Trim();

    public static bool TryParsePerson(string id, out string userId)
    {
        userId = string.Empty;
        const string prefix = "person:";
        if (!id.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        userId = id[prefix.Length..];
        return userId.Length > 0;
    }

    public static bool TryParseTell(string threadId, out string name, out string world)
    {
        name = string.Empty;
        world = string.Empty;
        const string prefix = "tell:";
        if (!threadId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = threadId[prefix.Length..];
        var at = rest.LastIndexOf('@');
        if (at < 0)
        {
            name = rest;
            return name.Length > 0;
        }

        name = rest[..at];
        world = rest[(at + 1)..];
        return name.Length > 0;
    }

    public static bool TryParseSlot(string threadId, string prefix, out int slot)
    {
        slot = 0;
        if (!threadId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(threadId[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out slot) &&
            slot is >= 1 and <= 8;
    }
}