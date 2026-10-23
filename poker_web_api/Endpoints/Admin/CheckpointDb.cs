using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;

namespace poker_web_api.Endpoints.Admin;

public static class CheckpointDb
{
    public static void MapCheckpointDb(this WebApplication app)
    {
        app.MapPost("/admin/db/checkpoint", async (PokerDbContext db) =>
        {
            // Merge the WAL into the main db file and shrink the WAL to zero.
            await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);");

            var dbPath = DbPaths.DbFilePath(app);
            var walPath = dbPath + "-wal";

            return Results.Ok(new
            {
                DbSizeBytes = DbPaths.FileSizeOrZero(dbPath),
                WalSizeBytes = DbPaths.FileSizeOrZero(walPath)
            });
        })
        .WithName("CheckpointDb");
    }
}
