namespace poker_web_api.Auth;

/// <summary>
/// Per-request authenticated identity + access decision, populated by the auth
/// middleware and read by endpoints. Scoped so each request gets its own instance.
/// </summary>
public sealed class CurrentUserContext
{
    public TelegramUser? User { get; set; }
    public AccessDecision Decision { get; set; } = AccessDecision.Pending;
    public TelegramAppKind AppKind { get; set; } = TelegramAppKind.Public;

    public bool IsAuthenticated => User is not null;
}
