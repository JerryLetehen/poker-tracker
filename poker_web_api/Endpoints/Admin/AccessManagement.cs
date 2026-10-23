using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Endpoints.Admin;

/// <summary>
/// Admin endpoints for managing the manual allow-lists and approving pending access
/// requests. These are under /admin/access so the auth middleware gates them to admins.
/// </summary>
public static class AccessManagement
{
    public record AccessUserDto(int Id, long TelegramUserId, string Scope, string? Label, DateTimeOffset AddedAt);
    public record PendingDto(int Id, long TelegramUserId, string? FirstName, string? LastName, string? Username, DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt);
    public record AddUserRequest(long TelegramUserId, string Scope, string? Label);
    public record ApproveRequest(string Scope);

    public static void MapAccessManagement(this WebApplication app)
    {
        // List all allow-list entries.
        app.MapGet("/admin/access/users", async (PokerDbContext db) =>
        {
            var users = await db.AccessUsers
                .OrderBy(u => u.Scope).ThenBy(u => u.Label)
                .Select(u => new AccessUserDto(u.Id, u.TelegramUserId, ScopeToString(u.Scope), u.Label, u.AddedAt))
                .ToListAsync();
            return Results.Ok(users);
        }).WithName("ListAccessUsers");

        // List pending access requests (people who knocked but aren't allow-listed yet).
        app.MapGet("/admin/access/pending", async (PokerDbContext db) =>
        {
            // SQLite cannot ORDER BY a DateTimeOffset in SQL, so fetch then sort in memory.
            var rows = await db.PendingAccessRequests
                .Select(r => new PendingDto(r.Id, r.TelegramUserId, r.FirstName, r.LastName, r.Username, r.FirstSeenAt, r.LastSeenAt))
                .ToListAsync();
            var pending = rows.OrderByDescending(r => r.LastSeenAt).ToList();
            return Results.Ok(pending);
        }).WithName("ListPendingAccess");

        // Add (or update) an allow-list entry by Telegram id.
        app.MapPost("/admin/access/users", async (PokerDbContext db, AddUserRequest request, TimeProvider time) =>
        {
            if (!TryParseScope(request.Scope, out var scope))
            {
                return Results.BadRequest(new { error = "scope must be 'public' or 'admin'." });
            }

            var existing = await db.AccessUsers.FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId);
            if (existing is null)
            {
                existing = new AccessUser
                {
                    TelegramUserId = request.TelegramUserId,
                    Scope = scope,
                    Label = request.Label,
                    AddedAt = time.GetUtcNow(),
                };
                db.AccessUsers.Add(existing);
            }
            else
            {
                existing.Scope = scope;
                if (request.Label is not null) existing.Label = request.Label;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new AccessUserDto(existing.Id, existing.TelegramUserId, ScopeToString(existing.Scope), existing.Label, existing.AddedAt));
        }).WithName("AddAccessUser");

        // Remove an allow-list entry.
        app.MapDelete("/admin/access/users/{id:int}", async (PokerDbContext db, int id) =>
        {
            var user = await db.AccessUsers.FindAsync(id);
            if (user is null) return Results.NotFound();
            db.AccessUsers.Remove(user);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithName("RemoveAccessUser");

        // Approve a pending request to the chosen scope (moves it onto the allow-list).
        app.MapPost("/admin/access/pending/{id:int}/approve", async (PokerDbContext db, int id, ApproveRequest request, TimeProvider time) =>
        {
            if (!TryParseScope(request.Scope, out var scope))
            {
                return Results.BadRequest(new { error = "scope must be 'public' or 'admin'." });
            }

            var pending = await db.PendingAccessRequests.FindAsync(id);
            if (pending is null) return Results.NotFound();

            var existing = await db.AccessUsers.FirstOrDefaultAsync(u => u.TelegramUserId == pending.TelegramUserId);
            if (existing is null)
            {
                db.AccessUsers.Add(new AccessUser
                {
                    TelegramUserId = pending.TelegramUserId,
                    Scope = scope,
                    Label = BuildLabel(pending.FirstName, pending.LastName, pending.Username),
                    AddedAt = time.GetUtcNow(),
                });
            }
            else
            {
                existing.Scope = scope;
                // Fill in a name if the existing entry has none.
                if (string.IsNullOrWhiteSpace(existing.Label))
                {
                    existing.Label = BuildLabel(pending.FirstName, pending.LastName, pending.Username);
                }
            }

            db.PendingAccessRequests.Remove(pending);
            await db.SaveChangesAsync();
            return Results.Ok(new { approved = pending.TelegramUserId, scope = ScopeToString(scope) });
        }).WithName("ApprovePending");

        // Dismiss a pending request without granting access.
        app.MapDelete("/admin/access/pending/{id:int}", async (PokerDbContext db, int id) =>
        {
            var pending = await db.PendingAccessRequests.FindAsync(id);
            if (pending is null) return Results.NotFound();
            db.PendingAccessRequests.Remove(pending);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithName("DismissPending");
    }

    /// <summary>
    /// Builds a human-friendly label from a Telegram identity: "First Last (@username)",
    /// using whatever parts are available. Returns the @username alone when there is no
    /// name, or null when nothing is known.
    /// </summary>
    private static string? BuildLabel(string? firstName, string? lastName, string? username)
    {
        var name = string.Join(' ', new[] { firstName, lastName }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        var handle = string.IsNullOrWhiteSpace(username) ? null : $"@{username}";

        if (!string.IsNullOrWhiteSpace(name) && handle is not null) return $"{name} ({handle})";
        if (!string.IsNullOrWhiteSpace(name)) return name;
        return handle; // may be null
    }

    private static string ScopeToString(AccessScope scope) => scope == AccessScope.Admin ? "admin" : "public";

    private static bool TryParseScope(string? value, out AccessScope scope)
    {
        switch (value?.ToLowerInvariant())
        {
            case "admin":
                scope = AccessScope.Admin;
                return true;
            case "public":
                scope = AccessScope.Public;
                return true;
            default:
                scope = AccessScope.Public;
                return false;
        }
    }
}
