using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Endpoints.Admin;

public static class RecordBuyIn
{
    public static void MapRecordBuyIn(this WebApplication app)
    {
        app.MapPost("/admin/games/{gameId:int}/buy-in", async (int gameId, PokerDbContext db, BuyInRequest request) =>
        {
            var game = await db.Games.FindAsync(gameId);
            if (game is null) return Results.NotFound();

            var player = await db.Players.FindAsync(request.PlayerId);
            if (player is null) return Results.NotFound();

            var entry = new GameEntry
            {
                GameId = gameId,
                PlayerId = request.PlayerId,
                Type = EntryType.BuyIn,
                Amount = request.Amount
            };
            db.GameEntries.Add(entry);
            await db.SaveChangesAsync();
            return Results.Ok(new { entry.Id });
        })
        .WithName("RecordBuyIn");
    }
}
