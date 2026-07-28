using poker_web_api.Data;

namespace poker_web_api.Endpoints.Public;

public static class GetLeaderboardHistory
{
    public static void MapGetLeaderboardHistory(this WebApplication app)
    {
        app.MapGet("/leaderboard/history", async (PokerDbContext db) =>
        {
            var repo = new LeaderboardHistoryRepository(db);
            return await repo.GetHistory();
        })
        .WithName("GetLeaderboardHistory");
    }
}
