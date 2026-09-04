using Linkpearl.Net;
using Linkpearl.Talk;

namespace Linkpearl.Destinations;

public readonly record struct SearchResult(
    string Kind,
    string Title,
    string Subtitle,
    string TalkId = "",
    string ProfileId = "");

public static class DirectorySearch
{
    public static int Fill(PearlSnapshot snapshot, string query, SearchResult[] dest, ITalk? talk = null)
    {
        if (dest.Length == 0)
        {
            return 0;
        }

        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return CopyRecents(snapshot, dest, talk);
        }

        var cursor = 0;
        cursor = AppendTalk(talk, trimmed, dest, cursor);
        cursor = AppendChats(snapshot.Chats, trimmed, dest, cursor);
        cursor = AppendPeople(snapshot.People, trimmed, dest, cursor);
        if (trimmed.Length >= 2)
        {
            cursor = AppendHits(snapshot.SearchHits, dest, cursor);
        }

        return cursor;
    }

    private static int CopyRecents(PearlSnapshot snapshot, SearchResult[] dest, ITalk? talk)
    {
        var cursor = 0;
        if (talk is not null)
        {
            var inbox = talk.Inbox();
            for (var index = 0; index < inbox.Count && cursor < dest.Length; index++)
            {
                var thread = inbox[index];
                dest[cursor] = new SearchResult("Talk", thread.Title, thread.Preview, thread.Id);
                cursor++;
            }
        }

        var chats = snapshot.Chats;
        for (var index = 0; index < chats.Length && cursor < dest.Length; index++)
        {
            dest[cursor] = new SearchResult("Chat", chats[index].Title, chats[index].Preview,
                TalkIds.Pearl(chats[index].Id));
            cursor++;
        }

        var people = snapshot.People;
        for (var index = 0; index < people.Length && cursor < dest.Length; index++)
        {
            var person = people[index];
            dest[cursor] = new SearchResult("Player", person.DisplayName, person.Handle, string.Empty,
                TalkIds.Person(person.Id));
            cursor++;
        }

        return cursor;
    }

    private static int AppendTalk(ITalk? talk, string query, SearchResult[] dest, int cursor)
    {
        if (talk is null)
        {
            return cursor;
        }

        var inbox = talk.Inbox();
        for (var index = 0; index < inbox.Count && cursor < dest.Length; index++)
        {
            var thread = inbox[index];
            if (!Contains(thread.Title, query) && !Contains(thread.Preview, query) && !Contains(thread.Subtitle, query))
            {
                continue;
            }

            dest[cursor] = new SearchResult("Talk", thread.Title, thread.Preview, thread.Id);
            cursor++;
        }

        var peers = talk.Peers();
        for (var index = 0; index < peers.Count && cursor < dest.Length; index++)
        {
            var peer = peers[index];
            if (!Contains(peer.Name, query) && !Contains(peer.World, query) && !Contains(peer.Number, query))
            {
                continue;
            }

            dest[cursor] = new SearchResult("Player", peer.Name, peer.World, peer.TellId, peer.Id);
            cursor++;
        }

        return cursor;
    }

    private static int AppendChats(PearlChat[] chats, string query, SearchResult[] dest, int cursor)
    {
        for (var index = 0; index < chats.Length && cursor < dest.Length; index++)
        {
            var chat = chats[index];
            if (!Contains(chat.Title, query) && !Contains(chat.Preview, query))
            {
                continue;
            }

            dest[cursor] = new SearchResult("Chat", chat.Title, chat.Preview, TalkIds.Pearl(chat.Id));
            cursor++;
        }

        return cursor;
    }

    private static int AppendPeople(PearlPerson[] people, string query, SearchResult[] dest, int cursor)
    {
        for (var index = 0; index < people.Length && cursor < dest.Length; index++)
        {
            var person = people[index];
            if (!Contains(person.DisplayName, query) && !Contains(person.Handle, query))
            {
                continue;
            }

            dest[cursor] = new SearchResult("Player", person.DisplayName, person.Handle, string.Empty,
                TalkIds.Person(person.Id));
            cursor++;
        }

        return cursor;
    }

    private static int AppendHits(PearlHit[] hits, SearchResult[] dest, int cursor)
    {
        for (var index = 0; index < hits.Length && cursor < dest.Length; index++)
        {
            var hit = hits[index];
            if (AlreadyListed(dest, cursor, hit.Title))
            {
                continue;
            }

            dest[cursor] = new SearchResult(hit.Kind, hit.Title, hit.Subtitle, string.Empty,
                hit.Id.Length == 0 ? string.Empty : TalkIds.Person(hit.Id));
            cursor++;
        }

        return cursor;
    }

    private static bool AlreadyListed(SearchResult[] dest, int count, string title)
    {
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(dest[index].Title, title, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
