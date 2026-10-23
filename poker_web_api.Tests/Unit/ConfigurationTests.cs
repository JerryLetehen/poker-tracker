using Microsoft.Extensions.Configuration;
using poker_web_api.Configuration;

namespace poker_web_api.Tests.Unit;

public class ConfigurationTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Binds_TelegramAuth_From_EnvStyle_Keys()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["TelegramAuth:PublicBotToken"] = "pub-token",
            ["TelegramAuth:AdminBotToken"] = "admin-token",
            ["TelegramAuth:InitDataMaxAgeSeconds"] = "3600",
            ["TelegramAuth:AllowDevMockAuth"] = "true",
            ["TelegramAuth:DevMockUserId"] = "42",
        });

        var options = new TelegramAuthOptions();
        config.GetSection(TelegramAuthOptions.SectionName).Bind(options);

        Assert.Equal("pub-token", options.PublicBotToken);
        Assert.Equal("admin-token", options.AdminBotToken);
        Assert.Equal(3600, options.InitDataMaxAgeSeconds);
        Assert.True(options.AllowDevMockAuth);
        Assert.Equal(42, options.DevMockUserId);
    }

    [Fact]
    public void BootstrapAdminIds_CommaSeparated_Override_Is_Applied()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["TelegramAuth:BootstrapAdminIds"] = "111, 222 ;333",
        });

        var options = new TelegramAuthOptions();
        ConfigurationHelpers.ApplyBootstrapAdminIdsOverride(config, options);

        Assert.Equal(new List<long> { 111, 222, 333 }, options.BootstrapAdminIds);
    }

    [Fact]
    public void BootstrapAdminIds_IndexedArray_Binds_Through_Standard_Binder()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["TelegramAuth:BootstrapAdminIds:0"] = "555",
            ["TelegramAuth:BootstrapAdminIds:1"] = "666",
        });

        var options = new TelegramAuthOptions();
        config.GetSection(TelegramAuthOptions.SectionName).Bind(options);
        // Scalar override should be a no-op since there is no scalar value.
        ConfigurationHelpers.ApplyBootstrapAdminIdsOverride(config, options);

        Assert.Equal(new List<long> { 555, 666 }, options.BootstrapAdminIds);
    }

    [Fact]
    public void Cors_AllowedOrigins_Binds_From_Indexed_Keys()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://a.example.com",
            ["Cors:AllowedOrigins:1"] = "https://b.example.com",
        });

        var options = new CorsOptions();
        config.GetSection(CorsOptions.SectionName).Bind(options);

        Assert.Equal(
            new List<string> { "https://a.example.com", "https://b.example.com" },
            options.AllowedOrigins);
    }

    [Fact]
    public void Validation_ProductionLike_Fails_When_Secrets_Missing()
    {
        var auth = new TelegramAuthOptions(); // all empty
        var cors = new CorsOptions();

        var errors = ConfigurationValidator.Validate(auth, cors, isProductionLike: true);

        Assert.Contains(errors, e => e.Contains("PublicBotToken"));
        Assert.Contains(errors, e => e.Contains("AdminBotToken"));
        Assert.Contains(errors, e => e.Contains("BootstrapAdminIds"));
        Assert.Contains(errors, e => e.Contains("AllowedOrigins"));
    }

    [Fact]
    public void Validation_ProductionLike_Fails_When_DevMockAuth_Enabled()
    {
        var auth = new TelegramAuthOptions
        {
            PublicBotToken = "p",
            AdminBotToken = "a",
            BootstrapAdminIds = new List<long> { 1 },
            AllowDevMockAuth = true,
        };
        var cors = new CorsOptions { AllowedOrigins = new List<string> { "https://x" } };

        var errors = ConfigurationValidator.Validate(auth, cors, isProductionLike: true);

        Assert.Contains(errors, e => e.Contains("AllowDevMockAuth"));
    }

    [Fact]
    public void Validation_ProductionLike_Passes_When_Complete()
    {
        var auth = new TelegramAuthOptions
        {
            PublicBotToken = "p",
            AdminBotToken = "a",
            BootstrapAdminIds = new List<long> { 123 },
        };
        var cors = new CorsOptions { AllowedOrigins = new List<string> { "https://x.example.com" } };

        var errors = ConfigurationValidator.Validate(auth, cors, isProductionLike: true);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validation_Development_Allows_Missing_Secrets_And_DevMock()
    {
        var auth = new TelegramAuthOptions { AllowDevMockAuth = true };
        var cors = new CorsOptions();

        var errors = ConfigurationValidator.Validate(auth, cors, isProductionLike: false);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateOrThrow_Throws_With_Aggregated_Messages()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigurationValidator.ValidateOrThrow(
                new TelegramAuthOptions(), new CorsOptions(), isProductionLike: true));

        Assert.Contains("PublicBotToken", ex.Message);
        Assert.Contains("AllowedOrigins", ex.Message);
    }
}
