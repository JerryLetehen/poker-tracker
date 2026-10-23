using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Auth;

/// <summary>
/// The authorization decision for a validated Telegram user.
/// </summary>
public enum AccessDecision
{
    /// <summary>User is on the public allow-list (or is an admin). Public endpoints allowed.</summary>
    Public,

    /// <summary>User is on the admin allow-list. Admin (and public) endpoints allowed.</summary>
    Admin,

    /// <summary>User is on no list. They are recorded as a pending request and denied.</summary>
    Pending
}

/// <summary>
/// Reads the SQLite allow-lists to decide what a validated Telegram user may do.
/// Access is entirely allow-list based (no group lookup). Fail-closed: anyone not on a
/// list is Pending (denied). Admins implicitly satisfy public access.
/// </summary>
public sealed class AccessControlService
{
    private readonly PokerDbContext _db;

    public AccessControlService(PokerDbContext db) => _db = db;

    /// <summary>
    /// Resolves the access decision for the given Telegram user id.
    /// </summary>
    public async Task<AccessDecision> ResolveAsync(long telegramUserId, CancellationToken ct = default)
    {
        var entry = await _db.AccessUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        return entry switch
        {
            { Scope: AccessScope.Admin } => AccessDecision.Admin,
            { Scope: AccessScope.Public } => AccessDecision.Public,
            _ => AccessDecision.Pending
        };
    }

    /// <summary>
    /// Whether the decision satisfies the access level an endpoint requires.
    /// </summary>
    public static bool Satisfies(AccessDecision decision, AccessScope required) => required switch
    {
        AccessScope.Admin => decision == AccessDecision.Admin,
        AccessScope.Public => decision is AccessDecision.Public or AccessDecision.Admin,
        _ => false
    };
}
