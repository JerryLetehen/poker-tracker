using FsCheck;
using FsCheck.Xunit;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Tests.Unit;

/// <summary>
/// Property-based tests for <see cref="LeaderboardRepository"/> (Feature: dual-leaderboard).
///
/// Each property builds a fresh in-memory database per iteration (unique db name) from
/// FsCheck-generated values, passes a FIXED <c>today</c> into
/// <see cref="LeaderboardRepository.GetLeaderboards"/>, and asserts a correctness property
/// from the design document. Generated data sizes are kept small so 100 iterations run quickly
/// against the EF in-memory provider.
/// </summary>
public class LeaderboardRepositoryPropertyTests
{
    // A fixed reference date used by every property so the 12-month window is deterministic.
    private static readonly DateOnly Today = new(2024, 6, 15);
    private static readonly DateOnly WindowStart = Today.AddMonths(-12); // 2023-06-15

    private const int ActiveMin = LeaderboardRepository.DefaultActiveMinGames; // 10

    // ----- Generators ------------------------------------------------------------------------

    // A per-game net delta kept in a modest range so Result/Rate stay comparable and fast to build.
    private static Gen<decimal> NetDeltaGen =>
        Gen.Choose(-200, 200).Select(i => (decimal)i);

    // A non-negative money amount used for buy-ins / cash-outs.
    private static Gen<decimal> AmountGen =>
        Gen.Choose(0, 300).Select(i => (decimal)i);

    // A count of games within a small range.
    private static Gen<int> SmallCountGen(int min, int max) => Gen.Choose(min, max);

    // ----- Helpers ---------------------------------------------------------------------------

    private static PokerDbContext NewDb() => TestDbHelper.CreateInMemoryDb(Guid.NewGuid().ToString());

    // Adds a game on the given date where the single player's net equals `net`
    // (buy-in 0, cash-out `net` when net >= 0; otherwise buy-in -net, cash-out 0).
    private static Game AddGameWithNet(PokerDbContext db, DateOnly date, int playerId, decimal net)
    {
        var game = TestDbHelper.AddGame(db, date);
        if (net >= 0)
        {
            TestDbHelper.AddCashOut(db, game.Id, playerId, net);
        }
        else
        {
            TestDbHelper.AddBuyIn(db, game.Id, playerId, -net);
        }
        return game;
    }

    // Ordinal-name-safe comparator check for the "net" ordering.
    private static bool NetOrdered(LeaderboardEntry a, LeaderboardEntry b)
    {
        if (a.Result != b.Result) return a.Result > b.Result;
        if (a.Rate != b.Rate) return a.Rate > b.Rate;
        return string.CompareOrdinal(a.Name, b.Name) < 0;
    }

    // Comparator check for the "rate" ordering.
    private static bool RateOrdered(LeaderboardEntry a, LeaderboardEntry b)
    {
        if (a.Rate != b.Rate) return a.Rate > b.Rate;
        if (a.Result != b.Result) return a.Result > b.Result;
        return string.CompareOrdinal(a.Name, b.Name) < 0;
    }

    // =========================================================================================

    // Feature: dual-leaderboard, Property 1: Window scope is inclusive on both endpoints (membership only)
    // Validates: Requirements 1.1, 1.2
    [Property(MaxTest = 100)]
    public Property WindowScopeIsInclusiveOnBothEndpoints()
    {
        // The 12-month window affects only Active MEMBERSHIP, not stats. Place ActiveMin-1 clearly-
        // in-window games plus one boundary game that is either just-inside (windowStart or today) or
        // just-outside (day before start / after today). With an inside boundary the player reaches
        // ActiveMin in-window games and qualifies; with an outside boundary they have only ActiveMin-1
        // in-window games and do NOT qualify. Either way the entry's Games (when present) equals the
        // player's ALL-TIME distinct game count, which is always ActiveMin here.
        var gen =
            from offsetDays in Gen.Choose(0, 300) // in-window anchor offset from windowStart
            from boundary in Gen.Elements("beforeStart", "atStart", "atToday", "afterToday")
            select (offsetDays, boundary);

        return Prop.ForAll(Arb.From(gen), t =>
        {
            var (offsetDays, boundary) = t;
            using var db = NewDb();
            var player = TestDbHelper.AddPlayer(db, "P");

            // ActiveMin-1 clearly-in-window games (distinct dates inside the window).
            var anchor = WindowStart.AddDays(Math.Min(offsetDays, 300));
            if (anchor > Today) anchor = Today;
            for (int i = 0; i < ActiveMin - 1; i++)
            {
                // Spread them backward from the anchor but keep them inside the window.
                var d = anchor.AddDays(-i);
                if (d < WindowStart) d = WindowStart;
                AddGameWithNet(db, d, player.Id, 10m);
            }

            DateOnly boundaryDate = boundary switch
            {
                "beforeStart" => WindowStart.AddDays(-1),
                "atStart" => WindowStart,
                "atToday" => Today,
                _ => Today.AddDays(1),
            };
            AddGameWithNet(db, boundaryDate, player.Id, 10m);

            var repo = new LeaderboardRepository(db);
            var active = repo.GetLeaderboards(1, "net", Today).Result.Active;

            // Count how many of the player's games fall inside [WindowStart, Today].
            var inWindow = db.Games.Count(g => g.Date >= WindowStart && g.Date <= Today);
            var allTimeGames = db.Games.Count(); // ActiveMin total games were added.
            var entry = active.SingleOrDefault(e => e.Name == "P");

            // The window endpoints are INCLUSIVE for membership: an at-start / at-today boundary game
            // counts toward the 10-games gate; a before-start / after-today one does not.
            bool boundaryCounts = boundary is "atStart" or "atToday";
            int expectedInWindow = (ActiveMin - 1) + (boundaryCounts ? 1 : 0);
            var dbMatches = inWindow == expectedInWindow;

            // Membership: qualifies iff in-window count >= ActiveMin.
            bool shouldQualify = expectedInWindow >= ActiveMin;

            bool entryCorrect = shouldQualify
                // When qualified, the entry's Games equals the ALL-TIME distinct game count (not windowed).
                ? entry is not null && entry.Games == allTimeGames
                // When not qualified, the player is absent from Active.
                : entry is null;

            return (dbMatches && entryCorrect)
                .Label($"boundary={boundary}, inWindow={inWindow}/{expectedInWindow}, allTime={allTimeGames}, games={entry?.Games}, qualify={shouldQualify}");
        });
    }

    // Feature: dual-leaderboard, Property 2: Active membership follows the game-count threshold
    // Validates: Requirements 1.3, 1.4
    [Property(MaxTest = 100)]
    public Property ActiveMembershipFollowsGameCountThreshold()
    {
        // Generate an in-window game count in [0, 15] and assert membership iff count >= ActiveMin.
        return Prop.ForAll(Arb.From(SmallCountGen(0, 15)), windowGames =>
        {
            using var db = NewDb();
            var player = TestDbHelper.AddPlayer(db, "P");

            for (int i = 0; i < windowGames; i++)
            {
                var d = WindowStart.AddDays(i); // distinct in-window dates
                AddGameWithNet(db, d, player.Id, 5m);
            }

            var repo = new LeaderboardRepository(db);
            var active = repo.GetLeaderboards(1, "rate", Today).Result.Active;

            bool present = active.Any(e => e.Name == "P");
            bool shouldBePresent = windowGames >= ActiveMin;

            return (present == shouldBePresent)
                .Label($"windowGames={windowGames}, present={present}, shouldBe={shouldBePresent}");
        });
    }

    // Feature: dual-leaderboard, Property 3: Per-game net equals cash-outs minus buy-ins
    // Validates: Requirements 2.2
    [Property(MaxTest = 100)]
    public Property PerGameNetEqualsCashOutsMinusBuyIns()
    {
        // Generate multisets of buy-ins and cash-outs for a single player in a single game.
        var gen =
            from buyIns in Gen.ListOf(AmountGen).Select(l => l.ToList())
            from cashOuts in Gen.ListOf(AmountGen).Select(l => l.ToList())
            // keep sizes modest
            where buyIns.Count <= 5 && cashOuts.Count <= 5
            // at least one entry so the player participates in the game
            where buyIns.Count + cashOuts.Count >= 1
            select (buyIns, cashOuts);

        return Prop.ForAll(Arb.From(gen), t =>
        {
            var (buyIns, cashOuts) = t;
            using var db = NewDb();
            var player = TestDbHelper.AddPlayer(db, "P");
            var game = TestDbHelper.AddGame(db, WindowStart.AddDays(1));

            foreach (var b in buyIns) TestDbHelper.AddBuyIn(db, game.Id, player.Id, b);
            foreach (var c in cashOuts) TestDbHelper.AddCashOut(db, game.Id, player.Id, c);

            var expectedNet = cashOuts.Sum() - buyIns.Sum();

            var repo = new LeaderboardRepository(db);
            // allMinGames = 1 so the single game qualifies for the All list; it is all-time stats.
            var all = repo.GetLeaderboards(1, "net", Today).Result.All;
            var entry = all.SingleOrDefault(e => e.Name == "P");

            return (entry is not null && entry.Games == 1 && entry.Result == expectedNet)
                .Label($"expected={expectedNet}, actual={entry?.Result}");
        });
    }

    // Feature: dual-leaderboard, Property 4: Result and Rate are a consistent aggregation of in-scope games
    // Validates: Requirements 2.3, 2.4, 3.5, 3.6
    [Property(MaxTest = 100)]
    public Property ResultAndRateAreConsistentAggregation()
    {
        // Generate a list of per-game nets (all in-window so both lists see the same scope).
        var gen = Gen.ListOf(NetDeltaGen).Select(l => l.ToList())
            .Where(l => l.Count >= 1 && l.Count <= 14);

        return Prop.ForAll(Arb.From(gen), nets =>
        {
            using var db = NewDb();
            var player = TestDbHelper.AddPlayer(db, "P");

            for (int i = 0; i < nets.Count; i++)
            {
                AddGameWithNet(db, WindowStart.AddDays(i), player.Id, nets[i]);
            }

            var expectedResult = nets.Sum();
            var expectedGames = nets.Count;
            var expectedRate = (double)expectedResult / expectedGames;

            var repo = new LeaderboardRepository(db);
            // allMinGames = 1 so the player appears in All with all-time stats (== in-window here).
            var all = repo.GetLeaderboards(1, "net", Today).Result.All;
            var entry = all.Single(e => e.Name == "P");

            bool ok = entry.Games == expectedGames
                      && entry.Result == expectedResult
                      && Math.Abs(entry.Rate - expectedRate) < 1e-9
                      && entry.Games >= 1;

            return ok.Label($"games={entry.Games}/{expectedGames}, result={entry.Result}/{expectedResult}, rate={entry.Rate}/{expectedRate}");
        });
    }

    // Feature: dual-leaderboard, Property 5: Active stats are all-time (include out-of-window games)
    // Validates: Requirements 2.1, 2.3, 2.4
    [Property(MaxTest = 100)]
    public Property ActiveStatsAreAllTime()
    {
        // The window is a MEMBERSHIP filter only: a qualified player (ActiveMin in-window games) shows
        // ALL-TIME Games/Result/Rate, so out-of-window games DO count toward their Active stats. Build
        // ActiveMin in-window games plus an arbitrary number of out-of-window games and assert the
        // Active entry aggregates over ALL of them (identical to what the All list would show).
        var gen =
            from inNets in Gen.ArrayOf(ActiveMin, NetDeltaGen)
            from outCount in Gen.Choose(0, 6)
            from outNets in Gen.ArrayOf(outCount, NetDeltaGen)
            from side in Gen.Elements("before", "after")
            select (inNets, outNets, side);

        return Prop.ForAll(Arb.From(gen), t =>
        {
            var (inNets, outNets, side) = t;
            using var db = NewDb();
            var player = TestDbHelper.AddPlayer(db, "P");

            // In-window qualifying games.
            for (int i = 0; i < inNets.Length; i++)
                AddGameWithNet(db, WindowStart.AddDays(i), player.Id, inNets[i]);

            // Out-of-window games that MUST now be included in the all-time Active stats.
            for (int i = 0; i < outNets.Length; i++)
            {
                var d = side == "before"
                    ? WindowStart.AddDays(-1 - i)
                    : Today.AddDays(1 + i);
                AddGameWithNet(db, d, player.Id, outNets[i]);
            }

            // Expected values are aggregated over ALL games (in-window + out-of-window).
            var expectedGames = inNets.Length + outNets.Length;
            var expectedResult = inNets.Sum() + outNets.Sum();
            var expectedRate = (double)expectedResult / expectedGames;

            var repo = new LeaderboardRepository(db);
            var resp = repo.GetLeaderboards(1, "net", Today).Result;
            var active = resp.Active.Single(e => e.Name == "P");
            var all = resp.All.Single(e => e.Name == "P");

            bool ok = active.Games == expectedGames
                      && active.Result == expectedResult
                      && Math.Abs(active.Rate - expectedRate) < 1e-9
                      // Active and All show the identical all-time numbers.
                      && active.Games == all.Games
                      && active.Result == all.Result
                      && Math.Abs(active.Rate - all.Rate) < 1e-9;

            return ok.Label($"outCount={outNets.Length}, side={side}, games={active.Games}/{expectedGames}, result={active.Result}/{expectedResult}");
        });
    }

    // Feature: dual-leaderboard, Property 6: All membership follows the all-time game-count threshold
    // Validates: Requirements 3.1, 3.3, 3.4
    [Property(MaxTest = 100)]
    public Property AllMembershipFollowsAllTimeThreshold()
    {
        // Generate an all-time game count and an allMinGames >= 1 (over all dates, in and out of window).
        var gen =
            from gameCount in Gen.Choose(0, 12)
            from allMin in Gen.Choose(1, 12)
            select (gameCount, allMin);

        return Prop.ForAll(Arb.From(gen), t =>
        {
            var (gameCount, allMin) = t;
            using var db = NewDb();
            var player = TestDbHelper.AddPlayer(db, "P");

            // Spread games across all dates (some in window, some far in the past).
            for (int i = 0; i < gameCount; i++)
            {
                // Alternate between in-window and out-of-window to prove date does not matter for All.
                var d = (i % 2 == 0)
                    ? WindowStart.AddDays(i)                 // in window
                    : WindowStart.AddDays(-30 - i);          // before window
                AddGameWithNet(db, d, player.Id, 7m);
            }

            var repo = new LeaderboardRepository(db);
            var all = repo.GetLeaderboards(allMin, "rate", Today).Result.All;

            bool present = all.Any(e => e.Name == "P");
            bool shouldBePresent = gameCount >= allMin;

            return (present == shouldBePresent)
                .Label($"gameCount={gameCount}, allMin={allMin}, present={present}, shouldBe={shouldBePresent}");
        });
    }

    // Feature: dual-leaderboard, Property 7: Repeated entries in one game count as a single game
    // Validates: Requirements 3.2
    [Property(MaxTest = 100)]
    public Property RepeatedEntriesInOneGameCountAsSingleGame()
    {
        // Generate a number of distinct games and, per game, a number of extra buy-in/cash-out entries.
        var gen =
            from distinctGames in Gen.Choose(1, 8)
            from extraPerGame in Gen.ArrayOf(distinctGames, Gen.Choose(0, 4))
            select (distinctGames, extraPerGame);

        return Prop.ForAll(Arb.From(gen), t =>
        {
            var (distinctGames, extraPerGame) = t;
            using var db = NewDb();
            var player = TestDbHelper.AddPlayer(db, "P");

            for (int i = 0; i < distinctGames; i++)
            {
                var game = TestDbHelper.AddGame(db, WindowStart.AddDays(i));
                // One guaranteed buy-in + cash-out, plus extra repeated entries (rebuys / partial cashouts).
                TestDbHelper.AddBuyIn(db, game.Id, player.Id, 10m);
                TestDbHelper.AddCashOut(db, game.Id, player.Id, 10m);
                for (int e = 0; e < extraPerGame[i]; e++)
                {
                    TestDbHelper.AddBuyIn(db, game.Id, player.Id, 5m);
                    TestDbHelper.AddCashOut(db, game.Id, player.Id, 5m);
                }
            }

            var repo = new LeaderboardRepository(db);
            var all = repo.GetLeaderboards(1, "net", Today).Result.All;
            var entry = all.Single(e => e.Name == "P");

            // Games must equal distinct games, not total entry count.
            return (entry.Games == distinctGames)
                .Label($"distinctGames={distinctGames}, reportedGames={entry.Games}");
        });
    }

    // Feature: dual-leaderboard, Property 8: Each list is a deterministic total ordering
    // Validates: Requirements 4.1, 4.2, 4.3, 4.4
    [Property(MaxTest = 100)]
    public Property EachListIsDeterministicTotalOrdering()
    {
        // Several players, each with a few in-window games (so both Active and All are populated),
        // and a sort mode of net or rate. Assert adjacent pairs satisfy the comparator in both lists.
        var gen =
            from playerCount in Gen.Choose(2, 12)
            from gamesPer in Gen.ArrayOf(playerCount, Gen.Choose(ActiveMin, ActiveMin + 3))
            from netsFlat in Gen.ArrayOf(playerCount * (ActiveMin + 3), NetDeltaGen)
            from sortBy in Gen.Elements("net", "rate")
            select (playerCount, gamesPer, netsFlat, sortBy);

        return Prop.ForAll(Arb.From(gen), t =>
        {
            var (playerCount, gamesPer, netsFlat, sortBy) = t;
            using var db = NewDb();

            int netIdx = 0;
            for (int p = 0; p < playerCount; p++)
            {
                // Unique, ordinal-stable names.
                var player = TestDbHelper.AddPlayer(db, $"P{p:D3}");
                int games = gamesPer[p];
                for (int g = 0; g < games; g++)
                {
                    var net = netsFlat[netIdx % netsFlat.Length];
                    netIdx++;
                    AddGameWithNet(db, WindowStart.AddDays(g), player.Id, net);
                }
            }

            var repo = new LeaderboardRepository(db);
            var resp = repo.GetLeaderboards(1, sortBy, Today).Result;

            Func<LeaderboardEntry, LeaderboardEntry, bool> ordered =
                sortBy == "net" ? NetOrdered : RateOrdered;

            bool CheckList(IReadOnlyList<LeaderboardEntry> list)
            {
                for (int i = 0; i < list.Count - 1; i++)
                {
                    if (!ordered(list[i], list[i + 1])) return false;
                }
                // totality: names are unique
                var names = list.Select(e => e.Name).ToList();
                return names.Count == names.Distinct().Count();
            }

            bool ok = CheckList(resp.Active) && CheckList(resp.All);
            return ok.Label($"sortBy={sortBy}, active={resp.Active.Count}, all={resp.All.Count}");
        });
    }

    // Feature: dual-leaderboard, Property 9: The minGames parameter affects only the All list
    // Validates: Requirements 5.5
    [Property(MaxTest = 100)]
    public Property MinGamesParameterAffectsOnlyAllList()
    {
        // Build data, then compare Active lists produced with two different allMinGames values.
        var gen =
            from playerCount in Gen.Choose(1, 8)
            from gamesPer in Gen.ArrayOf(playerCount, Gen.Choose(1, ActiveMin + 3))
            from netsFlat in Gen.ArrayOf(playerCount * (ActiveMin + 3), NetDeltaGen)
            from minA in Gen.Choose(1, 12)
            from minB in Gen.Choose(1, 12)
            from sortBy in Gen.Elements("net", "rate")
            select (playerCount, gamesPer, netsFlat, minA, minB, sortBy);

        return Prop.ForAll(Arb.From(gen), t =>
        {
            var (playerCount, gamesPer, netsFlat, minA, minB, sortBy) = t;

            // Two identical databases so the only difference is the allMinGames argument.
            LeaderboardsResponse Build(int allMin)
            {
                using var db = NewDb();
                int netIdx = 0;
                for (int p = 0; p < playerCount; p++)
                {
                    var player = TestDbHelper.AddPlayer(db, $"P{p:D3}");
                    for (int g = 0; g < gamesPer[p]; g++)
                    {
                        var net = netsFlat[netIdx % netsFlat.Length];
                        netIdx++;
                        AddGameWithNet(db, WindowStart.AddDays(g), player.Id, net);
                    }
                }
                var repo = new LeaderboardRepository(db);
                return repo.GetLeaderboards(allMin, sortBy, Today).Result;
            }

            var a = Build(minA).Active;
            var b = Build(minB).Active;

            bool sameActive = a.Count == b.Count &&
                a.Zip(b, (x, y) =>
                    x.Name == y.Name &&
                    x.Games == y.Games &&
                    x.Result == y.Result &&
                    Math.Abs(x.Rate - y.Rate) < 1e-9).All(eq => eq);

            return sameActive.Label($"minA={minA}, minB={minB}, aCount={a.Count}, bCount={b.Count}");
        });
    }
}
