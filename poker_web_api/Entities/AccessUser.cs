namespace poker_web_api.Entities;

/// <summary>
/// The access level granted to a Telegram user on this environment.
/// </summary>
public enum AccessScope
{
    /// <summary>May use the public app endpoints.</summary>
    Public,

    /// <summary>May use the admin app endpoints (and, implicitly, public endpoints).</summary>
    Admin
}

/// <summary>
/// A manually-managed allow-list entry: a Telegram user who is permitted to use the
/// app at the given scope. Access control is entirely allow-list based — there is no
/// group-membership lookup. The operator manages these via the admin UI (or the
/// bootstrap-admin seed for the very first admin).
/// </summary>
public class AccessUser
{
    public int Id { get; set; }

    /// <summary>The Telegram numeric user id. Unique within the environment.</summary>
    public long TelegramUserId { get; set; }

    public AccessScope Scope { get; set; }

    /// <summary>Optional human-friendly label (e.g. a name) to recognise the entry.</summary>
    public string? Label { get; set; }

    public DateTimeOffset AddedAt { get; set; }
}
