using Dalamud.Plugin.Services;
using ItemSheet = Lumina.Excel.Sheets.Item;

namespace Linkpearl.Platform.Ffxiv;

public sealed class FfxivGameItems : IGameItems
{
    private readonly GameMarketItem[] all;

    public FfxivGameItems(IDataManager data)
    {
        var rows = new List<GameMarketItem>();
        foreach (var item in data.GetExcelSheet<ItemSheet>())
        {
            if (item.RowId == 0 || item.ItemSearchCategory.RowId == 0)
            {
                continue;
            }

            var name = item.Name.ExtractText() ?? string.Empty;
            if (name.Length == 0)
            {
                continue;
            }

            rows.Add(new GameMarketItem((int)item.RowId, name, item.Icon));
        }

        all = rows.ToArray();
    }

    public IReadOnlyList<GameMarketItem> Search(string query, int limit = 40)
    {
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0 || limit <= 0)
        {
            return [];
        }

        if (int.TryParse(text, out var id) && id > 0)
        {
            var exact = Find(id);
            return exact is null ? [] : [exact.Value];
        }

        var cap = Math.Clamp(limit, 1, 80);
        var starts = new List<GameMarketItem>();
        var inside = new List<GameMarketItem>();
        for (var index = 0; index < all.Length && starts.Count + inside.Count < cap * 2; index++)
        {
            var item = all[index];
            var at = item.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                continue;
            }

            if (at == 0)
            {
                starts.Add(item);
            }
            else
            {
                inside.Add(item);
            }
        }

        if (starts.Count >= cap)
        {
            return starts.GetRange(0, cap);
        }

        var take = Math.Min(inside.Count, cap - starts.Count);
        if (take == 0)
        {
            return starts;
        }

        var merged = new GameMarketItem[starts.Count + take];
        starts.CopyTo(merged);
        inside.CopyTo(0, merged, starts.Count, take);
        return merged;
    }

    public GameMarketItem? Find(int id)
    {
        if (id <= 0)
        {
            return null;
        }

        for (var index = 0; index < all.Length; index++)
        {
            if (all[index].Id == id)
            {
                return all[index];
            }
        }

        return null;
    }
}
