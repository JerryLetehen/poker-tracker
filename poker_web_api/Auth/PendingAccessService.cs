using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Auth;

/// <summary>
/// Records validated-but-not-yet-allowed users as pending access requests, so the
/// operator can approve them from the admin UI. Upserts by Telegram id so repeat
/// attempts update "last seen" instead of creating duplicates.
/// </summary>
public sealed class PendingAccessService
{
    private readonly PokerDbContext _db;
    private readonly TimeProvider _timeProvider;

    public PendingAccessService(PokerDbContext db, TimeProvider? timeProvider = null)
    {
        _db = db;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Inserts or refreshes the pending request for the given user. Returns true if a new
    /// record was created, false if an existing one was refreshed.
    /// </summary>
    public async Task<bool> RecordAsync(TelegramUser user, CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();

        var existing = await _db.PendingAccessRequests
            .FirstOrDefaultAsync(r => r.TelegramUserId == user.Id, ct);

        if (existing is not null)
        {
            existing.FirstName = user.FirstName;
            existing.LastName = user.LastName;
            existing.Username = user.Username;
            existing.LastSeenAt = now;
            await _db.SaveChangesAsync(ct);
            return false;
        }

        _db.PendingAccessRequests.Add(new PendingAccessRequest
        {
            TelegramUserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Username = user.Username,
            FirstSeenAt = now,
            LastSeenAt = now,
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
