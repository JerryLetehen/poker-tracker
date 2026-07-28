using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Endpoints.Public;

public static class GetGameDetail
{
    public static void MapGetGameDetail(this WebApplication app)
    {
        app.MapGet("/games/{id:int}", async (int id, PokerDbContext db) =>
        {
            var game = await db.Games
                .Include(g => g.GameEntries)
                .ThenInclude(e => e.Player)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game is null) return Results.NotFound();

            var players = game.GameEntries
                .GroupBy(e => e.Player)
                .Select(g => new
                {
                    PlayerName = g.Key.Name,
                    BuyIn = g.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount),
                    CashOut = g.Where(e => e.Type == EntryType.CashOut).Sum(e => e.Amount),
                    Net = g.Where(e => e.Type == EntryType.CashOut).Sum(e => e.Amount)
                        - g.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount)
                })
                .OrderByDescending(p => p.Net)
                .ToList();

            return Results.Ok(new
            {
                Date = game.Date.ToString("dd-MM-yyyy"),
                game.GameType,
                Players = players
            });
        })
        .WithName("GetGameDetail");
    }
}
