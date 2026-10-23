namespace poker_web_api.Auth;

/// <summary>
/// Which Mini App (and therefore which bot token) a request is associated with.
/// Selected via the X-App request header.
/// </summary>
public enum TelegramAppKind
{
    Public,
    Admin
}
