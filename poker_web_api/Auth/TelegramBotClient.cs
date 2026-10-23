using System.Net.Http.Json;

namespace poker_web_api.Auth;

/// <summary>
/// Minimal client for the Telegram Bot HTTP API — just enough to reply to users.
/// Used by the bot webhook to answer /start with the user's numeric id.
/// </summary>
public interface ITelegramBotClient
{
    Task SendMessageAsync(string botToken, long chatId, string text, CancellationToken ct = default);
}

public sealed class TelegramBotClient : ITelegramBotClient
{
    private readonly HttpClient _http;

    public TelegramBotClient(HttpClient http) => _http = http;

    public async Task SendMessageAsync(string botToken, long chatId, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(botToken))
        {
            return;
        }

        var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
        using var response = await _http.PostAsJsonAsync(
            url,
            new { chat_id = chatId, text },
            ct);
        // Best-effort: we don't throw on failure so a bad reply can't break the webhook.
        response.Dispose();
    }
}
