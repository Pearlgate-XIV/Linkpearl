using Linkpearl.Net;

namespace Linkpearl.Destinations.Settings;

public readonly record struct CreditPerson(
    string Id,
    string FallbackName,
    string Handle,
    string GateId,
    string Work,
    string Plugin = "");

public readonly record struct ShownCredit(
    string Id,
    string Name,
    string Handle,
    string Work,
    string AvatarUrl,
    string ProfileId,
    bool FromProfile);

public static class CreditBook
{
    public static readonly CreditPerson[] People =
    [
        new("kiro", "", "", "", "Linkpearl · engineering"),
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
        var name = CreditName(characterName, snapshot.MeName, person.FallbackName);
        if (IsMe(person, snapshot, characterName))
        {
            return new ShownCredit(person.Id, name, string.Empty, person.Work, snapshot.MeAvatarUrl,
                snapshot.MeId.Length > 0 ? snapshot.MeId : person.GateId, true);
        }

        for (var index = 0; index < snapshot.People.Length; index++)
        {
            var peer = snapshot.People[index];
            if (!Matches(person, peer.Id, peer.Handle, peer.DisplayName))
            {
                continue;
            }

            return new ShownCredit(person.Id, CreditName(characterName, peer.DisplayName, person.FallbackName),
                string.Empty, person.Work, peer.AvatarUrl, peer.Id, true);
        }

        for (var index = 0; index < snapshot.SearchHits.Length; index++)
        {
            var hit = snapshot.SearchHits[index];
            if (!Matches(person, hit.Id, hit.Subtitle, hit.Title))
            {
                continue;
            }

            return new ShownCredit(person.Id, CreditName(characterName, hit.Title, person.FallbackName),
                string.Empty, person.Work, string.Empty, hit.Id, true);
        }

        return new ShownCredit(person.Id, name, string.Empty, person.Work, string.Empty,
            person.GateId, false);
    }

    private static bool IsMe(CreditPerson person, PearlSnapshot snapshot, string characterName) =>
        snapshot.SignedIn &&
        (Matches(person, snapshot.MeId, snapshot.MeHandle, snapshot.MeName) ||
         person.Id == "kiro" && characterName.Trim().Length > 0);

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

    private static string CreditName(string character, string pearl, string fallback)
    {
        var inGame = character.Trim();
        if (inGame.Length > 0 && !IsHiddenAlias(inGame))
        {
            return inGame;
        }

        var live = pearl.Trim();
        if (live.Length > 0 && !IsHiddenAlias(live))
        {
            return live;
        }

        var reserve = fallback.Trim();
        return IsHiddenAlias(reserve) ? string.Empty : reserve;
    }

    private static bool IsHiddenAlias(string name)
    {
        var fold = Fold(name);
        return fold is "kiro" or "kirio";
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
