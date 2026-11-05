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
        var response = await repo.GetLeaderboards(1, "rate");
        var result = response.All;

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
        var response = await repo.GetLeaderboards(1, "net");
        var result = response.All;

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
        var response = await repo.GetLeaderboards(5, "rate");
        var result = response.All;

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
        var response = await repo.GetLeaderboards(1, "rate");
        var result = response.All;

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
        var response = await repo.GetLeaderboards(1, "net");
        var result = response.All;

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
        var response = await repo.GetLeaderboards(1, "net");
        var result = response.All;

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
        var response = await repo.GetLeaderboards(1, "rate");
        var result = response.All;

        // Rate = (20 + -10 + 50) / 3 = 20
        Assert.Equal(20.0, result[0].Rate, 2);
    }

    // === IMPOSSIBLE / EDGE CASES ===

    [Fact]
    public async Task GetLeaderboard_EmptyDatabase_ReturnsEmptyList()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var repo = new LeaderboardRepository(db);
        var response = await repo.GetLeaderboards(1, "rate");
        var result = response.All;

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLeaderboard_NoPlayersWithEnoughGames_ReturnsEmptyList()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player = TestDbHelper.AddPlayer(db, "OneGameWonder");
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 50, (player, 200));

        var repo = new LeaderboardRepository(db);
        var response = await repo.GetLeaderboards(10, "rate");
        var result = response.All;

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLeaderboard_MinGamesZero_ReturnsAllPlayers()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player = TestDbHelper.AddPlayer(db, "Player");
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 50, (player, 60));

        var repo = new LeaderboardRepository(db);
        var response = await repo.GetLeaderboards(0, "rate");
        var result = response.All;

        Assert.Single(result);
    }

    [Fact]
    public async Task GetLeaderboard_PlayerWithZeroNet_IncludedInResults()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var player = TestDbHelper.AddPlayer(db, "BreakEven");
        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 50, (player, 50)); // net = 0

        var repo = new LeaderboardRepository(db);
        var response = await repo.GetLeaderboards(1, "rate");
        var result = response.All;

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
        var response = await repo.GetLeaderboards(1, "net");
        var result = response.All;

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
        var response = await repo.GetLeaderboards(1, "invalid_sort_value");
        var result = response.All;

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
        var response = await repo.GetLeaderboards(1, "rate");
        var result = response.All;

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
        var response = await repo.GetLeaderboards(1, "rate");
        var result = response.All;

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
        var response = await repo.GetLeaderboards(1, "net");
        var result = response.All;

        Assert.Equal(15000m, result[0].Result);
    }

    [Fact]
    public async Task GetLeaderboard_VerySmallAmounts_HandledCorrectly()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var penny = TestDbHelper.AddPlayer(db, "PennyPlayer");

        TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 1), 0.01m, (penny, 0.02m));

        var repo = new LeaderboardRepository(db);
        var response = await repo.GetLeaderboards(1, "net");
        var result = response.All;

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
        var response = await repo.GetLeaderboards(1, "rate");
        var result = response.All;

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
        var response = await repo.GetLeaderboards(1, "net");
        var result = response.All;

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
        var response = await repo.GetLeaderboards(1, "rate");
        var result = response.All;

        Assert.Equal(20, result.Count);
        // Verify sorted descending
        for (int i = 0; i < result.Count - 1; i++)
        {
            Assert.True(result[i].Rate >= result[i + 1].Rate);
        }
    }

    // === TASK 5: EXAMPLE AND EDGE-CASE TESTS ===

    // Req 1.5: Active_Min_Games default is 10, applied to the Active list only (request minGames
    // does not apply to Active). A player with exactly 9 in-window games is excluded, while a player
    // with exactly 10 in-window games is included.
    [Fact]
    public async Task GetLeaderboards_ActiveThresholdBoundary_NineExcludedTenIncluded()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var today = new DateOnly(2024, 6, 15);
        var nineGames = TestDbHelper.AddPlayer(db, "NineGames");
        var tenGames = TestDbHelper.AddPlayer(db, "TenGames");

        // Both players' games are well inside the 12-month window.
        for (int i = 0; i < 9; i++)
            TestDbHelper.AddCompleteGame(db, today.AddDays(-(i + 1)), 50, (nineGames, 60));
        for (int i = 0; i < 10; i++)
            TestDbHelper.AddCompleteGame(db, today.AddDays(-(i + 1)), 50, (tenGames, 60));

        var repo = new LeaderboardRepository(db);
        // allMinGames of 1 would include both on All, but Active uses the fixed threshold of 10.
        var response = await repo.GetLeaderboards(1, "rate", today);

        Assert.DoesNotContain(response.Active, e => e.Name == "NineGames");
        Assert.Contains(response.Active, e => e.Name == "TenGames");

        var ten = Assert.Single(response.Active, e => e.Name == "TenGames");
        Assert.Equal(10, ten.Games);
    }

    // Req 5.6: the All_Min_Games value (here allMinGames = 5) is applied to the All list. A player
    // with 4 distinct games is excluded, while a player with exactly 5 distinct games is included.
    [Fact]
    public async Task GetLeaderboards_AllThreshold_FourExcludedFiveIncluded()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var today = new DateOnly(2024, 6, 15);
        var fourGames = TestDbHelper.AddPlayer(db, "FourGames");
        var fiveGames = TestDbHelper.AddPlayer(db, "FiveGames");

        for (int i = 0; i < 4; i++)
            TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, i + 1), 50, (fourGames, 60));
        for (int i = 0; i < 5; i++)
            TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 2, i + 1), 50, (fiveGames, 60));

        var repo = new LeaderboardRepository(db);
        var response = await repo.GetLeaderboards(5, "rate", today);

        Assert.DoesNotContain(response.All, e => e.Name == "FourGames");
        Assert.Contains(response.All, e => e.Name == "FiveGames");

        var five = Assert.Single(response.All, e => e.Name == "FiveGames");
        Assert.Equal(5, five.Games);
    }

    // Req 1.1: last-day-of-month window fallback. With today = 2024-02-29, today.AddMonths(-12)
    // clamps to 2023-02-28, so the window start is 2023-02-28. A game dated exactly on the window
    // start counts toward the Active window; a game dated one day earlier (2023-02-27) does not.
    [Fact]
    public async Task GetLeaderboards_LastDayOfMonthWindowFallback_IncludesWindowStartExcludesDayBefore()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var today = new DateOnly(2024, 2, 29);
        var player = TestDbHelper.AddPlayer(db, "WindowEdge");

        // One game dated exactly on the window start (2023-02-28) — should count.
        TestDbHelper.AddCompleteGame(db, new DateOnly(2023, 2, 28), 50, (player, 60));
        // One game dated the day before the window start (2023-02-27) — should NOT count.
        TestDbHelper.AddCompleteGame(db, new DateOnly(2023, 2, 27), 50, (player, 60));
        // Nine more in-window games so the player reaches the Active threshold of 10 (10 in window).
        for (int i = 0; i < 9; i++)
            TestDbHelper.AddCompleteGame(db, new DateOnly(2023, 3, i + 1), 50, (player, 60));

        var repo = new LeaderboardRepository(db);
        var response = await repo.GetLeaderboards(1, "rate", today);

        // MEMBERSHIP: in-window games are 2023-02-28 + nine March games = 10, so the player meets the
        // 10-games-in-window Active gate. The 2023-02-27 game does NOT count toward that gate.
        // STATS are all-time: the player has 11 games total (the out-of-window 2023-02-27 game is still
        // counted), so the Active entry shows 11 games — the same number the All list shows.
        var active = Assert.Single(response.Active, e => e.Name == "WindowEdge");
        Assert.Equal(11, active.Games);

        // All-time the player has 11 games; Active and All show the identical all-time count.
        var all = Assert.Single(response.All, e => e.Name == "WindowEdge");
        Assert.Equal(11, all.Games);
    }

    // Req 5.5: the Active and All lists are computed independently. "RecentOnly" has 10 recent games
    // (qualifies for Active) but is excluded from All because allMinGames is set above their all-time
    // game count. "OldOnly" has enough all-time games to appear on All but all are outside the window,
    // so they are absent from Active. This confirms neither list's membership depends on the other.
    [Fact]
    public async Task GetLeaderboards_ListsComputedIndependently_PlayerInOneListOnly()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var today = new DateOnly(2024, 6, 15);
        var recentOnly = TestDbHelper.AddPlayer(db, "RecentOnly");
        var oldOnly = TestDbHelper.AddPlayer(db, "OldOnly");

        // RecentOnly: 10 recent in-window games -> qualifies for Active. All-time games = 10, which
        // is below allMinGames (50), so they are excluded from All.
        for (int i = 0; i < 10; i++)
            TestDbHelper.AddCompleteGame(db, today.AddDays(-(i + 1)), 50, (recentOnly, 60));

        // OldOnly: 60 all-time games, all dated well before the 12-month window -> qualifies for All
        // (>= 50) but has zero in-window games, so they are absent from Active.
        var oldDate = new DateOnly(2020, 1, 1);
        for (int i = 0; i < 60; i++)
            TestDbHelper.AddCompleteGame(db, oldDate.AddDays(i), 50, (oldOnly, 60));

        var repo = new LeaderboardRepository(db);
        var response = await repo.GetLeaderboards(50, "rate", today);

        // RecentOnly appears only in Active.
        Assert.Contains(response.Active, e => e.Name == "RecentOnly");
        Assert.DoesNotContain(response.All, e => e.Name == "RecentOnly");

        // OldOnly appears only in All.
        Assert.Contains(response.All, e => e.Name == "OldOnly");
        Assert.DoesNotContain(response.Active, e => e.Name == "OldOnly");
    }

    // Req 2.1, 2.3, 2.4: the 12-month window + 10-game rule is a MEMBERSHIP filter only; once a player
    // qualifies for Active, their Games/Result/Rate are all-time (they include out-of-window games) and
    // are IDENTICAL to the numbers the same player shows on the All list. A separate player whose games
    // are all older than the window qualifies for All (>= allMinGames all-time) but NOT for Active.
    [Fact]
    public async Task GetLeaderboards_ActiveStatsAreAllTime_IdenticalToAllList()
    {
        var db = TestDbHelper.CreateInMemoryDb();
        var today = new DateOnly(2024, 6, 15);

        // Qualifier: 10 in-window games (net +10 each) -> qualifies for Active. Plus 3 older
        // out-of-window games (net +40 each) that must be included in the all-time stats.
        var qualifier = TestDbHelper.AddPlayer(db, "Qualifier");
        for (int i = 0; i < 10; i++)
            TestDbHelper.AddCompleteGame(db, today.AddDays(-(i + 1)), 50, (qualifier, 60)); // net +10
        var oldDate = new DateOnly(2020, 1, 1);
        for (int i = 0; i < 3; i++)
            TestDbHelper.AddCompleteGame(db, oldDate.AddDays(i), 50, (qualifier, 90)); // net +40

        // OlderOnly: 6 games all older than the window -> on All (>=5) but not Active (0 in-window).
        var olderOnly = TestDbHelper.AddPlayer(db, "OlderOnly");
        for (int i = 0; i < 6; i++)
            TestDbHelper.AddCompleteGame(db, oldDate.AddDays(100 + i), 50, (olderOnly, 70)); // net +20

        var repo = new LeaderboardRepository(db);
        var response = await repo.GetLeaderboards(5, "net", today);

        // Qualifier shows ALL-TIME stats on Active: 13 games, result = 10*10 + 3*40 = 220.
        var active = Assert.Single(response.Active, e => e.Name == "Qualifier");
        var all = Assert.Single(response.All, e => e.Name == "Qualifier");
        Assert.Equal(13, active.Games);
        Assert.Equal(220m, active.Result);
        // The Active and All entries are the exact same all-time numbers.
        Assert.Equal(all.Games, active.Games);
        Assert.Equal(all.Result, active.Result);
        Assert.Equal(all.Rate, active.Rate);

        // OlderOnly is on All but not Active.
        Assert.Contains(response.All, e => e.Name == "OlderOnly");
        Assert.DoesNotContain(response.Active, e => e.Name == "OlderOnly");
    }
}
