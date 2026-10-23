using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Tests.Integration;

public class AdminEndpointsTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebAppFactory _factory;

    public AdminEndpointsTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private PokerDbContext GetDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PokerDbContext>();
    }

    // === CREATE GAME ===

    [Fact]
    public async Task CreateGame_ValidRequest_ReturnsGameWithId()
    {
        var response = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateGameResponse>();
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(50m, result.BuyInAmount);
    }

    [Fact]
    public async Task CreateGame_NullGameType_DefaultsToTexasHoldem()
    {
        var response = await _client.PostAsJsonAsync("/admin/games", new { gameType = (string?)null, buyInAmount = 25 });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateGameResponse>();
        Assert.Equal("texas-holdem-no-limit", result!.GameType);
    }

    [Fact]
    public async Task CreateGame_TournamentType_Accepted()
    {
        var response = await _client.PostAsJsonAsync("/admin/games", new { gameType = "tournament", buyInAmount = 15 });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateGameResponse>();
        Assert.Equal("tournament", result!.GameType);
    }

    // === CREATE PLAYER ===

    [Fact]
    public async Task CreatePlayer_ValidName_ReturnsPlayerWithId()
    {
        var response = await _client.PostAsJsonAsync("/admin/players", new { name = "TestPlayer" });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreatePlayerResponse>();
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("TestPlayer", result.Name);
    }

    [Fact]
    public async Task CreatePlayer_EmptyName_StillCreates()
    {
        var response = await _client.PostAsJsonAsync("/admin/players", new { name = "" });
        // Depending on validation — currently no validation, so it creates
        response.EnsureSuccessStatusCode();
    }

    // === RECORD BUY-IN ===

    [Fact]
    public async Task RecordBuyIn_ValidRequest_CreatesEntry()
    {
        // Create game and player first
        var gameRes = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var game = await gameRes.Content.ReadFromJsonAsync<CreateGameResponse>();

        var playerRes = await _client.PostAsJsonAsync("/admin/players", new { name = "BuyInPlayer" });
        var player = await playerRes.Content.ReadFromJsonAsync<CreatePlayerResponse>();

        // Record buy-in
        var response = await _client.PostAsJsonAsync($"/admin/games/{game!.Id}/buy-in", new { playerId = player!.Id, amount = 50 });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EntryResponse>();
        Assert.True(result!.Id > 0);
    }

    [Fact]
    public async Task RecordBuyIn_NonExistentGame_ReturnsNotFound()
    {
        var playerRes = await _client.PostAsJsonAsync("/admin/players", new { name = "Orphan" });
        var player = await playerRes.Content.ReadFromJsonAsync<CreatePlayerResponse>();

        var response = await _client.PostAsJsonAsync("/admin/games/99999/buy-in", new { playerId = player!.Id, amount = 50 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RecordBuyIn_NonExistentPlayer_ReturnsNotFound()
    {
        var gameRes = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var game = await gameRes.Content.ReadFromJsonAsync<CreateGameResponse>();

        var response = await _client.PostAsJsonAsync($"/admin/games/{game!.Id}/buy-in", new { playerId = 99999, amount = 50 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RecordBuyIn_MultipleBuyIns_AllRecorded()
    {
        var gameRes = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var game = await gameRes.Content.ReadFromJsonAsync<CreateGameResponse>();

        var playerRes = await _client.PostAsJsonAsync("/admin/players", new { name = "MultiBuyIn" });
        var player = await playerRes.Content.ReadFromJsonAsync<CreatePlayerResponse>();

        // 3 buy-ins
        await _client.PostAsJsonAsync($"/admin/games/{game!.Id}/buy-in", new { playerId = player!.Id, amount = 50 });
        await _client.PostAsJsonAsync($"/admin/games/{game.Id}/buy-in", new { playerId = player.Id, amount = 50 });
        await _client.PostAsJsonAsync($"/admin/games/{game.Id}/buy-in", new { playerId = player.Id, amount = 50 });

        // Check via admin game detail
        var detailRes = await _client.GetAsync($"/admin/games/{game.Id}");
        var detail = await detailRes.Content.ReadFromJsonAsync<AdminGameDetailResponse>();

        var buyIns = detail!.Entries.Where(e => e.Type == "buy-in" && e.PlayerId == player.Id).ToList();
        Assert.Equal(3, buyIns.Count);
    }

    // === RECORD CASH-OUT ===

    [Fact]
    public async Task RecordCashOut_ValidRequest_CreatesEntry()
    {
        var gameRes = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var game = await gameRes.Content.ReadFromJsonAsync<CreateGameResponse>();

        var playerRes = await _client.PostAsJsonAsync("/admin/players", new { name = "CashOutPlayer" });
        var player = await playerRes.Content.ReadFromJsonAsync<CreatePlayerResponse>();

        await _client.PostAsJsonAsync($"/admin/games/{game!.Id}/buy-in", new { playerId = player!.Id, amount = 50 });

        var response = await _client.PostAsJsonAsync($"/admin/games/{game.Id}/cash-out", new { playerId = player.Id, amount = 120 });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RecordCashOut_ZeroAmount_Accepted()
    {
        var gameRes = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var game = await gameRes.Content.ReadFromJsonAsync<CreateGameResponse>();

        var playerRes = await _client.PostAsJsonAsync("/admin/players", new { name = "ZeroCashOut" });
        var player = await playerRes.Content.ReadFromJsonAsync<CreatePlayerResponse>();

        await _client.PostAsJsonAsync($"/admin/games/{game!.Id}/buy-in", new { playerId = player!.Id, amount = 50 });

        var response = await _client.PostAsJsonAsync($"/admin/games/{game.Id}/cash-out", new { playerId = player.Id, amount = 0 });
        response.EnsureSuccessStatusCode();
    }

    // === DELETE ENTRY ===

    [Fact]
    public async Task DeleteEntry_ExistingEntry_Succeeds()
    {
        var gameRes = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var game = await gameRes.Content.ReadFromJsonAsync<CreateGameResponse>();

        var playerRes = await _client.PostAsJsonAsync("/admin/players", new { name = "DeleteMe" });
        var player = await playerRes.Content.ReadFromJsonAsync<CreatePlayerResponse>();

        var buyInRes = await _client.PostAsJsonAsync($"/admin/games/{game!.Id}/buy-in", new { playerId = player!.Id, amount = 50 });
        var entry = await buyInRes.Content.ReadFromJsonAsync<EntryResponse>();

        var response = await _client.DeleteAsync($"/admin/game-entries/{entry!.Id}");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DeleteEntry_NonExistentEntry_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/admin/game-entries/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // === DELETE GAME ===

    [Fact]
    public async Task DeleteGame_ExistingGame_DeletesGameAndEntries()
    {
        var gameRes = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var game = await gameRes.Content.ReadFromJsonAsync<CreateGameResponse>();

        var playerRes = await _client.PostAsJsonAsync("/admin/players", new { name = "GameToDelete" });
        var player = await playerRes.Content.ReadFromJsonAsync<CreatePlayerResponse>();

        await _client.PostAsJsonAsync($"/admin/games/{game!.Id}/buy-in", new { playerId = player!.Id, amount = 50 });
        await _client.PostAsJsonAsync($"/admin/games/{game.Id}/cash-out", new { playerId = player.Id, amount = 80 });

        var response = await _client.DeleteAsync($"/admin/games/{game.Id}");
        response.EnsureSuccessStatusCode();

        // Verify game is gone
        var getResponse = await _client.GetAsync($"/admin/games/{game.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteGame_NonExistentGame_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/admin/games/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // === RENAME PLAYER ===

    [Fact]
    public async Task RenamePlayer_ValidRequest_UpdatesName()
    {
        var playerRes = await _client.PostAsJsonAsync("/admin/players", new { name = "OldName" });
        var player = await playerRes.Content.ReadFromJsonAsync<CreatePlayerResponse>();

        var response = await _client.PutAsJsonAsync($"/admin/players/{player!.Id}", new { name = "NewName" });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreatePlayerResponse>();
        Assert.Equal("NewName", result!.Name);
    }

    [Fact]
    public async Task RenamePlayer_NonExistentPlayer_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync("/admin/players/99999", new { name = "Ghost" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // === GET PLAYERS WITH IDS ===

    [Fact]
    public async Task GetPlayersWithIds_ReturnsSortedByName()
    {
        await _client.PostAsJsonAsync("/admin/players", new { name = "Zebra" });
        await _client.PostAsJsonAsync("/admin/players", new { name = "Alpha" });

        var response = await _client.GetAsync("/admin/players");
        var players = await response.Content.ReadFromJsonAsync<List<CreatePlayerResponse>>();

        Assert.NotNull(players);
        // Verify sorted (Alpha before Zebra)
        var alphaIdx = players.FindIndex(p => p.Name == "Alpha");
        var zebraIdx = players.FindIndex(p => p.Name == "Zebra");
        Assert.True(alphaIdx < zebraIdx);
    }

    // === RESPONSE DTOs for deserialization ===
    private record CreateGameResponse(int Id, string Date, string GameType, decimal BuyInAmount);
    private record CreatePlayerResponse(int Id, string Name);
    private record EntryResponse(int Id);
    private record AdminGameDetailResponse(string Date, string GameType, decimal BuyInAmount, decimal Checksum, List<AdminEntryResponse> Entries);
    private record AdminEntryResponse(int Id, string Type, decimal Amount, string PlayerName, int PlayerId);
}
