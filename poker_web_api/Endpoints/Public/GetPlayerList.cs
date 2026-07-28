using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;

namespace poker_web_api.Endpoints.Public;

public static class GetPlayerList
{
    public static void MapGetPlayerList(this WebApplication app)
    {
        app.MapGet("/player-list", async (PokerDbContext db) =>
            await db.Players.Select(p => p.Name).ToListAsync())
            .WithName("GetPlayerList");
    }
}
