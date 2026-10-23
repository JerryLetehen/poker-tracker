using System.Security.Cryptography;
using System.Text;
using System.Web;
using poker_web_api.Auth;

namespace poker_web_api.Tests.Unit;

public class TelegramInitDataValidatorTests
{
    private const string BotToken = "123456:TEST_BOT_TOKEN";

    /// <summary>
    /// Builds a correctly-signed initData string the same way Telegram does, so we can
    /// assert the validator accepts genuine payloads and rejects tampered ones.
    /// </summary>
    private static string BuildSignedInitData(
        string botToken,
        long userId,
        long authDateUnix,
        string firstName = "Alice",
        string? username = "alice")
    {
        var userJson = username is null
            ? $"{{\"id\":{userId},\"first_name\":\"{firstName}\"}}"
            : $"{{\"id\":{userId},\"first_name\":\"{firstName}\",\"username\":\"{username}\"}}";

        // Fields that go into the data-check-string (everything except hash).
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = authDateUnix.ToString(),
            ["query_id"] = "AAE",
            ["user"] = userJson,
        };

        var dataCheckString = string.Join('\n', fields.Select(kvp => $"{kvp.Key}={kvp.Value}"));

        using var secretHmac = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secretKey = secretHmac.ComputeHash(Encoding.UTF8.GetBytes(botToken));
        using var finalHmac = new HMACSHA256(secretKey);
        var hash = Convert.ToHexStringLower(finalHmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString)));

        // Reassemble as url-encoded query string (order does not matter for the validator).
        var parts = fields
            .Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}")
            .Append($"hash={hash}");
        return string.Join('&', parts);
    }

    private static TelegramInitDataValidator NewValidator(int maxAgeSeconds = 86400, DateTimeOffset? now = null)
    {
        var timeProvider = now is null ? TimeProvider.System : new FakeTimeProvider(now.Value);
        return new TelegramInitDataValidator(maxAgeSeconds, timeProvider);
    }

    [Fact]
    public void Valid_InitData_Passes_And_Extracts_User()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var initData = BuildSignedInitData(BotToken, userId: 777, authDateUnix: now.ToUnixTimeSeconds());

        var result = NewValidator(now: now).Validate(initData, BotToken);

        Assert.True(result.IsValid, result.Error);
        Assert.NotNull(result.User);
        Assert.Equal(777, result.User!.Id);
        Assert.Equal("Alice", result.User.FirstName);
        Assert.Equal("alice", result.User.Username);
    }

    [Fact]
    public void Tampered_Field_Fails()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var initData = BuildSignedInitData(BotToken, userId: 777, authDateUnix: now.ToUnixTimeSeconds());

        // Flip the user id in the payload without re-signing.
        var tampered = initData.Replace("%22id%22%3a777", "%22id%22%3a999")
                               .Replace("\"id\":777", "\"id\":999");

        var result = NewValidator(now: now).Validate(tampered, BotToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Wrong_BotToken_Fails()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var initData = BuildSignedInitData(BotToken, userId: 777, authDateUnix: now.ToUnixTimeSeconds());

        var result = NewValidator(now: now).Validate(initData, "999999:OTHER_BOT_TOKEN");

        Assert.False(result.IsValid);
        Assert.Contains("mismatch", result.Error!);
    }

    [Fact]
    public void Expired_InitData_Fails()
    {
        var authDate = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var initData = BuildSignedInitData(BotToken, userId: 777, authDateUnix: authDate.ToUnixTimeSeconds());

        // Now is two hours later; max age is one hour.
        var now = authDate.AddHours(2);
        var result = NewValidator(maxAgeSeconds: 3600, now: now).Validate(initData, BotToken);

        Assert.False(result.IsValid);
        Assert.Contains("expired", result.Error!);
    }

    [Fact]
    public void Missing_Hash_Fails()
    {
        var result = NewValidator().Validate("auth_date=1700000000&user=%7B%22id%22%3A1%7D", BotToken);

        Assert.False(result.IsValid);
        Assert.Contains("hash", result.Error!);
    }

    [Fact]
    public void Empty_InitData_Fails()
    {
        var result = NewValidator().Validate("", BotToken);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_BotToken_Fails()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var initData = BuildSignedInitData(BotToken, userId: 1, authDateUnix: now.ToUnixTimeSeconds());

        var result = NewValidator(now: now).Validate(initData, "");
        Assert.False(result.IsValid);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
