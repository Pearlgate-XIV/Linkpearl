namespace Linkpearl.Net;

public sealed class StreamDesk : IStreamDesk
{
    private readonly IStreamDesk accounts;
    private readonly RolladeckDesk rolla;

    public StreamDesk(IStreamDesk accounts, RolladeckDesk rolla)
    {
        this.accounts = accounts;
        this.rolla = rolla;
    }

    public IReadOnlyList<StreamInfo> Live => MergeLive(accounts.Live, rolla.Live);

    public IReadOnlyList<StreamScheduleMark> CommunitySchedule => rolla.Schedule;

    public StreamProviderAccount Own => accounts.Own;

    public string Notice
    {
        get
        {
            var mine = accounts.Notice.Trim();
            var community = rolla.Notice.Trim();
            if (mine.Length == 0)
            {
                return community;
            }

            if (community.Length == 0 ||
                mine.Contains(community, StringComparison.OrdinalIgnoreCase))
            {
                return mine;
            }

            return mine + " " + community;
        }
    }

    public bool Busy => accounts.Busy || rolla.Busy;

    public void Refresh()
    {
        accounts.Refresh();
        rolla.Refresh();
    }

    public void RequestConnect() => accounts.RequestConnect();

    public void Disconnect() => accounts.Disconnect();

    public StreamInfo? Find(string idOrLogin)
    {
        return accounts.Find(idOrLogin) ?? FindIn(rolla.Live, idOrLogin);
    }

    public void Dispose()
    {
        accounts.Dispose();
        rolla.Dispose();
    }

    private static StreamInfo[] MergeLive(IReadOnlyList<StreamInfo> left, IReadOnlyList<StreamInfo> right)
    {
        var byLogin = new Dictionary<string, StreamInfo>(StringComparer.OrdinalIgnoreCase);
        void Put(StreamInfo row)
        {
            var key = StreamChrome.LoginOf(row.Username);
            if (key.Length == 0)
            {
                return;
            }

            if (byLogin.TryGetValue(key, out var prior))
            {
                if (string.Equals(prior.Source, "pearlgate", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(row.Source, "pearlgate", StringComparison.OrdinalIgnoreCase))
                {
                    byLogin[key] = prior with
                    {
                        Viewers = Math.Max(prior.Viewers, row.Viewers),
                        VenueLine = prior.VenueLine.Length > 0 ? prior.VenueLine : row.VenueLine,
                        Lifestream = prior.Lifestream.Length > 0 ? prior.Lifestream : row.Lifestream,
                        ArtUrl = prior.ArtUrl.Length > 0 ? prior.ArtUrl : row.ArtUrl,
                        Genre = prior.Genre.Length > 0 ? prior.Genre : row.Genre,
                        Bio = prior.Bio.Length > 0 ? prior.Bio : row.Bio,
                    };
                    return;
                }
            }

            byLogin[key] = row;
        }

        for (var index = 0; index < left.Count; index++)
        {
            Put(left[index]);
        }

        for (var index = 0; index < right.Count; index++)
        {
            Put(right[index]);
        }

        return byLogin.Values
            .Where(static row => row.Status == StreamStatus.Live)
            .OrderByDescending(static row => row.Viewers)
            .ThenBy(static row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static StreamInfo? FindIn(IReadOnlyList<StreamInfo> rows, string idOrLogin)
    {
        var key = StreamChrome.LoginOf(idOrLogin);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (string.Equals(row.Id, idOrLogin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.Username, key, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return null;
    }
}
