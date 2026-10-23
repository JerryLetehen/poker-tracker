namespace poker_web_api.Configuration;

/// <summary>
/// CORS configuration. Replaces the previous AllowAnyOrigin policy with an explicit
/// allow-list of origins (the deployed frontend domains). Provided via appsettings
/// locally and via env var Cors__AllowedOrigins__0, __1, ... in deployed environments.
/// </summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Exact origins allowed to call the API (scheme + host + optional port),
    /// e.g. "https://poker-dev.example.com". If empty, no cross-origin browser
    /// requests are permitted.
    /// </summary>
    public List<string> AllowedOrigins { get; set; } = new();
}
