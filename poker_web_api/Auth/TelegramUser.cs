namespace poker_web_api.Auth;

/// <summary>
/// The authenticated Telegram user extracted from a validated initData payload.
/// </summary>
public sealed record TelegramUser(
    long Id,
    string? FirstName,
    string? LastName,
    string? Username);
