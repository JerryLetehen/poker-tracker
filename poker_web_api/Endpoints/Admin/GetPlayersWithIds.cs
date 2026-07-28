using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;

namespace poker_web_api.Endpoints.Admin;

public static class GetPlayersWithIds
{
    public static void MapGetPlayersWithIds(this WebApplication app)
    {
        app.MapGet("/admin/players", async (PokerDbContext db) =>
            await db.Players.Select(p => new { p.Id, p.Name }).OrderBy(p => p.Name).ToListAsync())
        .WithName("GetPlayersWithIds");
    }
}
