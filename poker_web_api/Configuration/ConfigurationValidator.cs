namespace poker_web_api.Configuration;

/// <summary>
/// Validates required configuration at startup and fails fast with a clear, actionable
/// message when something critical is missing. This prevents a deployed environment from
/// silently booting without the secrets it needs to enforce access control.
/// </summary>
public static class ConfigurationValidator
{
    /// <summary>
    /// Validates the given options for the given environment.
    /// Returns the list of human-readable error messages (empty when valid).
    /// </summary>
    /// <param name="auth">Bound Telegram/auth options.</param>
    /// <param name="cors">Bound CORS options.</param>
    /// <param name="isProductionLike">
    /// True for deployed environments (Staging/Production) where real secrets are mandatory.
    /// False for local Development/Testing where dev-mock auth is allowed.
    /// </param>
    public static IReadOnlyList<string> Validate(
        TelegramAuthOptions auth,
        CorsOptions cors,
        bool isProductionLike)
    {
        var errors = new List<string>();

        if (isProductionLike)
        {
            if (string.IsNullOrWhiteSpace(auth.PublicBotToken))
            {
                errors.Add(
                    "Missing TelegramAuth:PublicBotToken. Set the TelegramAuth__PublicBotToken " +
                    "environment variable to the public bot's token (from BotFather).");
            }

            if (string.IsNullOrWhiteSpace(auth.AdminBotToken))
            {
                errors.Add(
                    "Missing TelegramAuth:AdminBotToken. Set the TelegramAuth__AdminBotToken " +
                    "environment variable to the admin bot's token (from BotFather).");
            }

            if (auth.BootstrapAdminIds.Count == 0)
            {
                errors.Add(
                    "No TelegramAuth:BootstrapAdminIds configured. Set TelegramAuth__BootstrapAdminIds " +
                    "to at least one Telegram numeric user id so an admin can access the app initially.");
            }

            if (cors.AllowedOrigins.Count == 0)
            {
                errors.Add(
                    "No Cors:AllowedOrigins configured. Set Cors__AllowedOrigins__0 (and __1, ...) " +
                    "to the exact frontend origin(s), e.g. https://poker-dev.example.com.");
            }

            if (auth.AllowDevMockAuth)
            {
                errors.Add(
                    "TelegramAuth:AllowDevMockAuth is enabled in a production-like environment. " +
                    "This bypasses real authentication and must be false outside Development/Testing.");
            }
        }

        if (auth.InitDataMaxAgeSeconds <= 0)
        {
            errors.Add("TelegramAuth:InitDataMaxAgeSeconds must be greater than zero.");
        }

        return errors;
    }

    /// <summary>
    /// Validates and throws <see cref="InvalidOperationException"/> if invalid, aggregating
    /// all messages so the operator sees every problem at once.
    /// </summary>
    public static void ValidateOrThrow(
        TelegramAuthOptions auth,
        CorsOptions cors,
        bool isProductionLike)
    {
        var errors = Validate(auth, cors, isProductionLike);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Invalid configuration:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(e => "  - " + e)));
        }
    }
}
