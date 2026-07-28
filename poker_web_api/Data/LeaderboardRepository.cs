using Microsoft.EntityFrameworkCore;

namespace poker_web_api.Data;

public class LeaderboardRepository
{
    private readonly PokerDbContext _db;

    public LeaderboardRepository(PokerDbContext db)
    {
        _db = db;
    }

    public async Task<List<LeaderboardEntry>> GetLeaderboard(int minGames, string sortBy)
    {
        var sortColumn = sortBy.ToLower() switch
        {
            "median" => "median",
            "net" => "result",
            _ => "rate"
        };

        var sql = $"""
            WITH player_nets AS (
                SELECT
                    e."PlayerId",
                    e."GameId",
                    g."Date",
                    SUM(CASE WHEN e."Type" = 'cash-out' THEN e."Amount" ELSE -e."Amount" END) AS net
                FROM "GameEntries" e
                JOIN "Games" g ON g."Id" = e."GameId"
                GROUP BY e."PlayerId", e."GameId", g."Date"
            ),
            last_game AS (
                SELECT "PlayerId", MAX("Date") AS last_date
                FROM player_nets
                GROUP BY "PlayerId"
            ),
            current_stats AS (
                SELECT
                    pn."PlayerId",
                    COUNT(*) AS games,
                    SUM(pn.net) AS result,
                    AVG(pn.net) AS rate,
                    percentile_cont(0.5) WITHIN GROUP (ORDER BY pn.net) AS median
                FROM player_nets pn
                GROUP BY pn."PlayerId"
                HAVING COUNT(*) >= {minGames}
            ),
            previous_stats AS (
                SELECT
                    pn."PlayerId",
                    COUNT(*) AS games,
                    SUM(pn.net) AS result,
                    AVG(pn.net) AS rate,
                    percentile_cont(0.5) WITHIN GROUP (ORDER BY pn.net) AS median
                FROM player_nets pn
                JOIN last_game lg ON lg."PlayerId" = pn."PlayerId"
                WHERE pn."Date" < lg.last_date
                GROUP BY pn."PlayerId"
            ),
            ranked AS (
                SELECT
                    current_stats."PlayerId",
                    current_stats.games,
                    current_stats.result,
                    current_stats.rate,
                    current_stats.median,
                    (current_stats.rate - COALESCE(previous_stats.rate, 0)) AS rate_delta,
                    (current_stats.median - COALESCE(previous_stats.median, 0)) AS median_delta,
                    ROW_NUMBER() OVER (ORDER BY current_stats.{sortColumn} DESC) AS current_pos,
                    ROW_NUMBER() OVER (ORDER BY COALESCE(previous_stats.{sortColumn}, 0) DESC) AS previous_pos
                FROM current_stats
                LEFT JOIN previous_stats ON previous_stats."PlayerId" = current_stats."PlayerId"
                    AND previous_stats.games >= {minGames}
            )
            SELECT
                p."Name" AS "Name",
                ranked.games::int AS "Games",
                ranked.result AS "Result",
                ranked.rate AS "Rate",
                ranked.median AS "Median",
                ranked.rate_delta AS "RateDelta",
                ranked.median_delta AS "MedianDelta",
                (ranked.previous_pos - ranked.current_pos)::int AS "PositionDelta"
            FROM ranked
            JOIN "Players" p ON p."Id" = ranked."PlayerId"
            ORDER BY ranked.current_pos
            """;

        return await _db.Database
            .SqlQueryRaw<LeaderboardEntry>(sql)
            .ToListAsync();
    }
}
