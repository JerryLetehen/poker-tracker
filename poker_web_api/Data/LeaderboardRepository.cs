using Microsoft.EntityFrameworkCore;
using poker_web_api.Entities;

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
        // Calculate net per player per game
        var playerGameNets = _db.GameEntries
            .GroupBy(e => new { e.PlayerId, e.GameId })
            .Select(g => new
            {
                g.Key.PlayerId,
                Net = g.Where(e => e.Type == EntryType.CashOut).Sum(e => e.Amount)
                    - g.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount)
            });

        // Aggregate per player
        var stats = playerGameNets
            .GroupBy(x => x.PlayerId)
            .Where(g => g.Count() >= minGames)
            .Select(g => new
            {
                PlayerId = g.Key,
                Games = g.Count(),
                Result = g.Sum(x => x.Net),
                Rate = g.Average(x => x.Net)
            });

        // Join with player names
        var query = stats
            .Join(_db.Players, s => s.PlayerId, p => p.Id, (s, p) => new
            {
                p.Name,
                s.Games,
                s.Result,
                s.Rate
            });

        // Execute, then sort in memory.
        // SQLite can't ORDER BY decimal in SQL, so we sort client-side.
        // Dataset is small (one row per player), so this is negligible.
        var data = await query.ToListAsync();

        var sorted = sortBy.ToLower() == "net"
            ? data.OrderByDescending(e => e.Result)
            : data.OrderByDescending(e => e.Rate);

        return sorted.Select(d => new LeaderboardEntry(
            d.Name,
            d.Games,
            d.Result,
            (double)d.Rate
        )).ToList();
    }
}
