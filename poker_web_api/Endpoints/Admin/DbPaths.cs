using Microsoft.Data.Sqlite;

namespace poker_web_api.Endpoints.Admin;

/// <summary>
/// Resolves the filesystem path for the SQLite database from configuration,
/// so the DB-management endpoints can inspect it.
/// </summary>
public static class DbPaths
{
    public static string DbFilePath(WebApplication app)
    {
        var connectionString = app.Configuration.GetConnectionString("Sqlite")
            ?? throw new InvalidOperationException("Sqlite connection string not configured");

        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;

        // Resolve relative paths against the app's content root
        return Path.IsPathRooted(dataSource)
            ? dataSource
            : Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, dataSource));
    }

    public static long FileSizeOrZero(string path) =>
        File.Exists(path) ? new FileInfo(path).Length : 0;
}
