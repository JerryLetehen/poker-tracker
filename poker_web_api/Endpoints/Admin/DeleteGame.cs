using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;

namespace poker_web_api.Endpoints.Admin;

public static class DeleteGame
{
    public static void MapDeleteGame(this WebApplication app)
    {
        app.MapDelete("/admin/games/{gameId:int}", async (int gameId, PokerDbContext db) =>
        {
            var game = await db.Games
                .Include(g => g.GameEntries)
                .FirstOrDefaultAsync(g => g.Id == gameId);
            if (game is null) return Results.NotFound();

            db.GameEntries.RemoveRange(game.GameEntries);
            db.Games.Remove(game);
            await db.SaveChangesAsync();
            return Results.Ok();
        })
        .WithName("DeleteGame");
    }
}
