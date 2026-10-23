using System.Text.Json;

namespace poker_web_api.Auth;

/// <summary>
/// Processes incoming Telegram bot updates (webhook payloads). Currently handles the
/// /start command by replying with the user's numeric Telegram id, which makes it easy
/// for people to tell the operator who they are for allow-listing.
/// </summary>
public sealed class BotWebhookService
{
    private readonly ITelegramBotClient _bot;

    public BotWebhookService(ITelegramBotClient bot) => _bot = bot;

    /// <summary>
    /// Handles one update for the bot identified by <paramref name="botToken"/>.
    /// Returns a short description of what was done (useful for tests/logging).
    /// </summary>
    public async Task<string> HandleUpdateAsync(string botToken, JsonElement update, CancellationToken ct = default)
    {
        if (!update.TryGetProperty("message", out var message))
        {
            return "ignored: no message";
        }

        if (!message.TryGetProperty("text", out var textElement) || textElement.ValueKind != JsonValueKind.String)
        {
            return "ignored: no text";
        }

        var text = textElement.GetString() ?? string.Empty;
        if (!text.StartsWith("/start", StringComparison.Ordinal))
        {
            return "ignored: not /start";
        }

        if (!message.TryGetProperty("from", out var from) ||
            !from.TryGetProperty("id", out var idElement))
        {
            return "ignored: no sender";
        }

        var userId = idElement.GetInt64();
        var chatId = message.TryGetProperty("chat", out var chat) && chat.TryGetProperty("id", out var chatIdEl)
            ? chatIdEl.GetInt64()
            : userId;

        var reply =
            $"Your Telegram ID is {userId}.\n" +
            "Send this number to the group admin to be granted access.";

        await _bot.SendMessageAsync(botToken, chatId, reply, ct);
        return $"replied id {userId}";
    }
}
