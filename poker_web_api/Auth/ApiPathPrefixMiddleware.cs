namespace poker_web_api.Auth;

/// <summary>
/// In the single-app deployment the API and both SPAs share one host. Browsers call the
/// API under an "/api" prefix (the frontends' default base URL), while the SPAs are served
/// at "/" (public) and "/admin/" (admin). This middleware strips a leading "/api" from the
/// request path so the existing endpoint routes (e.g. "/player-list", "/admin/players")
/// keep matching unchanged, and marks the request as an API call so SPA fallback can skip it.
///
/// Requests without the "/api" prefix are left untouched (static files, SPA fallback,
/// "/uploads", "/bot", "/health").
/// </summary>
public sealed class ApiPathPrefixMiddleware
{
    public const string ApiRequestItemKey = "__IsApiRequest";

    private readonly RequestDelegate _next;

    public ApiPathPrefixMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api", out var remainder))
        {
            context.Items[ApiRequestItemKey] = true;
            // Rewrite "/api/foo" -> "/foo" (and "/api" -> "/") for downstream routing.
            context.Request.Path = remainder.HasValue && remainder.Value!.Length > 0
                ? remainder
                : "/";
        }

        return _next(context);
    }
}
