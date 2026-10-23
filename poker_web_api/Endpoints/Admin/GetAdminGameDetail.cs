using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Endpoints.Admin;

public static class GetAdminGameDetail
{
    public static void MapGetAdminGameDetail(this WebApplication app)
    {
        app.MapGet("/admin/games/{gameId:int}", async (int gameId, PokerDbContext db) =>
        {
            var game = await db.Games
                .Include(g => g.GameEntries)
                .ThenInclude(e => e.Player)
                .FirstOrDefaultAsync(g => g.Id == gameId);

            if (game is null) return Results.NotFound();

            var entries = game.GameEntries
                .OrderBy(e => e.Id)
                .Select(e => new
                {
                    e.Id,
                    Type = e.Type == EntryType.BuyIn ? "buy-in" : "cash-out",
                    e.Amount,
                    PlayerName = e.Player.Name,
                    e.PlayerId,
                })
                .ToList();

            return Results.Ok(new
            {
                Date = game.Date.ToString("dd-MM-yyyy"),
                game.GameType,
                game.BuyInAmount,
                Checksum = game.GameEntries.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount)
                         - game.GameEntries.Where(e => e.Type == EntryType.CashOut).Sum(e => e.Amount),
                Entries = entries
            });
        })
        .WithName("GetAdminGameDetail");
    }
}
