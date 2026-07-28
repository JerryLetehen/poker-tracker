using poker_web_api.Data;

namespace poker_web_api.Endpoints.Admin;

public static class DeleteEntry
{
    public static void MapDeleteEntry(this WebApplication app)
    {
        app.MapDelete("/admin/game-entries/{entryId:int}", async (int entryId, PokerDbContext db) =>
        {
            var entry = await db.GameEntries.FindAsync(entryId);
            if (entry is null) return Results.NotFound();

            db.GameEntries.Remove(entry);
            await db.SaveChangesAsync();
            return Results.Ok();
        })
        .WithName("DeleteEntry");
    }
}
