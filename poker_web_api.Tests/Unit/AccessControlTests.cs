using poker_web_api.Auth;
using poker_web_api.Configuration;
using poker_web_api.Entities;

namespace poker_web_api.Tests.Unit;

public class AccessControlTests
{
    [Theory]
    [InlineData(AccessDecision.Admin, AccessScope.Admin, true)]
    [InlineData(AccessDecision.Admin, AccessScope.Public, true)]
    [InlineData(AccessDecision.Public, AccessScope.Public, true)]
    [InlineData(AccessDecision.Public, AccessScope.Admin, false)]
    [InlineData(AccessDecision.Pending, AccessScope.Public, false)]
    [InlineData(AccessDecision.Pending, AccessScope.Admin, false)]
    public void Satisfies_Matrix(AccessDecision decision, AccessScope required, bool expected)
    {
        Assert.Equal(expected, AccessControlService.Satisfies(decision, required));
    }

    [Fact]
    public async Task Resolve_ReturnsPending_ForUnknownUser()
    {
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = new AccessControlService(db);

        Assert.Equal(AccessDecision.Pending, await service.ResolveAsync(999));
    }

    [Fact]
    public async Task Resolve_ReturnsScope_ForKnownUser()
    {
        using var db = TestDbHelper.CreateInMemoryDb();
        db.AccessUsers.Add(new AccessUser { TelegramUserId = 10, Scope = AccessScope.Public, AddedAt = DateTimeOffset.UtcNow });
        db.AccessUsers.Add(new AccessUser { TelegramUserId = 20, Scope = AccessScope.Admin, AddedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var service = new AccessControlService(db);

        Assert.Equal(AccessDecision.Public, await service.ResolveAsync(10));
        Assert.Equal(AccessDecision.Admin, await service.ResolveAsync(20));
    }

    [Fact]
    public async Task BootstrapSeeder_AddsAdmin_OnFreshDb()
    {
        using var db = TestDbHelper.CreateInMemoryDb();
        var options = new TelegramAuthOptions { BootstrapAdminIds = new List<long> { 42, 43 } };

        await BootstrapAdminSeeder.SeedAsync(db, options);

        var admins = db.AccessUsers.Where(u => u.Scope == AccessScope.Admin).Select(u => u.TelegramUserId).ToList();
        Assert.Contains(42L, admins);
        Assert.Contains(43L, admins);
    }

    [Fact]
    public async Task BootstrapSeeder_IsIdempotent_AndPromotesExisting()
    {
        using var db = TestDbHelper.CreateInMemoryDb();
        db.AccessUsers.Add(new AccessUser { TelegramUserId = 42, Scope = AccessScope.Public, AddedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var options = new TelegramAuthOptions { BootstrapAdminIds = new List<long> { 42 } };

        await BootstrapAdminSeeder.SeedAsync(db, options);
        await BootstrapAdminSeeder.SeedAsync(db, options); // run twice

        var entries = db.AccessUsers.Where(u => u.TelegramUserId == 42).ToList();
        Assert.Single(entries);
        Assert.Equal(AccessScope.Admin, entries[0].Scope);
    }

    [Fact]
    public async Task PendingService_Upserts_WithoutDuplicates()
    {
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = new PendingAccessService(db);

        var created = await service.RecordAsync(new TelegramUser(5, "A", null, "a"));
        var createdAgain = await service.RecordAsync(new TelegramUser(5, "A-updated", null, "a"));

        Assert.True(created);
        Assert.False(createdAgain);
        Assert.Single(db.PendingAccessRequests);
        Assert.Equal("A-updated", db.PendingAccessRequests.Single().FirstName);
    }
}
