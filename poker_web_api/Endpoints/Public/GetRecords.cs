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
            var maxWinnings = await db.GameEntries
                .GroupBy(e => new { e.PlayerId, e.GameId })
                .Select(g => new
                {
                    g.Key.PlayerId,
                    g.Key.GameId,
                    Net = g.Where(e => e.Type == EntryType.CashOut).Sum(e => e.Amount)
                        - g.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount)
                })
                .OrderByDescending(x => x.Net)
                .Take(20)
                .Join(db.Players, x => x.PlayerId, p => p.Id, (x, p) => new { p.Name, x.GameId, x.Net })
                .Join(db.Games, x => x.GameId, g => g.Id, (x, g) => new
                {
                    PlayerName = x.Name,
                    x.GameId,
                    Date = g.Date.ToString(),
                    x.Net
                })
                .ToListAsync();

            var bestWins = maxWinnings
                .GroupBy(x => x.PlayerName)
                .Select(g => g.First())
                .OrderByDescending(x => x.Net)
                .ToList();

            var maxLosses = await db.GameEntries
                .GroupBy(e => new { e.PlayerId, e.GameId })
                .Select(g => new
                {
                    g.Key.PlayerId,
                    g.Key.GameId,
                    Net = g.Where(e => e.Type == EntryType.CashOut).Sum(e => e.Amount)
                        - g.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount)
                })
                .OrderBy(x => x.Net)
                .Take(20)
                .Join(db.Players, x => x.PlayerId, p => p.Id, (x, p) => new { p.Name, x.GameId, x.Net })
                .Join(db.Games, x => x.GameId, g => g.Id, (x, g) => new
                {
                    PlayerName = x.Name,
                    x.GameId,
                    Date = g.Date.ToString(),
                    x.Net
                })
                .ToListAsync();

            var worstLosses = maxLosses
                .GroupBy(x => x.PlayerName)
                .Select(g => g.First())
                .OrderBy(x => x.Net)
                .ToList();

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

            var topByPot = await db.Games
                .Select(g => new
                {
                    g.Id,
                    Date = g.Date.ToString(),
                    Pot = g.GameEntries.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount)
                })
                .OrderByDescending(g => g.Pot)
                .Take(5)
                .ToListAsync();

            var allBuyIns = await db.GameEntries
                .Where(e => e.Type == EntryType.BuyIn)
                .GroupBy(e => new { e.PlayerId, e.GameId })
                .Select(g => new
                {
                    g.Key.PlayerId,
                    g.Key.GameId,
                    BuyIn = g.Sum(e => e.Amount)
                })
                .ToListAsync();

            var topBuyIns = allBuyIns
                .GroupBy(x => x.PlayerId)
                .Select(g => g.OrderByDescending(x => x.BuyIn).First())
                .OrderByDescending(x => x.BuyIn)
                .Take(5)
                .ToList();

            var buyInPlayerIds = topBuyIns.Select(x => x.PlayerId).ToList();
            var buyInGameIds = topBuyIns.Select(x => x.GameId).ToList();
            var buyInPlayers = await db.Players.Where(p => buyInPlayerIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);
            var buyInGames = await db.Games.Where(g => buyInGameIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Date);

            var topBuyInsResult = topBuyIns.Select(x => new
            {
                PlayerName = buyInPlayers[x.PlayerId],
                x.GameId,
                Date = buyInGames[x.GameId].ToString("dd-MM-yyyy"),
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
