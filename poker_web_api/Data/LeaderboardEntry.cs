namespace poker_web_api.Data;

public record LeaderboardEntry(string Name, int Games, decimal Result, double Rate, double Median, double RateDelta, double MedianDelta, int PositionDelta);
