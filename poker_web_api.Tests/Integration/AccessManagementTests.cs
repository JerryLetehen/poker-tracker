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
/// Verifies the allow-list management + pending-approval endpoints: admin-only access
/// and correct add/remove/approve behaviour.
/// </summary>
public class AccessManagementTests
{
    private const string PublicBotToken = "111:PUBLIC_BOT";
    private const string AdminBotToken = "222:ADMIN_BOT";

    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = "MgmtTestDb_" + Guid.NewGuid();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("TelegramAuth:AllowDevMockAuth", "false");
            builder.UseSetting("TelegramAuth:PublicBotToken", PublicBotToken);
            builder.UseSetting("TelegramAuth:AdminBotToken", AdminBotToken);
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
                var ctx = services.SingleOrDefault(d => d.ServiceType == typeof(PokerDbContext));
                if (ctx != null) services.Remove(ctx);
                services.AddDbContext<PokerDbContext>(o => o.UseInMemoryDatabase(_dbName));
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

        public T Query<T>(Func<PokerDbContext, T> query)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PokerDbContext>();
            return query(db);
        }
    }

    private static string Sign(string botToken, long userId)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["user"] = $"{{\"id\":{userId},\"first_name\":\"T\"}}",
        };
        var dcs = string.Join('\n', fields.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        using var sh = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var key = sh.ComputeHash(Encoding.UTF8.GetBytes(botToken));
        using var fh = new HMACSHA256(key);
        var hash = Convert.ToHexStringLower(fh.ComputeHash(Encoding.UTF8.GetBytes(dcs)));
        return string.Join('&', fields.Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}").Append($"hash={hash}"));
    }

    private static HttpRequestMessage AdminReq(HttpMethod method, string path, long userId, object? body = null)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-App", "admin");
        req.Headers.Add("Authorization", "tma " + Sign(AdminBotToken, userId));
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    private static Factory WithAdmin(long adminId)
    {
        var f = new Factory();
        f.Seed(db => db.AccessUsers.Add(new AccessUser { TelegramUserId = adminId, Scope = AccessScope.Admin, AddedAt = DateTimeOffset.UtcNow }));
        return f;
    }

    [Fact]
    public async Task ManagementEndpoints_RequireAdmin()
    {
        using var factory = new Factory();
        // Use id 2 (id 1 is the Testing bootstrap admin, which would be seeded as admin).
        factory.Seed(db => db.AccessUsers.Add(new AccessUser { TelegramUserId = 2, Scope = AccessScope.Public, AddedAt = DateTimeOffset.UtcNow }));
        var client = factory.CreateClient();

        // A public (non-admin) user signed with the public bot hitting an admin mgmt endpoint.
        var req = new HttpRequestMessage(HttpMethod.Get, "/admin/access/users");
        req.Headers.Add("X-App", "public");
        req.Headers.Add("Authorization", "tma " + Sign(PublicBotToken, 2));
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AddUser_RoundTrip()
    {
        using var factory = WithAdmin(100);
        var client = factory.CreateClient();

        var add = await client.SendAsync(AdminReq(HttpMethod.Post, "/admin/access/users", 100,
            new { telegramUserId = 555, scope = "public", label = "Friend" }));
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        var list = await client.SendAsync(AdminReq(HttpMethod.Get, "/admin/access/users", 100));
        var users = await list.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.Contains(users!, u => u.TelegramUserId == 555 && u.Scope == "public" && u.Label == "Friend");
    }

    [Fact]
    public async Task RemoveUser_Works()
    {
        using var factory = WithAdmin(100);
        factory.Seed(db => db.AccessUsers.Add(new AccessUser { TelegramUserId = 777, Scope = AccessScope.Public, AddedAt = DateTimeOffset.UtcNow }));
        var client = factory.CreateClient();

        var id = factory.Query(db => db.AccessUsers.Single(u => u.TelegramUserId == 777).Id);
        var del = await client.SendAsync(AdminReq(HttpMethod.Delete, $"/admin/access/users/{id}", 100));
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        Assert.False(factory.Query(db => db.AccessUsers.Any(u => u.TelegramUserId == 777)));
    }

    [Fact]
    public async Task ApprovePending_MovesToAllowList()
    {
        using var factory = WithAdmin(100);
        factory.Seed(db => db.PendingAccessRequests.Add(new PendingAccessRequest
        {
            TelegramUserId = 909, FirstName = "Jo", FirstSeenAt = DateTimeOffset.UtcNow, LastSeenAt = DateTimeOffset.UtcNow
        }));
        var client = factory.CreateClient();

        var pendingId = factory.Query(db => db.PendingAccessRequests.Single(r => r.TelegramUserId == 909).Id);
        var approve = await client.SendAsync(AdminReq(HttpMethod.Post, $"/admin/access/pending/{pendingId}/approve", 100, new { scope = "public" }));
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        Assert.True(factory.Query(db => db.AccessUsers.Any(u => u.TelegramUserId == 909 && u.Scope == AccessScope.Public)));
        Assert.False(factory.Query(db => db.PendingAccessRequests.Any(r => r.TelegramUserId == 909)));
    }

    [Fact]
    public async Task ApprovePending_SetsLabel_FromNameAndUsername()
    {
        using var factory = WithAdmin(100);
        factory.Seed(db => db.PendingAccessRequests.Add(new PendingAccessRequest
        {
            TelegramUserId = 321, FirstName = "Ruslan", LastName = "Dudnikov", Username = "JerryLetehen",
            FirstSeenAt = DateTimeOffset.UtcNow, LastSeenAt = DateTimeOffset.UtcNow
        }));
        var client = factory.CreateClient();

        var pendingId = factory.Query(db => db.PendingAccessRequests.Single(r => r.TelegramUserId == 321).Id);
        await client.SendAsync(AdminReq(HttpMethod.Post, $"/admin/access/pending/{pendingId}/approve", 100, new { scope = "public" }));

        var label = factory.Query(db => db.AccessUsers.Single(u => u.TelegramUserId == 321).Label);
        Assert.Equal("Ruslan Dudnikov (@JerryLetehen)", label);
    }

    [Fact]
    public async Task ApprovePending_UsernameOnly_WhenNoName()
    {
        using var factory = WithAdmin(100);
        factory.Seed(db => db.PendingAccessRequests.Add(new PendingAccessRequest
        {
            TelegramUserId = 322, FirstName = null, LastName = null, Username = "soloname",
            FirstSeenAt = DateTimeOffset.UtcNow, LastSeenAt = DateTimeOffset.UtcNow
        }));
        var client = factory.CreateClient();

        var pendingId = factory.Query(db => db.PendingAccessRequests.Single(r => r.TelegramUserId == 322).Id);
        await client.SendAsync(AdminReq(HttpMethod.Post, $"/admin/access/pending/{pendingId}/approve", 100, new { scope = "public" }));

        var label = factory.Query(db => db.AccessUsers.Single(u => u.TelegramUserId == 322).Label);
        Assert.Equal("@soloname", label);
    }

    [Fact]
    public async Task AddUser_InvalidScope_BadRequest()
    {
        using var factory = WithAdmin(100);
        var client = factory.CreateClient();

        var resp = await client.SendAsync(AdminReq(HttpMethod.Post, "/admin/access/users", 100,
            new { telegramUserId = 5, scope = "superuser", label = (string?)null }));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    private sealed record UserDto(int Id, long TelegramUserId, string Scope, string? Label, DateTimeOffset AddedAt);
}
