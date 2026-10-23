namespace poker_web_api.Endpoints.Admin;

public static class GetDbStatus
{
    public static void MapGetDbStatus(this WebApplication app)
    {
        app.MapGet("/admin/db/status", () =>
        {
            var dbPath = DbPaths.DbFilePath(app);
            var walPath = dbPath + "-wal";

            return Results.Ok(new
            {
                DbSizeBytes = DbPaths.FileSizeOrZero(dbPath),
                WalSizeBytes = DbPaths.FileSizeOrZero(walPath)
            });
        })
        .WithName("GetDbStatus");
    }
}
