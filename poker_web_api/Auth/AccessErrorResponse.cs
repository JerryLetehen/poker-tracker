namespace poker_web_api.Auth;

/// <summary>
/// Body returned for auth failures so the client can branch on <c>reason</c>:
///   - "unauthenticated" (401): no valid Telegram identity (missing/invalid initData).
///   - "pending" (403): valid identity, on no allow-list; recorded as pending.
///   - "denied" (403): valid identity, but lacking the required scope.
/// </summary>
public sealed record AccessErrorResponse(string Reason, string Message)
{
    public long? TelegramUserId { get; init; }
}
