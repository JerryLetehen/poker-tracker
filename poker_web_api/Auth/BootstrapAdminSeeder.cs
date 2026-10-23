using Microsoft.EntityFrameworkCore;
using poker_web_api.Configuration;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Auth;

/// <summary>
/// Ensures the configured bootstrap admin Telegram id(s) exist on the admin allow-list,
/// so the operator can reach the admin app the first time on a fresh database. Idempotent:
/// existing entries are promoted to admin if needed, and never duplicated.
/// </summary>
public static class BootstrapAdminSeeder
{
    public static async Task SeedAsync(
        PokerDbContext db,
        TelegramAuthOptions options,
        TimeProvider? timeProvider = null,
        CancellationToken ct = default)
    {
        if (options.BootstrapAdminIds.Count == 0)
        {
            return;
        }

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();

        foreach (var id in options.BootstrapAdminIds.Distinct())
        {
            var existing = await db.AccessUsers.FirstOrDefaultAsync(u => u.TelegramUserId == id, ct);
            if (existing is null)
            {
                db.AccessUsers.Add(new AccessUser
                {
                    TelegramUserId = id,
                    Scope = AccessScope.Admin,
                    Label = "bootstrap admin",
                    AddedAt = now,
                });
            }
            else if (existing.Scope != AccessScope.Admin)
            {
                existing.Scope = AccessScope.Admin;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
