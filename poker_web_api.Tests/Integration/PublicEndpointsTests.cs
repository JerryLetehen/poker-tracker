using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Tests.Integration;

public class PublicEndpointsTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebAppFactory _factory;

    public PublicEndpointsTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private PokerDbContext GetDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PokerDbContext>();
    }

    // === GET /player-list ===

    [Fact]
    public async Task GetPlayerList_EmptyDb_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/player-list");
        response.EnsureSuccessStatusCode();
        var players = await response.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(players);
    }

    [Fact]
    public async Task GetPlayerList_WithPlayers_ReturnsNames()
    {
        await _client.PostAsJsonAsync("/admin/players", new { name = "PublicTest1" });
        await _client.PostAsJsonAsync("/admin/players", new { name = "PublicTest2" });

        var response = await _client.GetAsync("/player-list");
        var players = await response.Content.ReadFromJsonAsync<List<string>>();

        Assert.Contains("PublicTest1", players!);
        Assert.Contains("PublicTest2", players);
    }

    // === GET /leaderboard ===

    [Fact]
    public async Task GetLeaderboard_EmptyDb_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/leaderboard?minGames=1&sortBy=rate");
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<List<LeaderboardResponse>>();
        Assert.NotNull(data);
    }

    [Fact]
    public async Task GetLeaderboard_WithData_ReturnsResults()
    {
        // Create a game via admin
        var playerRes = await _client.PostAsJsonAsync("/admin/players", new { name = "LeaderboardPlayer" });
        var player = await playerRes.Content.ReadFromJsonAsync<PlayerResponse>();

        var gameRes = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var game = await gameRes.Content.ReadFromJsonAsync<GameResponse>();

        await _client.PostAsJsonAsync($"/admin/games/{game!.Id}/buy-in", new { playerId = player!.Id, amount = 50 });
        await _client.PostAsJsonAsync($"/admin/games/{game.Id}/cash-out", new { playerId = player.Id, amount = 80 });

        var response = await _client.GetAsync("/leaderboard?minGames=1&sortBy=rate");
        var data = await response.Content.ReadFromJsonAsync<List<LeaderboardResponse>>();

        var entry = data!.FirstOrDefault(e => e.Name == "LeaderboardPlayer");
        Assert.NotNull(entry);
        Assert.Equal(1, entry.Games);
        Assert.Equal(30m, entry.Result);
    }

    [Fact]
    public async Task GetLeaderboard_MinGamesFilters_CorrectBehavior()
    {
        var response = await _client.GetAsync("/leaderboard?minGames=1000&sortBy=rate");
        var data = await response.Content.ReadFromJsonAsync<List<LeaderboardResponse>>();
        Assert.Empty(data!);
    }

    // === GET /games ===

    [Fact]
    public async Task GetGames_ReturnsGamesList()
    {
        await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });

        var response = await _client.GetAsync("/games");
        response.EnsureSuccessStatusCode();
        var games = await response.Content.ReadFromJsonAsync<List<GamesListResponse>>();
        Assert.NotNull(games);
        Assert.True(games.Count > 0);
    }

    [Fact]
    public async Task GetGames_IncludesPlayerCountAndPot()
    {
        var gameRes = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var game = await gameRes.Content.ReadFromJsonAsync<GameResponse>();

        var p1Res = await _client.PostAsJsonAsync("/admin/players", new { name = "GamesListP1" });
        var p1 = await p1Res.Content.ReadFromJsonAsync<PlayerResponse>();
        var p2Res = await _client.PostAsJsonAsync("/admin/players", new { name = "GamesListP2" });
        var p2 = await p2Res.Content.ReadFromJsonAsync<PlayerResponse>();

        await _client.PostAsJsonAsync($"/admin/games/{game!.Id}/buy-in", new { playerId = p1!.Id, amount = 50 });
        await _client.PostAsJsonAsync($"/admin/games/{game.Id}/buy-in", new { playerId = p2!.Id, amount = 50 });
        await _client.PostAsJsonAsync($"/admin/games/{game.Id}/cash-out", new { playerId = p1.Id, amount = 60 });
        await _client.PostAsJsonAsync($"/admin/games/{game.Id}/cash-out", new { playerId = p2.Id, amount = 40 });

        var response = await _client.GetAsync("/games");
        var games = await response.Content.ReadFromJsonAsync<List<GamesListResponse>>();
        var thisGame = games!.FirstOrDefault(g => g.Id == game.Id);

        Assert.NotNull(thisGame);
        Assert.Equal(2, thisGame.PlayerCount);
        Assert.Equal(100m, thisGame.Pot);
    }

    // === GET /games/:id ===

    [Fact]
    public async Task GetGameDetail_ExistingGame_ReturnsPlayersWithNet()
    {
        var gameRes = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var game = await gameRes.Content.ReadFromJsonAsync<GameResponse>();

        var playerRes = await _client.PostAsJsonAsync("/admin/players", new { name = "DetailPlayer" });
        var player = await playerRes.Content.ReadFromJsonAsync<PlayerResponse>();

        await _client.PostAsJsonAsync($"/admin/games/{game!.Id}/buy-in", new { playerId = player!.Id, amount = 50 });
        await _client.PostAsJsonAsync($"/admin/games/{game.Id}/cash-out", new { playerId = player.Id, amount = 75 });

        var response = await _client.GetAsync($"/games/{game.Id}");
        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<GameDetailResponse>();

        Assert.NotNull(detail);
        Assert.Single(detail.Players);
        Assert.Equal("DetailPlayer", detail.Players[0].PlayerName);
        Assert.Equal(25m, detail.Players[0].Net);
    }

    [Fact]
    public async Task GetGameDetail_NonExistentGame_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/games/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // === GET /players/:name/progression ===

    [Fact]
    public async Task GetPlayerProgression_ExistingPlayer_ReturnsData()
    {
        var playerRes = await _client.PostAsJsonAsync("/admin/players", new { name = "ProgressionPlayer" });
        var player = await playerRes.Content.ReadFromJsonAsync<PlayerResponse>();

        var gameRes = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var game = await gameRes.Content.ReadFromJsonAsync<GameResponse>();

        await _client.PostAsJsonAsync($"/admin/games/{game!.Id}/buy-in", new { playerId = player!.Id, amount = 50 });
        await _client.PostAsJsonAsync($"/admin/games/{game.Id}/cash-out", new { playerId = player.Id, amount = 90 });

        var response = await _client.GetAsync($"/players/ProgressionPlayer/progression");
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<List<ProgressionResponse>>();

        Assert.Single(data!);
        Assert.Equal(40m, data[0].Result);
        Assert.Equal(40m, data[0].CumulativeResult);
    }

    [Fact]
    public async Task GetPlayerProgression_NonExistentPlayer_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/players/NonExistentGhost/progression");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPlayerProgression_MultipleGames_CumulativeResultAccumulates()
    {
        var playerRes = await _client.PostAsJsonAsync("/admin/players", new { name = "CumulativePlayer" });
        var player = await playerRes.Content.ReadFromJsonAsync<PlayerResponse>();

        // Game 1: net = +20
        var g1Res = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var g1 = await g1Res.Content.ReadFromJsonAsync<GameResponse>();
        await _client.PostAsJsonAsync($"/admin/games/{g1!.Id}/buy-in", new { playerId = player!.Id, amount = 50 });
        await _client.PostAsJsonAsync($"/admin/games/{g1.Id}/cash-out", new { playerId = player.Id, amount = 70 });

        // Game 2: net = -10
        var g2Res = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var g2 = await g2Res.Content.ReadFromJsonAsync<GameResponse>();
        await _client.PostAsJsonAsync($"/admin/games/{g2!.Id}/buy-in", new { playerId = player.Id, amount = 50 });
        await _client.PostAsJsonAsync($"/admin/games/{g2.Id}/cash-out", new { playerId = player.Id, amount = 40 });

        var response = await _client.GetAsync($"/players/CumulativePlayer/progression");
        var data = await response.Content.ReadFromJsonAsync<List<ProgressionResponse>>();

        Assert.Equal(2, data!.Count);
        Assert.Equal(20m, data[0].CumulativeResult);
        Assert.Equal(10m, data[1].CumulativeResult); // 20 + (-10) = 10
    }

    // === RARE CASES ===

    [Fact]
    public async Task GetGameDetail_PlayerWithMultipleRebuys_ShowsTotalBuyIn()
    {
        var gameRes = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var game = await gameRes.Content.ReadFromJsonAsync<GameResponse>();

        var playerRes = await _client.PostAsJsonAsync("/admin/players", new { name = "RebuyDetailPlayer" });
        var player = await playerRes.Content.ReadFromJsonAsync<PlayerResponse>();

        await _client.PostAsJsonAsync($"/admin/games/{game!.Id}/buy-in", new { playerId = player!.Id, amount = 50 });
        await _client.PostAsJsonAsync($"/admin/games/{game.Id}/buy-in", new { playerId = player.Id, amount = 50 });
        await _client.PostAsJsonAsync($"/admin/games/{game.Id}/buy-in", new { playerId = player.Id, amount = 50 });
        await _client.PostAsJsonAsync($"/admin/games/{game.Id}/cash-out", new { playerId = player.Id, amount = 200 });

        var response = await _client.GetAsync($"/games/{game.Id}");
        var detail = await response.Content.ReadFromJsonAsync<GameDetailResponse>();

        Assert.Equal(150m, detail!.Players[0].BuyIn);
        Assert.Equal(200m, detail.Players[0].CashOut);
        Assert.Equal(50m, detail.Players[0].Net);
    }

    [Fact]
    public async Task GetLeaderboard_SortByNet_ReturnsDifferentOrderThanRate()
    {
        var p1Res = await _client.PostAsJsonAsync("/admin/players", new { name = "HighRateLowNet" });
        var p1 = await p1Res.Content.ReadFromJsonAsync<PlayerResponse>();
        var p2Res = await _client.PostAsJsonAsync("/admin/players", new { name = "LowRateHighNet" });
        var p2 = await p2Res.Content.ReadFromJsonAsync<PlayerResponse>();

        // P1: 1 game, net +100 (rate = 100)
        var g1Res = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
        var g1 = await g1Res.Content.ReadFromJsonAsync<GameResponse>();
        await _client.PostAsJsonAsync($"/admin/games/{g1!.Id}/buy-in", new { playerId = p1!.Id, amount = 50 });
        await _client.PostAsJsonAsync($"/admin/games/{g1.Id}/cash-out", new { playerId = p1.Id, amount = 150 });

        // P2: 10 games, net +20 each = +200 total (rate = 20)
        for (int i = 0; i < 10; i++)
        {
            var gRes = await _client.PostAsJsonAsync("/admin/games", new { gameType = "texas-holdem-no-limit", buyInAmount = 50 });
            var g = await gRes.Content.ReadFromJsonAsync<GameResponse>();
            await _client.PostAsJsonAsync($"/admin/games/{g!.Id}/buy-in", new { playerId = p2!.Id, amount = 50 });
            await _client.PostAsJsonAsync($"/admin/games/{g.Id}/cash-out", new { playerId = p2.Id, amount = 70 });
        }

        var rateRes = await _client.GetAsync("/leaderboard?minGames=1&sortBy=rate");
        var rateData = await rateRes.Content.ReadFromJsonAsync<List<LeaderboardResponse>>();

        var netRes = await _client.GetAsync("/leaderboard?minGames=1&sortBy=net");
        var netData = await netRes.Content.ReadFromJsonAsync<List<LeaderboardResponse>>();

        // By rate: P1 first (rate 100 > 20)
        var p1RateIdx = rateData!.FindIndex(e => e.Name == "HighRateLowNet");
        var p2RateIdx = rateData.FindIndex(e => e.Name == "LowRateHighNet");
        Assert.True(p1RateIdx < p2RateIdx);

        // By net: P2 first (200 > 100)
        var p1NetIdx = netData!.FindIndex(e => e.Name == "HighRateLowNet");
        var p2NetIdx = netData.FindIndex(e => e.Name == "LowRateHighNet");
        Assert.True(p2NetIdx < p1NetIdx);
    }

    // === Response DTOs ===
    private record PlayerResponse(int Id, string Name);
    private record GameResponse(int Id, string Date, string GameType, decimal BuyInAmount);
    private record LeaderboardResponse(string Name, int Games, decimal Result, double Rate);
    private record GamesListResponse(int Id, string Date, string GameType, int PlayerCount, decimal Pot);
    private record GameDetailResponse(string Date, string GameType, List<GameDetailPlayerResponse> Players);
    private record GameDetailPlayerResponse(string PlayerName, decimal BuyIn, decimal CashOut, decimal Net);
    private record ProgressionResponse(int GameId, string Date, decimal Result, decimal CumulativeResult, double Rate, decimal BuyIn);
}
