using System.IO.Compression;
using Microsoft.Data.Sqlite;

namespace poker_web_api.Endpoints.Admin;

public static class BackupDb
{
    public static void MapBackupDb(this WebApplication app)
    {
        app.MapGet("/admin/db/backup", (CancellationToken ct) =>
        {
            var dbPath = DbPaths.DbFilePath(app);

            if (!File.Exists(dbPath))
                return Results.NotFound("Database file not found");

            // Make a consistent snapshot of the db into a temp file using SQLite's
            // online backup API. This captures WAL contents correctly, even while
            // the app is actively using the database.
            var tempDbPath = Path.Combine(Path.GetTempPath(), $"poker-backup-{Guid.NewGuid()}.db");

            using (var source = new SqliteConnection($"Data Source={dbPath}"))
            using (var destination = new SqliteConnection($"Data Source={tempDbPath}"))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }
            // Ensure the temp db is fully written and not left in WAL mode with sidecars
            SqliteConnection.ClearAllPools();

            // Build the zip in memory: poker.db
            var memory = new MemoryStream();
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                // Add the database snapshot
                archive.CreateEntryFromFile(tempDbPath, "poker.db");
            }

            // Clean up the temp db snapshot (and any sidecars)
            TryDelete(tempDbPath);
            TryDelete(tempDbPath + "-wal");
            TryDelete(tempDbPath + "-shm");

            memory.Position = 0;
            var fileName = $"poker-backup-{DateTime.Now:yyyy-MM-dd-HHmmss}.zip";
            return Results.File(memory, "application/zip", fileName);
        })
        .WithName("BackupDb");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best effort */ }
    }
}
