using poker_web_api.Data;

namespace poker_web_api.Endpoints.Admin;

public static class RenamePlayer
{
    public static void MapRenamePlayer(this WebApplication app)
    {
        app.MapPut("/admin/players/{playerId:int}", async (int playerId, PokerDbContext db, CreatePlayerRequest request) =>
        {
            var player = await db.Players.FindAsync(playerId);
            if (player is null) return Results.NotFound();
            player.Name = request.Name;
            await db.SaveChangesAsync();
            return Results.Ok(new { player.Id, player.Name });
        })
        .WithName("RenamePlayer");
    }
}
