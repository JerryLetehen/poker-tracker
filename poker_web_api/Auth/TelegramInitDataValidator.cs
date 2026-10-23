using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace poker_web_api.Auth;

/// <summary>
/// Validates Telegram Mini App <c>initData</c> payloads.
///
/// Algorithm (per Telegram's documented scheme):
///   1. Parse initData as application/x-www-form-urlencoded key/value pairs.
///   2. Remove the "hash" field; build the data-check-string from the remaining
///      pairs as "key=value" lines, sorted by key, joined with '\n'.
///   3. secret_key = HMAC_SHA256(key="WebAppData", message=bot_token)
///   4. computed = HMAC_SHA256(key=secret_key, message=data_check_string)
///   5. Valid if computed (hex) equals the provided "hash" (constant-time compare).
///   6. Additionally reject payloads older than the configured max age (auth_date).
/// </summary>
public interface ITelegramInitDataValidator
{
    /// <summary>
    /// Validates <paramref name="initData"/> against <paramref name="botToken"/>.
    /// </summary>
    InitDataValidationResult Validate(string initData, string botToken);
}

public sealed class TelegramInitDataValidator : ITelegramInitDataValidator
{
    private const string WebAppDataKey = "WebAppData";

    private readonly int _maxAgeSeconds;
    private readonly TimeProvider _timeProvider;

    public TelegramInitDataValidator(int maxAgeSeconds, TimeProvider? timeProvider = null)
    {
        _maxAgeSeconds = maxAgeSeconds;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public InitDataValidationResult Validate(string initData, string botToken)
    {
        if (string.IsNullOrWhiteSpace(initData))
        {
            return InitDataValidationResult.Invalid("initData is empty.");
        }

        if (string.IsNullOrWhiteSpace(botToken))
        {
            return InitDataValidationResult.Invalid("Bot token is not configured.");
        }

        // Parse as query-string pairs. We must preserve the raw (decoded) values
        // exactly, so we parse manually rather than relying on a map that could
        // reorder or re-encode.
        var pairs = ParsePairs(initData);

        if (!pairs.TryGetValue("hash", out var providedHash) || string.IsNullOrEmpty(providedHash))
        {
            return InitDataValidationResult.Invalid("initData has no hash field.");
        }

        // Build data-check-string from all fields except hash, sorted by key.
        var dataCheckString = string.Join(
            '\n',
            pairs
                .Where(kvp => kvp.Key != "hash")
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .Select(kvp => $"{kvp.Key}={kvp.Value}"));

        var secretKey = HmacSha256(Encoding.UTF8.GetBytes(WebAppDataKey), Encoding.UTF8.GetBytes(botToken));
        var computed = HmacSha256(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
        var computedHex = Convert.ToHexStringLower(computed);

        if (!FixedTimeEquals(computedHex, providedHash))
        {
            return InitDataValidationResult.Invalid("initData hash mismatch (tampered or wrong bot token).");
        }

        // Freshness check using auth_date (unix seconds).
        if (!pairs.TryGetValue("auth_date", out var authDateRaw)
            || !long.TryParse(authDateRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var authDateUnix))
        {
            return InitDataValidationResult.Invalid("initData has no valid auth_date.");
        }

        var nowUnix = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var ageSeconds = nowUnix - authDateUnix;
        if (ageSeconds > _maxAgeSeconds)
        {
            return InitDataValidationResult.Invalid($"initData expired (age {ageSeconds}s > max {_maxAgeSeconds}s).");
        }
        // Small tolerance for clock skew on the "future" side.
        if (ageSeconds < -300)
        {
            return InitDataValidationResult.Invalid("initData auth_date is in the future.");
        }

        var user = ParseUser(pairs);
        if (user is null)
        {
            return InitDataValidationResult.Invalid("initData has no user field.");
        }

        return InitDataValidationResult.Valid(user);
    }

    private static Dictionary<string, string> ParsePairs(string initData)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in initData.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq < 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(part[..eq]);
            var value = Uri.UnescapeDataString(part[(eq + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private static TelegramUser? ParseUser(IReadOnlyDictionary<string, string> pairs)
    {
        if (!pairs.TryGetValue("user", out var userJson) || string.IsNullOrWhiteSpace(userJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(userJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("id", out var idElement))
            {
                return null;
            }

            var id = idElement.ValueKind == JsonValueKind.Number
                ? idElement.GetInt64()
                : long.Parse(idElement.GetString() ?? "0", CultureInfo.InvariantCulture);

            return new TelegramUser(
                Id: id,
                FirstName: GetStringOrNull(root, "first_name"),
                LastName: GetStringOrNull(root, "last_name"),
                Username: GetStringOrNull(root, "username"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetStringOrNull(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static byte[] HmacSha256(byte[] key, byte[] message)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(message);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(a),
            Encoding.ASCII.GetBytes(b.ToLowerInvariant()));
}
