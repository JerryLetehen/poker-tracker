using System.Net;
using System.Net.Http.Json;

namespace poker_web_api.Tests.Integration;

// Endpoint tests for GET /leaderboard (dual-leaderboard feature, Task 7).
// Uses the same TestWebAppFactory + HttpClient pattern as PublicEndpointsTests:
// seed data through the admin endpoints, then exercise the public /leaderboard endpoint.
//
// The endpoint now returns a LeaderboardsResponse object: { "active": [...], "all": [...] },
// where each row carries { name, games, result, rate }.
//
// Seeding note: CreateGame always dates games as "today", so every seeded game falls inside
// the Active 12-month window. The Active list uses a fixed threshold of 10 distinct games, so
// these tests focus their row-level assertions on the All list (whose threshold is the
// request's minGames) and only assert that the Active array is present (non-null).
public class LeaderboardEndpointTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public LeaderboardEndpointTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    // Seeds one complete game (buy-in then cash-out) for a player and returns nothing.
    private async Task SeedGameAsync(int playerId, decimal buyIn, decimal cashOut)
    {
        var gameRes = await _client.PostAsJsonAsync("/admin/games",
            new { gameType = "texas-holdem-no-limit", buyInAmount = buyIn });
        gameRes.EnsureSuccessStatusCode();
        var game = await gameRes.Content.ReadFromJsonAsync<GameResponse>();

        await _client.PostAsJsonAsync($"/admin/games/{game!.Id}/buy-in",
            new { playerId, amount = buyIn });
        await _client.PostAsJsonAsync($"/admin/games/{game.Id}/cash-out",
            new { playerId, amount = cashOut });
    }

    private async Task<int> CreatePlayerAsync(string name)
    {
        var res = await _client.PostAsJsonAsync("/admin/players", new { name });
        res.EnsureSuccessStatusCode();
        var player = await res.Content.ReadFromJsonAsync<PlayerResponse>();
        return player!.Id;
    }

    // === 7.1 Response shape and row fields (Req 5.1, 5.4) ===

    [Fact]
    public async Task GetLeaderboard_ReturnsBothActiveAndAllArrays()
    {
        var response = await _client.GetAsync("/leaderboard?minGames=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LeaderboardsResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body!.Active);
        Assert.NotNull(body.All);
    }

    [Fact]
    public async Task GetLeaderboard_RowFields_DeserializePopulated()
    {
        // Seed a single player with one game: buy-in 50, cash-out 80 => net +30, rate 30.
        var playerId = await CreatePlayerAsync("ShapePlayer");
        await SeedGameAsync(playerId, buyIn: 50, cashOut: 80);

        // minGames=1 so the single-game player qualifies for the All list.
        var response = await _client.GetAsync("/leaderboard?minGames=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LeaderboardsResponse>();
        Assert.NotNull(body);

        var row = body!.All.FirstOrDefault(r => r.Name == "ShapePlayer");
        Assert.NotNull(row);
        Assert.Equal("ShapePlayer", row!.Name);
        Assert.Equal(1, row.Games);
        Assert.Equal(30m, row.Result);
        Assert.Equal(30.0, row.Rate, 3);
    }

    // === 7.2 Defaults (Req 4.5, 5.6) ===

    [Fact]
    public async Task GetLeaderboard_NoSortBy_DefaultsToRateOrdering()
    {
        // RateLeader: 1 game, net +100 => result 100, rate 100.
        var rateLeader = await CreatePlayerAsync("RateLeaderDefault");
        await SeedGameAsync(rateLeader, buyIn: 50, cashOut: 150);

        // NetLeader: 5 games, each net +40 => result 200 (> 100), rate 40 (< 100).
        var netLeader = await CreatePlayerAsync("NetLeaderDefault");
        for (var i = 0; i < 5; i++)
            await SeedGameAsync(netLeader, buyIn: 50, cashOut: 90);

        // No sortBy => default "rate": higher-rate player (rate 100) precedes lower-rate (rate 40).
        var response = await _client.GetAsync("/leaderboard?minGames=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LeaderboardsResponse>();
        Assert.NotNull(body);

        var rateIdx = body!.All.FindIndex(r => r.Name == "RateLeaderDefault");
        var netIdx = body.All.FindIndex(r => r.Name == "NetLeaderDefault");
        Assert.True(rateIdx >= 0, "RateLeaderDefault should be present in the All list");
        Assert.True(netIdx >= 0, "NetLeaderDefault should be present in the All list");

        // Sanity: ordering by net would put NetLeader first; default rate ordering puts RateLeader first.
        Assert.True(rateIdx < netIdx,
            "With the default rate ordering, the higher-rate player must precede the higher-net player");
    }

    [Fact]
    public async Task GetLeaderboard_NoMinGames_AppliesDefaultThresholdOfFive()
    {
        // FourGamePlayer: exactly 4 distinct games => below the default All threshold (5), excluded.
        var fourGamePlayer = await CreatePlayerAsync("FourGamePlayer");
        for (var i = 0; i < 4; i++)
            await SeedGameAsync(fourGamePlayer, buyIn: 50, cashOut: 60);

        // FiveGamePlayer: exactly 5 distinct games => meets the default threshold, included.
        var fiveGamePlayer = await CreatePlayerAsync("FiveGamePlayer");
        for (var i = 0; i < 5; i++)
            await SeedGameAsync(fiveGamePlayer, buyIn: 50, cashOut: 60);

        // No minGames query param => default All threshold is 5.
        var response = await _client.GetAsync("/leaderboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LeaderboardsResponse>();
        Assert.NotNull(body);

        Assert.DoesNotContain(body!.All, r => r.Name == "FourGamePlayer");
        Assert.Contains(body.All, r => r.Name == "FiveGamePlayer");
    }

    // === Active stats are all-time: identical rows in both lists (Req 2.1, 2.3, 2.4) ===

    [Fact]
    public async Task GetLeaderboard_ActiveEligiblePlayer_ShowsIdenticalAllTimeStatsInBothLists()
    {
        // Admin-created games are always dated today, so all 10 games fall inside the Active window
        // and the player is Active-eligible (>= 10 distinct games). Because Active stats are all-time,
        // the player's Games/Result/Rate must be identical in both the active and all arrays.
        var playerId = await CreatePlayerAsync("DualListPlayer");
        for (var i = 0; i < 10; i++)
            await SeedGameAsync(playerId, buyIn: 50, cashOut: 60); // net +10 each => result 100

        var response = await _client.GetAsync("/leaderboard?minGames=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LeaderboardsResponse>();
        Assert.NotNull(body);

        var activeRow = body!.Active.FirstOrDefault(r => r.Name == "DualListPlayer");
        var allRow = body.All.FirstOrDefault(r => r.Name == "DualListPlayer");
        Assert.NotNull(activeRow);
        Assert.NotNull(allRow);

        // All-time stats: 10 games, result +100, rate +10 — identical across both lists.
        Assert.Equal(10, activeRow!.Games);
        Assert.Equal(100m, activeRow.Result);
        Assert.Equal(10.0, activeRow.Rate, 3);
        Assert.Equal(allRow!.Games, activeRow.Games);
        Assert.Equal(allRow.Result, activeRow.Result);
        Assert.Equal(allRow.Rate, activeRow.Rate, 3);
    }

    // === 7.3 Invalid-parameter rejection (Req 5.3, 5.7) ===

    [Fact]
    public async Task GetLeaderboard_InvalidSortBy_Returns400()
    {
        var response = await _client.GetAsync("/leaderboard?sortBy=bogus");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // No valid leaderboard lists are returned on the reject path.
        var body = await response.Content.ReadFromJsonAsync<LeaderboardsResponse>();
        var hasLists = body is { Active: not null, All: not null };
        Assert.False(hasLists, "A 400 response must not carry valid active/all lists");
    }

    [Theory]
    [InlineData("/leaderboard?minGames=0")]
    [InlineData("/leaderboard?minGames=-3")]
    [InlineData("/leaderboard?minGames=abc")]
    public async Task GetLeaderboard_InvalidMinGames_Returns400(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // === Response DTOs ===
    private record LeaderboardsResponse(List<LeaderboardRow> Active, List<LeaderboardRow> All);
    private record LeaderboardRow(string Name, int Games, decimal Result, double Rate);
    private record PlayerResponse(int Id, string Name);
    private record GameResponse(int Id, string Date, string GameType, decimal BuyInAmount);
}
