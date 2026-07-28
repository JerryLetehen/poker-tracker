using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Endpoints.Admin;

public static class CreateGame
{
    public static void MapCreateGame(this WebApplication app)
    {
        app.MapPost("/admin/games", async (PokerDbContext db, CreateGameRequest request) =>
        {
            var game = new Game
            {
                Date = DateOnly.FromDateTime(DateTime.Now),
                GameType = request.GameType ?? "texas-holdem-no-limit",
                BuyInAmount = request.BuyInAmount
            };
            db.Games.Add(game);
            await db.SaveChangesAsync();
            return Results.Ok(new { game.Id, Date = game.Date.ToString("dd-MM-yyyy"), game.GameType, game.BuyInAmount });
        })
        .WithName("CreateGame");
    }
}
