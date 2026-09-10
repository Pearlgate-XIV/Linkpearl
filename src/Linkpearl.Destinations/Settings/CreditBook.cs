using Linkpearl.Net;

namespace Linkpearl.Destinations.Settings;

public enum CreditKind : byte
{
    Person = 0,
    Plugin = 1,
}

public readonly record struct CreditPerson(
    string Id,
    string FallbackName,
    string Handle,
    string GateId,
    string Work,
    CreditKind Kind = CreditKind.Person,
    string PluginPage = "",
    string GitHubPage = "",
    string IconUrl = "",
    string IconAsset = "");

public readonly record struct ShownCredit(
    string Id,
    string Name,
    string Handle,
    string Work,
    string AvatarUrl,
    string ProfileId,
    bool FromProfile,
    CreditKind Kind,
    string PluginPage = "",
    string GitHubPage = "",
    string IconAsset = "");

public static class CreditBook
{
    public static readonly CreditPerson[] People =
    [
        new("alyx", "A'lyx Nightingale", "", "", "UI/UX design & Development"),
        new("sibyl", "Sibyl Cenotaph", "", "", "Moderation | Management | Concept development"),
        new("roxanne", "Roxanne Delyre", "", "", "System & Security Development | Concept Development"),
        new("lucia", "Lucia Mae", "", "", "Moderation"),
        new("aetheros", "AetherOS", "", "", "General Contribution | Inspiration", CreditKind.Plugin,
            "https://puni.sh/directory/aetherlove",
            "https://github.com/FFXIV-Aetherlove/Aetherlove",
            "https://puni.sh/api/plugins/icon/154",
            "Icons/credits/aetheros.png"),
        new("echomix", "Echomix", "", "", "General Contribution | Inspiration", CreditKind.Plugin,
            "https://echomix.app",
            "https://github.com/jfraygit/EchoXIV/tree/main/EchoMix",
            "https://raw.githubusercontent.com/jfraygit/EchoXIV/main/icons/echomix-icon.png",
            "Icons/credits/echomix.png"),
    ];

    public static ShownCredit[] Resolve(PearlSnapshot snapshot, string characterName = "")
    {
        var shown = new List<ShownCredit>(People.Length);
        for (var index = 0; index < People.Length; index++)
        {
            var credit = Resolve(People[index], snapshot, characterName);
            if (credit.Name.Length > 0)
            {
                shown.Add(credit);
            }
        }

        return shown.ToArray();
    }

    public static ShownCredit Resolve(CreditPerson person, PearlSnapshot snapshot, string characterName = "")
    {
        if (person.Kind == CreditKind.Plugin)
        {
            return Show(person, person.FallbackName, person.Handle, person.IconUrl, string.Empty, false);
        }

        if (IsMe(person, snapshot, characterName))
        {
            var mine = LiveName(characterName, snapshot.MeName, person.FallbackName);
            return Show(person, mine, string.Empty, snapshot.MeAvatarUrl,
                snapshot.MeId.Length > 0 ? snapshot.MeId : person.GateId, true);
        }

        for (var index = 0; index < snapshot.People.Length; index++)
        {
            var peer = snapshot.People[index];
            if (!Matches(person, peer.Id, peer.Handle, peer.DisplayName))
            {
                continue;
            }

            return Show(person, Prefer(peer.DisplayName, person.FallbackName), peer.Handle, peer.AvatarUrl, peer.Id,
                true);
        }

        for (var index = 0; index < snapshot.SearchHits.Length; index++)
        {
            var hit = snapshot.SearchHits[index];
            if (!Matches(person, hit.Id, hit.Subtitle, hit.Title))
            {
                continue;
            }

            return Show(person, Prefer(hit.Title, person.FallbackName), string.Empty, string.Empty, hit.Id, true);
        }

        return Show(person, person.FallbackName, person.Handle, string.Empty, person.GateId, false);
    }

    private static ShownCredit Show(CreditPerson person, string name, string handle, string avatar, string profileId,
        bool fromProfile) =>
        new(person.Id, name, handle, person.Work, avatar, profileId, fromProfile, person.Kind, person.PluginPage,
            person.GitHubPage, person.IconAsset);

    private static bool IsMe(CreditPerson person, PearlSnapshot snapshot, string characterName) =>
        snapshot.SignedIn &&
        (Matches(person, snapshot.MeId, snapshot.MeHandle, snapshot.MeName) ||
         Matches(person, snapshot.MeId, snapshot.MeHandle, characterName));

    private static bool Matches(CreditPerson person, string id, string handle, string name) =>
        person.GateId.Length > 0 && SameId(person.GateId, id) ||
        person.Handle.Length > 0 && SameHandle(person.Handle, handle) ||
        SameGlyph(person.FallbackName, name);

    private static bool SameId(string left, string right) =>
        left.Length > 0 && right.Length > 0 &&
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool SameHandle(string left, string right)
    {
        var a = StripAt(left);
        var b = StripAt(right);
        return a.Length > 0 && b.Length > 0 && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameGlyph(string left, string right)
    {
        var a = Fold(left);
        var b = Fold(right);
        return a.Length > 0 && b.Length > 0 && (a == b || a.Contains(b, StringComparison.Ordinal) ||
                                               b.Contains(a, StringComparison.Ordinal));
    }

    private static string Prefer(string live, string fallback)
    {
        var shown = live.Trim();
        return shown.Length > 0 ? shown : fallback.Trim();
    }

    private static string LiveName(string character, string pearl, string fallback)
    {
        var inGame = character.Trim();
        return inGame.Length > 0 ? inGame : Prefer(pearl, fallback);
    }

    private static string StripAt(string value)
    {
        var text = value.Trim();
        return text.StartsWith('@') ? text[1..] : text;
    }

    private static string Fold(string value)
    {
        var builder = new char[value.Length];
        var count = 0;
        foreach (var glyph in value)
        {
            if (char.IsLetterOrDigit(glyph))
            {
                builder[count++] = char.ToLowerInvariant(glyph);
            }
        }

        return new string(builder, 0, count);
    }
}
