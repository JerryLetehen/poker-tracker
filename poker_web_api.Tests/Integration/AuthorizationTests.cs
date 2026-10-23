using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Tests.Integration;

/// <summary>
/// Exercises the real auth/authorization pipeline (mock auth disabled) to prove that
/// the manual allow-list gating returns the correct status codes for each scenario.
/// </summary>
public class AuthorizationTests
{
    private const string PublicBotToken = "111:PUBLIC_BOT";
    private const string AdminBotToken = "222:ADMIN_BOT";

    private sealed class RealAuthFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = "AuthTestDb_" + Guid.NewGuid();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("TelegramAuth:AllowDevMockAuth", "false");
            builder.UseSetting("TelegramAuth:PublicBotToken", PublicBotToken);
            builder.UseSetting("TelegramAuth:AdminBotToken", AdminBotToken);
            builder.UseSetting("TelegramAuth:InitDataMaxAgeSeconds", "86400");
            // No bootstrap admins — we manage the allow-list directly in each test.
            builder.UseSetting("TelegramAuth:BootstrapAdminIds", "");

            builder.ConfigureServices(services =>
            {
                var toRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<PokerDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                    .ToList();
                foreach (var d in toRemove) services.Remove(d);

                var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(PokerDbContext));
                if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

                services.AddDbContext<PokerDbContext>(options => options.UseInMemoryDatabase(_dbName));
            });

            builder.UseEnvironment("Testing");
        }

        public void Seed(Action<PokerDbContext> seed)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PokerDbContext>();
            seed(db);
            db.SaveChanges();
        }

        public int CountPending()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PokerDbContext>();
            return db.PendingAccessRequests.Count();
        }
    }

    private static string SignInitData(string botToken, long userId, string firstName = "Test")
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["user"] = $"{{\"id\":{userId},\"first_name\":\"{firstName}\"}}",
        };
        var dataCheckString = string.Join('\n', fields.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        using var secretHmac = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secretKey = secretHmac.ComputeHash(Encoding.UTF8.GetBytes(botToken));
        using var finalHmac = new HMACSHA256(secretKey);
        var hash = Convert.ToHexStringLower(finalHmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString)));
        var parts = fields.Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}").Append($"hash={hash}");
        return string.Join('&', parts);
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string app, string? initData)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-App", app);
        if (initData is not null)
        {
            req.Headers.Add("Authorization", "tma " + initData);
        }
        return req;
    }

    [Fact]
    public async Task NoInitData_Returns401()
    {
        using var factory = new RealAuthFactory();
        var client = factory.CreateClient();

        var resp = await client.SendAsync(Request(HttpMethod.Get, "/player-list", "public", initData: null));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task InvalidInitData_Returns401()
    {
        using var factory = new RealAuthFactory();
        var client = factory.CreateClient();

        var resp = await client.SendAsync(Request(HttpMethod.Get, "/player-list", "public", initData: "garbage&hash=deadbeef"));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ValidUser_NotOnList_PublicEndpoint_Returns403Pending_AndRecordsPending()
    {
        using var factory = new RealAuthFactory();
        var client = factory.CreateClient();

        var initData = SignInitData(PublicBotToken, userId: 500);
        var resp = await client.SendAsync(Request(HttpMethod.Get, "/player-list", "public", initData));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("pending", body!.Reason);
        Assert.Equal(500, body.TelegramUserId);
        Assert.Equal(1, factory.CountPending());

        // Repeat attempt should not create a duplicate pending record.
        await client.SendAsync(Request(HttpMethod.Get, "/player-list", "public", SignInitData(PublicBotToken, 500)));
        Assert.Equal(1, factory.CountPending());
    }

    [Fact]
    public async Task PublicUser_CanUsePublic_ButNotAdmin()
    {
        using var factory = new RealAuthFactory();
        factory.Seed(db => db.AccessUsers.Add(new AccessUser
        {
            TelegramUserId = 600, Scope = AccessScope.Public, AddedAt = DateTimeOffset.UtcNow
        }));
        var client = factory.CreateClient();

        var publicResp = await client.SendAsync(
            Request(HttpMethod.Get, "/player-list", "public", SignInitData(PublicBotToken, 600)));
        Assert.Equal(HttpStatusCode.OK, publicResp.StatusCode);

        var adminResp = await client.SendAsync(
            Request(HttpMethod.Get, "/admin/players", "admin", SignInitData(AdminBotToken, 600)));
        Assert.Equal(HttpStatusCode.Forbidden, adminResp.StatusCode);
        var body = await adminResp.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("denied", body!.Reason);
    }

    [Fact]
    public async Task AdminUser_CanUseAdminAndPublic()
    {
        using var factory = new RealAuthFactory();
        factory.Seed(db => db.AccessUsers.Add(new AccessUser
        {
            TelegramUserId = 700, Scope = AccessScope.Admin, AddedAt = DateTimeOffset.UtcNow
        }));
        var client = factory.CreateClient();

        var adminResp = await client.SendAsync(
            Request(HttpMethod.Get, "/admin/players", "admin", SignInitData(AdminBotToken, 700)));
        Assert.Equal(HttpStatusCode.OK, adminResp.StatusCode);

        var publicResp = await client.SendAsync(
            Request(HttpMethod.Get, "/player-list", "public", SignInitData(PublicBotToken, 700)));
        Assert.Equal(HttpStatusCode.OK, publicResp.StatusCode);
    }

    [Fact]
    public async Task AdminInitData_SignedWithPublicToken_FailsValidation()
    {
        using var factory = new RealAuthFactory();
        factory.Seed(db => db.AccessUsers.Add(new AccessUser
        {
            TelegramUserId = 800, Scope = AccessScope.Admin, AddedAt = DateTimeOffset.UtcNow
        }));
        var client = factory.CreateClient();

        // Signed with the wrong (public) token but sent as the admin app → 401.
        var resp = await client.SendAsync(
            Request(HttpMethod.Get, "/admin/players", "admin", SignInitData(PublicBotToken, 800)));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    private sealed record ErrorBody(string Reason, string Message, long? TelegramUserId);
}
