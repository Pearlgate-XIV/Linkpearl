namespace Linkpearl.Persistence;

public interface ILedgerRecord
{
    string RecordId { get; }
}

public interface ILedger<TRecord> where TRecord : class, ILedgerRecord
{
    int Count { get; }

    event Action? Changed;

    bool TryGet(string recordId, out TRecord record);

    IReadOnlyList<TRecord> Snapshot();

    void Upsert(TRecord record);

    bool Remove(string recordId);

    void Clear();

    void Flush();
}

public interface IDepot
{
    string Root { get; }

    long BudgetBytes { get; }

    bool TryPath(string key, out string path);

    string Reserve(string key);

    void Commit(string key);

    void Evict(string key);

    void Trim();
}
