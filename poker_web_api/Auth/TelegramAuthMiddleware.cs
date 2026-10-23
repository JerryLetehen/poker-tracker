using poker_web_api.Configuration;
using poker_web_api.Entities;

namespace poker_web_api.Auth;

/// <summary>
/// Authenticates and authorizes each API request from a Telegram Mini App.
///
/// Flow:
///   1. Skip infrastructure paths (OpenAPI, health, scalar) — these are not gated.
///   2. Determine the required scope from the path: "/admin/*" needs Admin, otherwise Public.
///   3. Pick the bot token from the X-App header (public vs admin) and validate the
///      initData carried in the Authorization header ("tma &lt;initData&gt;").
///   4. Resolve the allow-list decision for the identified user; record unknown-but-valid
///      users as pending.
///   5. Enforce fail-closed, returning precise status codes:
///        401 unauthenticated — no valid identity;
///        403 pending         — valid identity, on no list;
///        403 denied          — valid identity, insufficient scope.
///
/// In Development/Testing with AllowDevMockAuth, a request lacking real initData is
/// treated as the configured mock user so the app can run without a bot.
/// </summary>
public sealed class TelegramAuthMiddleware
{
    public const string AppHeader = "X-App";
    public const string AuthHeader = "Authorization";
    public const string AuthScheme = "tma ";

    private static readonly string[] UnprotectedPrefixes =
    {
        "/openapi",
        "/uploads",
        "/health",
        "/scalar",
        "/bot/", // Telegram bot webhook — protected by its own secret path segment.
    };

    private readonly RequestDelegate _next;
    private readonly TelegramAuthOptions _options;
    private readonly ITelegramInitDataValidator _validator;
    private readonly bool _frontendsBundled;

    public TelegramAuthMiddleware(
        RequestDelegate next,
        TelegramAuthOptions options,
        ITelegramInitDataValidator validator,
        FrontendHostingOptions frontendHosting)
    {
        _next = next;
        _options = options;
        _validator = validator;
        _frontendsBundled = frontendHosting.Enabled;
    }

    public async Task InvokeAsync(
        HttpContext context,
        CurrentUserContext currentUser,
        AccessControlService accessControl,
        PendingAccessService pendingAccess)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // When the API also serves the SPAs, only "/api" requests (marked by the
        // ApiPathPrefixMiddleware) are gated. SPA navigations and static assets pass through.
        if (_frontendsBundled && !context.Items.ContainsKey(ApiPathPrefixMiddleware.ApiRequestItemKey))
        {
            await _next(context);
            return;
        }

        if (IsUnprotected(path))
        {
            await _next(context);
            return;
        }

        var appKind = ResolveAppKind(context);
        currentUser.AppKind = appKind;

        var user = Authenticate(context, appKind);
        if (user is null && _options.AllowDevMockAuth)
        {
            user = new TelegramUser(_options.DevMockUserId, "Dev", "User", "devuser");
        }

        if (user is null)
        {
            await WriteError(context, StatusCodes.Status401Unauthorized,
                new AccessErrorResponse("unauthenticated", "Open this app from its Telegram bot."));
            return;
        }

        currentUser.User = user;
        var decision = await accessControl.ResolveAsync(user.Id, context.RequestAborted);
        currentUser.Decision = decision;

        var required = RequiredScope(path);

        if (AccessControlService.Satisfies(decision, required))
        {
            await _next(context);
            return;
        }

        if (decision == AccessDecision.Pending)
        {
            await pendingAccess.RecordAsync(user, context.RequestAborted);
            await WriteError(context, StatusCodes.Status403Forbidden,
                new AccessErrorResponse("pending", "Your access is pending approval.")
                {
                    TelegramUserId = user.Id
                });
            return;
        }

        await WriteError(context, StatusCodes.Status403Forbidden,
            new AccessErrorResponse("denied", "You do not have access to this action.")
            {
                TelegramUserId = user.Id
            });
    }

    private static bool IsUnprotected(string path) =>
        UnprotectedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private static AccessScope RequiredScope(string path) =>
        path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
            ? AccessScope.Admin
            : AccessScope.Public;

    private TelegramUser? Authenticate(HttpContext context, TelegramAppKind appKind)
    {
        var header = context.Request.Headers[AuthHeader].ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith(AuthScheme, StringComparison.Ordinal))
        {
            return null;
        }

        var initData = header[AuthScheme.Length..].Trim();
        var botToken = appKind == TelegramAppKind.Admin ? _options.AdminBotToken : _options.PublicBotToken;

        var result = _validator.Validate(initData, botToken);
        return result.IsValid ? result.User : null;
    }

    private static TelegramAppKind ResolveAppKind(HttpContext context)
    {
        var app = context.Request.Headers[AppHeader].ToString();
        return string.Equals(app, "admin", StringComparison.OrdinalIgnoreCase)
            ? TelegramAppKind.Admin
            : TelegramAppKind.Public;
    }

    private static Task WriteError(HttpContext context, int statusCode, AccessErrorResponse body)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(body);
    }
}
