namespace poker_web_api.Auth;

/// <summary>
/// Outcome of validating a Telegram Mini App initData string.
/// </summary>
public sealed class InitDataValidationResult
{
    public bool IsValid { get; private init; }

    /// <summary>Why validation failed (null when valid). For diagnostics/logging only.</summary>
    public string? Error { get; private init; }

    /// <summary>The parsed user (null when invalid or the payload had no user).</summary>
    public TelegramUser? User { get; private init; }

    public static InitDataValidationResult Valid(TelegramUser user) =>
        new() { IsValid = true, User = user };

    public static InitDataValidationResult Invalid(string error) =>
        new() { IsValid = false, Error = error };
}
