using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Tests.Unit;

public class LeaderboardRepositoryTests
{
    // === POSSIBLE CASES (normal operation) ===

    [Fact]
    public async Task GetLeaderboard_SortByRate_ReturnsPlayersSortedByAverageNet()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player1 = TestDbHelper.AddPlayer(db, "HighRate");
        var player2 = TestDbHelper.AddPlayer(db, "LowRate");

        // Player1: 2 games, net +100 each = rate +100
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 50, (player1, 150), (player2, 20));
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 2), 50, (player1, 150), (player2, 20));

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "rate");

        Assert.Equal(2, result.Count);
        Assert.Equal("HighRate", result[0].Name);
        Assert.Equal("LowRate", result[1].Name);
        Assert.True(result[0].Rate > result[1].Rate);
    }

    [Fact]
    public async Task GetLeaderboard_SortByNet_ReturnsPlayersSortedByTotalNet()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player1 = TestDbHelper.AddPlayer(db, "BigNet");
        var player2 = TestDbHelper.AddPlayer(db, "SmallNet");

        // Player1: 1 game, net +200. Player2: 3 games, net +50 each = +150 total
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 50, (player1, 250));
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 2), 50, (player2, 100));
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 3), 50, (player2, 100));
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 4), 50, (player2, 100));

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "net");

        Assert.Equal("BigNet", result[0].Name);
        Assert.Equal(200m, result[0].Result);
        Assert.Equal("SmallNet", result[1].Name);
        Assert.Equal(150m, result[1].Result);
    }

    [Fact]
    public async Task GetLeaderboard_MinGamesFilter_ExcludesPlayersWithFewerGames()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var active = TestDbHelper.AddPlayer(db, "Active");
        var casual = TestDbHelper.AddPlayer(db, "Casual");

        // Active: 5 games
        for (int i = 0; i < 5; i++)
            TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, i + 1), 50, (active, 60));

        // Casual: 2 games
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 2, 1), 50, (casual, 100));
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 2, 2), 50, (casual, 100));

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(5, "rate");

        Assert.Single(result);
        Assert.Equal("Active", result[0].Name);
    }

    [Fact]
    public async Task GetLeaderboard_CorrectlyCalculatesGamesCount()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player = TestDbHelper.AddPlayer(db, "Player");

        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 50, (player, 60));
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 2), 50, (player, 40));
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 3), 50, (player, 70));

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "rate");

        Assert.Equal(3, result[0].Games);
    }

    [Fact]
    public async Task GetLeaderboard_CorrectlyCalculatesNetWithMultipleBuyIns()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player = TestDbHelper.AddPlayer(db, "Rebuyer");

        var game = TestDbHelper.AddGame(db, new DateOnly(2024, 1, 1), buyInAmount: 50);
        TestDbHelper.AddBuyIn(db, game.Id, player.Id, 50);
        TestDbHelper.AddBuyIn(db, game.Id, player.Id, 50); // rebuy
        TestDbHelper.AddCashOut(db, game.Id, player.Id, 130);

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "net");

        // Net = 130 - 100 = 30
        Assert.Equal(30m, result[0].Result);
    }

    [Fact]
    public async Task GetLeaderboard_NegativeNetValues_HandledCorrectly()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var loser = TestDbHelper.AddPlayer(db, "Loser");

        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 50, (loser, 10));
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 2), 50, (loser, 5));

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "net");

        Assert.Equal(-85m, result[0].Result); // (10-50) + (5-50) = -85
        Assert.True(result[0].Rate < 0);
    }

    [Fact]
    public async Task GetLeaderboard_RateCalculation_IsAverageNetPerGame()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player = TestDbHelper.AddPlayer(db, "Player");

        // Game 1: net = +20, Game 2: net = -10, Game 3: net = +50
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 50, (player, 70));
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 2), 50, (player, 40));
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 3), 50, (player, 100));

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "rate");

        // Rate = (20 + -10 + 50) / 3 = 20
        Assert.Equal(20.0, result[0].Rate, 2);
    }

    // === IMPOSSIBLE / EDGE CASES ===

    [Fact]
    public async Task GetLeaderboard_EmptyDatabase_ReturnsEmptyList()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "rate");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLeaderboard_NoPlayersWithEnoughGames_ReturnsEmptyList()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player = TestDbHelper.AddPlayer(db, "OneGameWonder");
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 50, (player, 200));

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(10, "rate");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLeaderboard_MinGamesZero_ReturnsAllPlayers()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player = TestDbHelper.AddPlayer(db, "Player");
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 50, (player, 60));

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(0, "rate");

        Assert.Single(result);
    }

    [Fact]
    public async Task GetLeaderboard_PlayerWithZeroNet_IncludedInResults()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player = TestDbHelper.AddPlayer(db, "BreakEven");
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 50, (player, 50)); // net = 0

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "rate");

        Assert.Single(result);
        Assert.Equal(0m, result[0].Result);
        Assert.Equal(0.0, result[0].Rate);
    }

    [Fact]
    public async Task GetLeaderboard_PlayerWithOnlyBuyInsNoCashOut_NegativeNet()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player = TestDbHelper.AddPlayer(db, "NoCashOut");

        var game = TestDbHelper.AddGame(db, new DateOnly(2024, 1, 1));
        TestDbHelper.AddBuyIn(db, game.Id, player.Id, 50);
        // No cash-out — player left without cashing out (rare but possible data state)

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "net");

        Assert.Single(result);
        Assert.Equal(-50m, result[0].Result);
    }

    [Fact]
    public async Task GetLeaderboard_InvalidSortBy_DefaultsToRate()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var highRate = TestDbHelper.AddPlayer(db, "HighRate");
        var highNet = TestDbHelper.AddPlayer(db, "HighNet");

        // HighRate: 1 game, net +100, rate = 100
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 50, (highRate, 150));
        // HighNet: 3 games, net +10 each = 30 total, rate = 10
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 2, 1), 10, (highNet, 20));
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 2, 2), 10, (highNet, 20));
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 2, 3), 10, (highNet, 20));

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "invalid_sort_value");

        // Should default to rate sort
        Assert.Equal("HighRate", result[0].Name);
    }

    // === RARE CASES ===

    [Fact]
    public async Task GetLeaderboard_PlayerInManyGamesButOnlyBuyIn_AllNegative()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player = TestDbHelper.AddPlayer(db, "AlwaysLoses");

        for (int i = 0; i < 10; i++)
            TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, i + 1), 100, (player, 0)); // loses everything

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "rate");

        Assert.Equal(-100.0, result[0].Rate, 2);
        Assert.Equal(-1000m, result[0].Result);
    }

    [Fact]
    public async Task GetLeaderboard_TiedRates_BothPlayersIncluded()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player1 = TestDbHelper.AddPlayer(db, "Player1");
        var player2 = TestDbHelper.AddPlayer(db, "Player2");

        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 50, (player1, 100), (player2, 100));

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "rate");

        Assert.Equal(2, result.Count);
        Assert.Equal(result[0].Rate, result[1].Rate);
    }

    [Fact]
    public async Task GetLeaderboard_VeryLargeAmounts_HandledCorrectly()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var whale = TestDbHelper.AddPlayer(db, "Whale");

        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 10000m, (whale, 25000m));

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "net");

        Assert.Equal(15000m, result[0].Result);
    }

    [Fact]
    public async Task GetLeaderboard_VerySmallAmounts_HandledCorrectly()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var penny = TestDbHelper.AddPlayer(db, "PennyPlayer");

        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 0.01m, (penny, 0.02m));

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "net");

        Assert.Equal(0.01m, result[0].Result);
    }

    [Fact]
    public async Task GetLeaderboard_PlayerParticipatesInSameGameMultipleTimes_CountsAsOneGame()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player = TestDbHelper.AddPlayer(db, "Rebuyer");

        var game = TestDbHelper.AddGame(db, new DateOnly(2024, 1, 1), buyInAmount: 50);
        TestDbHelper.AddBuyIn(db, game.Id, player.Id, 50);
        TestDbHelper.AddBuyIn(db, game.Id, player.Id, 50);
        TestDbHelper.AddBuyIn(db, game.Id, player.Id, 50);
        TestDbHelper.AddCashOut(db, game.Id, player.Id, 200);

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "rate");

        Assert.Equal(1, result[0].Games); // 1 game, not 3
        Assert.Equal(50m, result[0].Result); // 200 - 150 = 50
    }

    [Fact]
    public async Task GetLeaderboard_MixOfGameTypes_AllCounted()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player = TestDbHelper.AddPlayer(db, "Versatile");

        var game1 = TestDbHelper.AddGame(db, new DateOnly(2024, 1, 1), "texas-holdem-no-limit", 50);
        TestDbHelper.AddBuyIn(db, game1.Id, player.Id, 50);
        TestDbHelper.AddCashOut(db, game1.Id, player.Id, 80);

        var game2 = TestDbHelper.AddGame(db, new DateOnly(2024, 1, 2), "tournament", 15);
        TestDbHelper.AddBuyIn(db, game2.Id, player.Id, 15);
        TestDbHelper.AddCashOut(db, game2.Id, player.Id, 45);

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "net");

        Assert.Equal(2, result[0].Games);
        Assert.Equal(60m, result[0].Result); // (80-50) + (45-15) = 30 + 30 = 60
    }

    [Fact]
    public async Task GetLeaderboard_ManyPlayers_CorrectOrdering()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var players = Enumerable.Range(1, 20)
            .Select(i => TestDbHelper.AddPlayer(db, $"Player{i:D2}"))
            .ToList();

        // Each player gets a different cash-out so rates are unique
        var date = new DateOnly(2024, 1, 1);
        foreach (var (player, idx) in players.Select((p, i) => (p, i)))
        {
            TestDbHelper.AddCompleteGame(db, date.AddDays(idx), 50, (player, 50 + idx * 5));
        }

        var repo = new LeaderboardRepository(db);
        var result = await repo.GetLeaderboard(1, "rate");

        Assert.Equal(20, result.Count);
        // Verify sorted descending
        for (int i = 0; i < result.Count - 1; i++)
        {
            Assert.True(result[i].Rate >= result[i + 1].Rate);
        }
    }
}
