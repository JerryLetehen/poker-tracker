using poker_web_api.Data;

namespace poker_web_api.Endpoints.Public;

public static class GetLeaderboard
{
    public static void MapGetLeaderboard(this WebApplication app)
    {
        app.MapGet("/leaderboard", async (PokerDbContext db, string? sortBy, int? minGames) =>
        {
            var sort = sortBy ?? "rate";
            if (sort is not ("net" or "rate"))
                return Results.BadRequest(new { error = "invalid sortBy; expected 'net' or 'rate'" });

            var allMinGames = minGames ?? LeaderboardRepository.DefaultAllMinGames;
            if (allMinGames < 1)
                return Results.BadRequest(new { error = "invalid minGames; expected integer >= 1" });

            var repo = new LeaderboardRepository(db);
            var result = await repo.GetLeaderboards(allMinGames, sort);
            return Results.Ok(result);
        })
        .WithName("GetLeaderboard");
    }
}
