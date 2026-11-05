using Microsoft.EntityFrameworkCore;
using poker_web_api.Entities;

namespace poker_web_api.Data;

public class LeaderboardRepository
{
    public const int DefaultActiveMinGames = 10;
    public const int DefaultAllMinGames = 5;

    private readonly PokerDbContext _db;

    public LeaderboardRepository(PokerDbContext db)
    {
        _db = db;
    }

    public async Task<LeaderboardsResponse> GetLeaderboards(int allMinGames, string sortBy, DateOnly? today = null)
    {
        // Resolve the current date once per call so the window is stable across the request.
        var currentDate = today ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // Load the shared per-player-per-game net rows once; both lists derive from them.
        var rows = await LoadPlayerGameNets();

        // --- Active list ---
        // MEMBERSHIP is decided by recent activity: a player qualifies if they have at least
        // DefaultActiveMinGames distinct games within a rolling 12-month window ending on the
        // current date (inclusive both ends). DateOnly.AddMonths clamps to the last valid day of
        // the target month (e.g. 2024-02-29 -> 2023-02-28).
        //
        // STATS are ALWAYS all-time: once a player qualifies, their Games/Result/Rate are computed
        // from ALL their games, not just the windowed ones. So the Active list is a filtered set of
        // players shown with the same all-time numbers as the All list.
        var windowStart = currentDate.AddMonths(-12);
        var activePlayerIds = rows
            .Where(r => r.Date >= windowStart && r.Date <= currentDate)
            .GroupBy(r => r.PlayerId)
            .Where(g => g.Select(r => r.GameId).Distinct().Count() >= DefaultActiveMinGames)
            .Select(g => g.Key)
            .ToHashSet();

        var activeRows = rows.Where(r => activePlayerIds.Contains(r.PlayerId)).ToList();
        // minGames = 1 here only guards against a player with zero all-time rows; membership was
        // already decided by the windowed threshold above.
        var active = OrderEntries(BuildEntries(activeRows, minGames: 1), sortBy);

        // --- All list ---
        // MEMBERSHIP: players with at least allMinGames distinct games all-time. STATS all-time.
        var all = OrderEntries(BuildEntries(rows, allMinGames), sortBy);

        return new LeaderboardsResponse(active, all);
    }

    // One row per player per game: the player's net for that game plus the game date and player name.
    // Shared by the Active (window-filtered) and All (all-time) lists so the data is loaded once.
    private sealed record PlayerGameNet(int PlayerId, string Name, int GameId, DateOnly Date, decimal Net);

    // Executes the grouped per-player-per-game net aggregation in SQL and materializes it to memory.
    // Net = Σ CashOut − Σ BuyIn, where an absent side sums to zero (preserved from the original query).
    // The grouping collapses rebuys to a single (PlayerId, GameId) row, so later row counts per player
    // equal the distinct game count. Materializing once avoids a second DB round-trip for the window list.
    private async Task<List<PlayerGameNet>> LoadPlayerGameNets()
    {
        var query = _db.GameEntries
            .GroupBy(e => new { e.PlayerId, e.GameId })
            .Select(g => new
            {
                g.Key.PlayerId,
                g.Key.GameId,
                Net = g.Where(e => e.Type == EntryType.CashOut).Sum(e => e.Amount)
                    - g.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount)
            })
            .Join(_db.Players, s => s.PlayerId, p => p.Id, (s, p) => new { s.PlayerId, p.Name, s.GameId, s.Net })
            .Join(_db.Games, s => s.GameId, game => game.Id, (s, game) => new
            {
                s.PlayerId,
                s.Name,
                s.GameId,
                game.Date,
                s.Net
            });

        var data = await query.ToListAsync();

        return data
            .Select(d => new PlayerGameNet(d.PlayerId, d.Name, d.GameId, d.Date, d.Net))
            .ToList();
    }

    // Aggregates the given per-player-per-game rows into one entry per qualifying player (unordered;
    // ordering is applied separately). Because LoadPlayerGameNets already collapses rebuys to one
    // (PlayerId, GameId) row, the row count per player equals that player's distinct game count, so
    // Games = row count, Result = Σ Net, and Rate = Result / Games. Players below the minGames
    // distinct-game threshold are dropped. Used by both the Active (window-filtered) and All lists.
    private static List<LeaderboardEntry> BuildEntries(IReadOnlyList<PlayerGameNet> rows, int minGames)
    {
        return rows
            .GroupBy(x => x.PlayerId)
            .Where(g => g.Count() >= minGames)
            .Select(g =>
            {
                var games = g.Count();
                var result = g.Sum(x => x.Net);
                var rate = (double)result / games;
                return new LeaderboardEntry(g.First().Name, games, result, rate);
            })
            .ToList();
    }

    // Applies the deterministic total ordering for a leaderboard list (sorted in memory because
    // SQLite can't ORDER BY decimal; the dataset is one row per qualifying player). Name is the
    // final tie-break using StringComparer.Ordinal and is unique per player, so the order is total.
    // "net": Result desc, then Rate desc, then Name asc. Any other value (rate, default, unknown):
    // Rate desc, then Result desc, then Name asc — preserving the existing non-"net" fallback.
    private static List<LeaderboardEntry> OrderEntries(IEnumerable<LeaderboardEntry> entries, string sortBy)
    {
        return sortBy.ToLower() == "net"
            ? entries
                .OrderByDescending(e => e.Result)
                .ThenByDescending(e => e.Rate)
                .ThenBy(e => e.Name, StringComparer.Ordinal)
                .ToList()
            : entries
                .OrderByDescending(e => e.Rate)
                .ThenByDescending(e => e.Result)
                .ThenBy(e => e.Name, StringComparer.Ordinal)
                .ToList();
    }
}
