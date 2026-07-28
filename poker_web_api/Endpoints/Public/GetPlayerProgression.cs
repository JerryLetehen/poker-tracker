using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Endpoints.Public;

public static class GetPlayerProgression
{
    public static void MapGetPlayerProgression(this WebApplication app)
    {
        app.MapGet("/players/{name}/progression", async (string name, PokerDbContext db) =>
        {
            var player = await db.Players.FirstOrDefaultAsync(p => p.Name == name);
            if (player is null) return Results.NotFound();

            var gameNets = await db.GameEntries
                .Where(e => e.PlayerId == player.Id)
                .GroupBy(e => new { e.GameId, e.Game.Date })
                .Select(g => new
                {
                    g.Key.GameId,
                    g.Key.Date,
                    Net = g.Where(e => e.Type == EntryType.CashOut).Sum(e => e.Amount)
                        - g.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount),
                    BuyIn = g.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount)
                })
                .OrderBy(g => g.Date)
                .ToListAsync();

            var cumulativeResult = 0m;
            var nets = new List<decimal>();
            var progression = gameNets.Select(g =>
            {
                cumulativeResult += g.Net;
                nets.Add(g.Net);
                var sortedNets = nets.OrderBy(n => n).ToList();
                var count = sortedNets.Count;
                var median = count % 2 == 1
                    ? sortedNets[count / 2]
                    : (sortedNets[count / 2 - 1] + sortedNets[count / 2]) / 2;

                return new
                {
                    g.GameId,
                    Date = g.Date.ToString("dd-MM-yyyy"),
                    Result = g.Net,
                    CumulativeResult = cumulativeResult,
                    Rate = cumulativeResult / count,
                    Median = median,
                    g.BuyIn
                };
            }).ToList();

            return Results.Ok(progression);
        })
        .WithName("GetPlayerProgression");
    }
}
