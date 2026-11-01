using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace poker_web_api.Auth;

/// <summary>
/// Minimal client for the Telegram Bot HTTP API — just enough to reply to users and
/// publish game results. Used by the bot webhook and the publish-results endpoint.
/// </summary>
public interface ITelegramBotClient
{
    Task SendMessageAsync(string botToken, long chatId, string text, CancellationToken ct = default);

    /// <summary>
    /// Sends a text message to a chat identified by a string chat id (e.g. a group id).
    /// Optionally disables link previews. Returns true when Telegram reports ok so the
    /// caller can surface success/failure.
    /// </summary>
    Task<bool> SendTextAsync(
        string botToken,
        string chatId,
        string text,
        bool disableWebPagePreview = true,
        string? parseMode = null,
        CancellationToken ct = default);

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

    public async Task<bool> SendTextAsync(
        string botToken,
        string chatId,
        string text,
        bool disableWebPagePreview = true,
        string? parseMode = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId) || string.IsNullOrEmpty(text))
        {
            return false;
        }

        var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

        try
        {
            using var response = await _http.PostAsJsonAsync(
                url,
                new
                {
                    chat_id = chatId,
                    text,
                    disable_web_page_preview = disableWebPagePreview,
                    parse_mode = parseMode,
                },
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return doc.RootElement.TryGetProperty("ok", out var ok)
                && ok.ValueKind == JsonValueKind.True;
        }
        catch (Exception)
        {
            // Consistent with SendMessageAsync: a transport failure must not throw.
            return false;
        }
    }
}
