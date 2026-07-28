using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Endpoints.Admin;

public static class GetActiveGame
{
    public static void MapGetActiveGame(this WebApplication app)
    {
        app.MapGet("/admin/active-game", async (PokerDbContext db) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var game = await db.Games
                .Include(g => g.GameEntries)
                .ThenInclude(e => e.Player)
                .Where(g => g.Date == today)
                .FirstOrDefaultAsync();

            if (game is null) return Results.Ok(new { Active = false });

            var players = game.GameEntries
                .GroupBy(e => e.Player)
                .Select(g => new
                {
                    PlayerId = g.Key.Id,
                    PlayerName = g.Key.Name,
                    BuyIns = g.Count(e => e.Type == EntryType.BuyIn),
                    TotalIn = g.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount),
                    CashOut = g.Where(e => e.Type == EntryType.CashOut).Any()
                        ? (decimal?)g.Where(e => e.Type == EntryType.CashOut).Sum(e => e.Amount)
                        : null,
                    Entries = g.Select(e => new { e.Id, e.Type, e.Amount }).ToList()
                })
                .ToList();

            return Results.Ok(new
            {
                Active = true,
                GameId = game.Id,
                Date = game.Date.ToString("dd-MM-yyyy"),
                game.GameType,
                Players = players
            });
        })
        .WithName("GetActiveGame");
    }
}
