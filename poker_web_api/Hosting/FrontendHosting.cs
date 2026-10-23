using Microsoft.Extensions.FileProviders;
using poker_web_api.Auth;
using poker_web_api.Configuration;

namespace poker_web_api.Hosting;

/// <summary>
/// Serves the two built SPAs from the API container in the single-app deployment:
///   - Public SPA at "/"            (static assets + SPA fallback to its index.html)
///   - Admin  SPA at "/admin"       (static assets + SPA fallback to its index.html)
/// API calls arrive under "/api" (stripped earlier by ApiPathPrefixMiddleware) and are
/// never treated as SPA routes. "/uploads", "/bot", "/health" are left to their handlers.
/// </summary>
public static class FrontendHosting
{
    public static void UseBundledFrontends(this WebApplication app, FrontendHostingOptions options)
    {
        if (!options.Enabled)
        {
            return;
        }

        var contentRoot = app.Environment.ContentRootPath;
        var publicRoot = Path.IsPathRooted(options.PublicRoot)
            ? options.PublicRoot
            : Path.Combine(contentRoot, options.PublicRoot);
        var adminRoot = Path.IsPathRooted(options.AdminRoot)
            ? options.AdminRoot
            : Path.Combine(contentRoot, options.AdminRoot);
        var adminPath = "/" + options.AdminPath.Trim('/');

        var publicProvider = new PhysicalFileProvider(publicRoot);
        var adminProvider = new PhysicalFileProvider(adminRoot);

        // Serve the ADMIN SPA's static assets under its path prefix first (more specific),
        // then the PUBLIC SPA's assets at the root.
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = adminProvider,
            RequestPath = adminPath,
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = publicProvider,
            RequestPath = string.Empty,
        });

        // SPA fallback: for non-API GET requests that didn't match a static file or an
        // API endpoint, return the appropriate index.html so client-side routing works.
        app.MapFallback(async context =>
        {
            // Never serve HTML for API/infra requests.
            var isApi = context.Items.ContainsKey(ApiPathPrefixMiddleware.ApiRequestItemKey);
            var path = context.Request.Path.Value ?? "/";
            if (isApi
                || path.StartsWith("/uploads", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/bot", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var (provider, file) = path.StartsWith(adminPath, StringComparison.OrdinalIgnoreCase)
                ? (adminProvider, "index.html")
                : (publicProvider, "index.html");

            var indexFile = provider.GetFileInfo(file);
            if (!indexFile.Exists)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(indexFile);
        });
    }
}
