using poker_web_api.Data;

namespace poker_web_api.Endpoints.Public;

public static class GetLeaderboard
{
    public static void MapGetLeaderboard(this WebApplication app)
    {
        app.MapGet("/leaderboard", async (PokerDbContext db, int minGames = 1, string sortBy = "rate") =>
        {
            var repo = new LeaderboardRepository(db);
            return await repo.GetLeaderboard(minGames, sortBy);
        })
        .WithName("GetLeaderboard");
    }
}
