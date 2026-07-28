using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Endpoints.Public;

public static class GetGames
{
    public static void MapGetGames(this WebApplication app)
    {
        app.MapGet("/games", async (PokerDbContext db) =>
        {
            var games = await db.Games
                .Include(g => g.GameEntries)
                .ThenInclude(e => e.Player)
                .OrderByDescending(g => g.Date)
                .ToListAsync();

            var result = games.Select(g =>
            {
                var playerStats = g.GameEntries
                    .GroupBy(e => e.Player)
                    .Select(pg => new
                    {
                        Name = pg.Key.Name,
                        BuyIn = pg.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount),
                        Net = pg.Where(e => e.Type == EntryType.CashOut).Sum(e => e.Amount)
                            - pg.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount)
                    })
                    .ToList();

                var biggestBuyIn = playerStats.OrderByDescending(p => p.BuyIn).FirstOrDefault();
                var biggestNet = playerStats.OrderByDescending(p => p.Net).FirstOrDefault();
                var biggestLoss = playerStats.OrderBy(p => p.Net).FirstOrDefault();

                return new
                {
                    g.Id,
                    Date = g.Date.ToString("dd-MM-yyyy"),
                    g.GameType,
                    PlayerCount = playerStats.Count,
                    Pot = g.GameEntries.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount),
                    Checksum = g.GameEntries.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount)
                             - g.GameEntries.Where(e => e.Type == EntryType.CashOut).Sum(e => e.Amount),
                    BiggestBuyInPlayer = biggestBuyIn?.Name,
                    BiggestBuyInAmount = biggestBuyIn?.BuyIn ?? 0,
                    BiggestNetPlayer = biggestNet?.Name,
                    BiggestNetAmount = biggestNet?.Net ?? 0,
                    BiggestLossPlayer = biggestLoss?.Name,
                    BiggestLossAmount = biggestLoss?.Net ?? 0
                };
            }).ToList();

            return result;
        })
        .WithName("GetGames");
    }
}
