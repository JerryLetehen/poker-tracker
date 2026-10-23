namespace poker_web_api.Configuration;

/// <summary>
/// Strongly-typed access-control and Telegram settings, bound from configuration
/// (appsettings for local defaults, environment variables for secrets in deployed envs).
///
/// Secrets (bot tokens) must NEVER be committed. They are supplied via environment
/// variables / .env files that live only on the machine running the app.
/// </summary>
public sealed class TelegramAuthOptions
{
    public const string SectionName = "TelegramAuth";

    /// <summary>
    /// Bot token for the PUBLIC Mini App's bot. Used to validate initData coming
    /// from the public frontend. Secret — provided via env var TelegramAuth__PublicBotToken.
    /// </summary>
    public string PublicBotToken { get; set; } = string.Empty;

    /// <summary>
    /// Bot token for the ADMIN Mini App's bot. Used to validate initData coming
    /// from the admin frontend. Secret — provided via env var TelegramAuth__AdminBotToken.
    /// </summary>
    public string AdminBotToken { get; set; } = string.Empty;

    /// <summary>
    /// Telegram numeric user IDs seeded as admins on startup so the operator can
    /// reach the admin app the first time. After bootstrap, manage via the admin UI.
    /// Provided via env var TelegramAuth__BootstrapAdminIds (comma-separated) or
    /// as a JSON array in appsettings.
    /// </summary>
    public List<long> BootstrapAdminIds { get; set; } = new();

    /// <summary>
    /// Maximum age (in seconds) of a Telegram initData payload before it is rejected,
    /// based on its auth_date field. Guards against replay of stale payloads.
    /// </summary>
    public int InitDataMaxAgeSeconds { get; set; } = 86400;

    /// <summary>
    /// When true, the auth pipeline accepts a development-only mock identity instead of
    /// requiring real Telegram initData. Only ever enabled in local/dev or test runs —
    /// never in staging/production.
    /// </summary>
    public bool AllowDevMockAuth { get; set; }

    /// <summary>
    /// The Telegram user id used when <see cref="AllowDevMockAuth"/> is enabled and a
    /// request carries no real initData. Lets local development exercise the app without
    /// a bot. Ignored outside dev/test.
    /// </summary>
    public long DevMockUserId { get; set; } = 1;

    /// <summary>
    /// Secret path segment for the bot webhook endpoint. Telegram is configured (via
    /// setWebhook) to call /bot/{app}/webhook/{secret}; only requests carrying the
    /// matching secret are processed. Provided via env var TelegramAuth__WebhookSecret.
    /// Secret — not committed. If empty, the webhook endpoint is disabled.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;
}

/// <summary>
/// Controls whether the API also serves the two built SPAs from the same container
/// (the single-app Fly deployment). When enabled, the public SPA is served at "/" and
/// the admin SPA at "/admin/", with the API reachable under "/api". In local development
/// the SPAs run under Vite instead, so this stays disabled.
/// </summary>
public sealed class FrontendHostingOptions
{
    public const string SectionName = "FrontendHosting";

    /// <summary>When true, serve the bundled SPA static files (prod single-app image).</summary>
    public bool Enabled { get; set; }

    /// <summary>Filesystem path to the public SPA build (index.html + assets).</summary>
    public string PublicRoot { get; set; } = "wwwroot/public";

    /// <summary>Filesystem path to the admin SPA build.</summary>
    public string AdminRoot { get; set; } = "wwwroot/admin";

    /// <summary>URL path prefix the admin SPA is served under.</summary>
    public string AdminPath { get; set; } = "/admin";
}
