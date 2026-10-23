namespace poker_web_api.Entities;

public enum EntryType
{
    BuyIn,
    CashOut
}

public class GameEntry
{
    public int Id { get; set; }

    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public EntryType Type { get; set; }
    public decimal Amount { get; set; }
    public string? PhotoUrl { get; set; }
}