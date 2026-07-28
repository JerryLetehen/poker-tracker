using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Endpoints.Admin;

public static class CreatePlayer
{
    public static void MapCreatePlayer(this WebApplication app)
    {
        app.MapPost("/admin/players", async (PokerDbContext db, CreatePlayerRequest request) =>
        {
            var player = new Player { Name = request.Name };
            db.Players.Add(player);
            await db.SaveChangesAsync();
            return Results.Ok(new { player.Id, player.Name });
        })
        .WithName("CreatePlayer");
    }
}
