namespace poker_web_api.Data;

public record LeaderboardsResponse(IReadOnlyList<LeaderboardEntry> Active, IReadOnlyList<LeaderboardEntry> All);
