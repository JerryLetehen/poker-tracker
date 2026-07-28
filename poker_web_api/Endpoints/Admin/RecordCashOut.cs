using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Endpoints.Admin;

public static class RecordCashOut
{
    public static void MapRecordCashOut(this WebApplication app)
    {
        app.MapPost("/admin/games/{gameId:int}/cash-out", async (int gameId, PokerDbContext db, CashOutRequest request) =>
        {
            var game = await db.Games.FindAsync(gameId);
            if (game is null) return Results.NotFound();

            var player = await db.Players.FindAsync(request.PlayerId);
            if (player is null) return Results.NotFound();

            var entry = new GameEntry
            {
                GameId = gameId,
                PlayerId = request.PlayerId,
                Type = EntryType.CashOut,
                Amount = request.Amount
            };
            db.GameEntries.Add(entry);
            await db.SaveChangesAsync();
            return Results.Ok(new { entry.Id });
        })
        .WithName("RecordCashOut");
    }
}
