using Microsoft.EntityFrameworkCore;
using poker_web_api.Entities;

namespace poker_web_api.Data;

public record LeaderboardSnapshot(
    string Date,
    List<PlayerSnapshot> Players
);

public record PlayerSnapshot(
    string Name,
    decimal CumulativeNet,
    double Rate,
    int Games,
    int RateRank,
    int NetRank
);

public class LeaderboardHistoryRepository
{
    private readonly PokerDbContext _db;

    public LeaderboardHistoryRepository(PokerDbContext db)
    {
        _db = db;
    }

    public async Task<List<LeaderboardSnapshot>> GetHistory()
    {
        // Get all game entries grouped by player and game, ordered by game date
        var allEntries = await _db.GameEntries
            .Include(e => e.Game)
            .Include(e => e.Player)
            .ToListAsync();

        var games = allEntries
            .GroupBy(e => e.Game)
            .OrderBy(g => g.Key.Date)
            .ToList();

        // Track running totals per player
        var playerNets = new Dictionary<string, List<decimal>>();
        var snapshots = new List<LeaderboardSnapshot>();

        foreach (var game in games)
        {
            // Calculate net per player in this game
            var gamePlayerNets = game
                .GroupBy(e => e.Player.Name)
                .ToDictionary(
                    g => g.Key,
                    g => g.Where(e => e.Type == EntryType.CashOut).Sum(e => e.Amount)
                       - g.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount)
                );

            // Update running totals
            foreach (var (playerName, net) in gamePlayerNets)
            {
                if (!playerNets.ContainsKey(playerName))
                    playerNets[playerName] = [];
                playerNets[playerName].Add(net);
            }

            // Build snapshot for all players who have played at least once so far
            var playerSnapshots = playerNets
                .Select(kv => new
                {
                    Name = kv.Key,
                    CumulativeNet = kv.Value.Sum(),
                    Rate = (double)kv.Value.Sum() / kv.Value.Count,
                    Games = kv.Value.Count
                })
                .ToList();

            // Rank by rate and net
            var byRate = playerSnapshots.OrderByDescending(p => p.Rate).ToList();
            var byNet = playerSnapshots.OrderByDescending(p => p.CumulativeNet).ToList();

            var players = playerSnapshots.Select(p => new PlayerSnapshot(
                p.Name,
                p.CumulativeNet,
                p.Rate,
                p.Games,
                RateRank: byRate.FindIndex(x => x.Name == p.Name) + 1,
                NetRank: byNet.FindIndex(x => x.Name == p.Name) + 1
            )).ToList();

            snapshots.Add(new LeaderboardSnapshot(
                game.Key.Date.ToString("dd-MM-yyyy"),
                players
            ));
        }

        return snapshots;
    }
}
