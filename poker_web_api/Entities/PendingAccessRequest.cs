namespace poker_web_api.Entities;

/// <summary>
/// A validated Telegram user who opened the app but is not yet on any allow-list.
/// Recorded so the operator can see who is "knocking" and approve them with one tap
/// from the admin UI, without having to hunt down numeric ids.
/// </summary>
public class PendingAccessRequest
{
    public int Id { get; set; }

    /// <summary>The Telegram numeric user id. Unique (upserted on repeat attempts).</summary>
    public long TelegramUserId { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Username { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}
