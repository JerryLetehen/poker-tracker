using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using poker_web_api.Auth;
using poker_web_api.Configuration;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Endpoints.Admin;

/// <summary>
/// Publishes a game's results to the configured Telegram group as a plain text message:
///
///   DD.MM.YYYY
///
///   #1 Name in:-150 out:230 net:+80
///   #2 Name in:-50 out:70 net:+20
///   ...
///
///   🎮 Game: https://t.me/&lt;bot&gt;?startapp=game_&lt;id&gt;
///   🏆 Leaderboard: https://t.me/&lt;bot&gt;?startapp=leaderboard
///
/// Players are sorted by net descending (best first). The group chat id comes from
/// TelegramAuth:ResultsChatId and the deep links use TelegramAuth:PublicBotUsername.
/// The PUBLIC bot token is used to send (the group is the public-facing audience).
/// </summary>
public static class PublishGameResults
{
    public static void MapPublishGameResults(this WebApplication app)
    {
        app.MapPost("/admin/games/{id:int}/publish", async (
            int id,
            PokerDbContext db,
            TelegramAuthOptions options,
            ITelegramBotClient bot,
            CancellationToken ct) =>
        {
            var game = await db.Games
                .Include(g => g.GameEntries)
                .ThenInclude(e => e.Player)
                .FirstOrDefaultAsync(g => g.Id == id, ct);

            if (game is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(options.ResultsChatId))
            {
                return Results.BadRequest(new { ok = false, error = "Telegram ResultsChatId is not configured." });
            }

            var text = BuildMessage(game, options.PublicBotUsername);

            var sent = await bot.SendTextAsync(options.PublicBotToken, options.ResultsChatId, text, true, "HTML", ct);
            if (!sent)
            {
                return Results.Json(
                    new { ok = false, error = "Telegram rejected the message." },
                    statusCode: StatusCodes.Status502BadGateway);
            }

            return Results.Ok(new { ok = true });
        })
        .WithName("PublishGameResults");
    }

    // Keycap-digit emoji for ranks 1–10. Ranks beyond 10 fall back to plain digits.
    private static readonly string[] RankEmoji =
    {
        "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟",
    };

    /// <summary>
    /// Builds the results message (Telegram HTML parse mode). Public for unit testing.
    /// Format:
    ///   DD.MM.YYYY
    ///
    ///   1️⃣ Name net:&lt;b&gt;+80&lt;/b&gt;
    ///   2️⃣ Name net:&lt;b&gt;-20&lt;/b&gt;
    ///
    ///   🎮 Game: &lt;link&gt;
    ///
    ///   🏆 Leaderboard: &lt;link&gt;
    /// </summary>
    public static string BuildMessage(Game game, string? botUsername)
    {
        var players = game.GameEntries
            .GroupBy(e => e.Player.Name)
            .Select(g =>
            {
                var buyIn = g.Where(e => e.Type == EntryType.BuyIn).Sum(e => e.Amount);
                var cashOut = g.Where(e => e.Type == EntryType.CashOut).Sum(e => e.Amount);
                return new { Name = g.Key, Net = cashOut - buyIn };
            })
            .OrderByDescending(p => p.Net)
            .ToList();

        var sb = new StringBuilder();
        sb.Append(game.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
        sb.Append("\n\n");

        for (var i = 0; i < players.Count; i++)
        {
            var p = players[i];
            sb.Append(Rank(i + 1)).Append(' ').Append(HtmlEscape(p.Name))
              .Append(" <b>").Append(Signed(p.Net)).Append("</b>")
              .Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(botUsername))
        {
            var user = botUsername.TrimStart('@');
            sb.Append("\n🎮 Game: ").Append($"https://t.me/{user}?startapp=game_{game.Id}");
            sb.Append("\n\n🏆 Leaderboard: ").Append($"https://t.me/{user}?startapp=leaderboard");
        }

        return sb.ToString().TrimEnd('\n');
    }

    private static string Rank(int n) =>
        n >= 1 && n <= RankEmoji.Length ? RankEmoji[n - 1] : n.ToString(CultureInfo.InvariantCulture);

    /// <summary>Escapes the characters Telegram HTML parse mode treats specially.</summary>
    private static string HtmlEscape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>Formats net with an explicit +/- sign and always two decimals.</summary>
    private static string Signed(decimal value)
    {
        var s = Math.Abs(value).ToString("0.00", CultureInfo.InvariantCulture);
        return value < 0 ? "-" + s : "+" + s;
    }
}
