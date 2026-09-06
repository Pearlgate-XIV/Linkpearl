namespace Linkpearl.Platform;

public readonly record struct GameMarketItem(int Id, string Name, uint IconId);

public interface IGameItems
{
    IReadOnlyList<GameMarketItem> Search(string query, int limit = 40);

    GameMarketItem? Find(int id);
}
