namespace poker_web_api.Entities;

public class Player
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<GameEntry> GameEntries { get; set; } = [];
}