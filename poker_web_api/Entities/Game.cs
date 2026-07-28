namespace poker_web_api.Entities;

public class Game
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string GameType { get; set; } = string.Empty;
    public decimal BuyInAmount { get; set; } = 10;

    public ICollection<GameEntry> GameEntries { get; set; } = [];
}