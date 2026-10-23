using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Endpoints.Public;

public static class GetRecords
{
    public static void MapGetRecords(this WebApplication app)
    {
        app.MapGet("/records", async (PokerDbContext db) =>
        {
            // SQLite cannot ORDER BY a decimal expression (including decimal SUM aggregates)
            // inside a SQL query. So we materialize the raw per-player-per-game rows into
            // memory first, then compute nets and order/take/group with LINQ-to-Objects.
            var rawEntries = await db.GameEntries
                .Select(e => new
                {
                    e.PlayerId,
                    e.GameId,
                    e.Type,
                    e.Amount
                })
                .ToListAsync();

            var playerNames = await db.Players.ToDictionaryAsync(p => p.Id, p => p.Name);
            var gameDates = await db.Games.ToDictionaryAsync(g => g.Id, g => g.Date);

            // Net per player per game, computed in memory.
            var perPlayerPerGameNets = rawEntries
                .GroupBy(e => new { e.PlayerId, e.GameId })
                .Select(g => new
                {
                    g.Key.PlayerId,
                    g.Key.GameId,
                    Net = g.Where(e => e.Type == EntryType.CashOut).Sum(e => e.Amount)
                        - g.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount)
                })
                .Select(x => new
                {
                    PlayerName = playerNames[x.PlayerId],
                    x.GameId,
                    Date = gameDates.TryGetValue(x.GameId, out var d) ? d.ToString() : string.Empty,
                    x.Net
                })
                .ToList();

            var bestWins = perPlayerPerGameNets
                .OrderByDescending(x => x.Net)
                .Take(20)
                .GroupBy(x => x.PlayerName)
                .Select(g => g.First())
                .OrderByDescending(x => x.Net)
                .ToList();

            var worstLosses = perPlayerPerGameNets
                .OrderBy(x => x.Net)
                .Take(20)
                .GroupBy(x => x.PlayerName)
                .Select(g => g.First())
                .OrderBy(x => x.Net)
                .ToList();

            // PlayerCount is an int, so ordering by it in SQL is fine.
            var topByPlayers = await db.Games
                .Select(g => new
                {
                    g.Id,
                    Date = g.Date.ToString(),
                    PlayerCount = g.GameEntries.Select(e => e.PlayerId).Distinct().Count()
                })
                .OrderByDescending(g => g.PlayerCount)
                .Take(3)
                .ToListAsync();

            // Pot is a decimal SUM, which SQLite cannot ORDER BY. Compute the pot per game
            // in memory from the already-fetched raw entries, then order/take.
            var topByPot = rawEntries
                .Where(e => e.Type == EntryType.BuyIn)
                .GroupBy(e => e.GameId)
                .Select(g => new
                {
                    Id = g.Key,
                    Date = gameDates.TryGetValue(g.Key, out var d) ? d.ToString() : string.Empty,
                    Pot = g.Sum(e => e.Amount)
                })
                .OrderByDescending(g => g.Pot)
                .Take(5)
                .ToList();

            var topBuyIns = rawEntries
                .Where(e => e.Type == EntryType.BuyIn)
                .GroupBy(e => new { e.PlayerId, e.GameId })
                .Select(g => new
                {
                    g.Key.PlayerId,
                    g.Key.GameId,
                    BuyIn = g.Sum(e => e.Amount)
                })
                .GroupBy(x => x.PlayerId)
                .Select(g => g.OrderByDescending(x => x.BuyIn).First())
                .OrderByDescending(x => x.BuyIn)
                .Take(5)
                .ToList();

            var topBuyInsResult = topBuyIns.Select(x => new
            {
                PlayerName = playerNames[x.PlayerId],
                x.GameId,
                Date = gameDates.TryGetValue(x.GameId, out var d) ? d.ToString("dd-MM-yyyy") : string.Empty,
                x.BuyIn
            }).ToList();

            return Results.Ok(new
            {
                BestWins = bestWins,
                WorstLosses = worstLosses,
                TopByPlayers = topByPlayers,
                TopByPot = topByPot,
                TopBuyIns = topBuyInsResult
            });
        })
        .WithName("GetRecords");
    }
}
