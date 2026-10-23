using System.Text.Json;
using poker_web_api.Auth;
using poker_web_api.Configuration;

namespace poker_web_api.Endpoints;

/// <summary>
/// Telegram bot webhook endpoint. Telegram POSTs updates here when someone messages
/// a bot. The path carries the app kind (public/admin, to pick the right bot token)
/// and a secret segment that must match the configured webhook secret.
///
/// This endpoint is intentionally NOT behind the Mini App auth middleware: it is called
/// by Telegram's servers, not by an authenticated Mini App user. The secret segment
/// is what protects it. (It lives under /bot, which the auth middleware does not gate.)
/// </summary>
public static class BotWebhook
{
    public static void MapBotWebhook(this WebApplication app)
    {
        app.MapPost("/bot/{appKind}/webhook/{secret}", async (
            string appKind,
            string secret,
            HttpRequest request,
            TelegramAuthOptions options,
            BotWebhookService webhookService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(options.WebhookSecret) ||
                !CryptoEquals(secret, options.WebhookSecret))
            {
                return Results.NotFound();
            }

            var botToken = string.Equals(appKind, "admin", StringComparison.OrdinalIgnoreCase)
                ? options.AdminBotToken
                : options.PublicBotToken;

            using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
            await webhookService.HandleUpdateAsync(botToken, doc.RootElement.Clone(), ct);

            // Always 200 so Telegram does not retry on our internal issues.
            return Results.Ok();
        })
        .WithName("BotWebhook");
    }

    private static bool CryptoEquals(string a, string b) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a),
            System.Text.Encoding.UTF8.GetBytes(b));
}
