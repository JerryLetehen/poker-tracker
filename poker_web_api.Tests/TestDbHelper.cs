using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;
using poker_web_api.Entities;

namespace poker_web_api.Tests;

public static class TestDbHelper
{
    public static PokerDbContext CreateInMemoryDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<PokerDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new PokerDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public static Player AddPlayer(PokerDbContext db, string name)
    {
        var player = new Player { Name = name };
        db.Players.Add(player);
        db.SaveChanges();
        return player;
    }

    public static Game AddGame(PokerDbContext db, DateOnly? date = null, string gameType = "texas-holdem-no-limit", decimal buyInAmount = 50)
    {
        var game = new Game
        {
            Date = date ?? DateOnly.FromDateTime(DateTime.Now),
            GameType = gameType,
            BuyInAmount = buyInAmount
        };
        db.Games.Add(game);
        db.SaveChanges();
        return game;
    }

    public static GameEntry AddBuyIn(PokerDbContext db, int gameId, int playerId, decimal amount)
    {
        var entry = new GameEntry
        {
            GameId = gameId,
            PlayerId = playerId,
            Type = EntryType.BuyIn,
            Amount = amount
        };
        db.GameEntries.Add(entry);
        db.SaveChanges();
        return entry;
    }

    public static GameEntry AddCashOut(PokerDbContext db, int gameId, int playerId, decimal amount)
    {
        var entry = new GameEntry
        {
            GameId = gameId,
            PlayerId = playerId,
            Type = EntryType.CashOut,
            Amount = amount
        };
        db.GameEntries.Add(entry);
        db.SaveChanges();
        return entry;
    }

    /// <summary>
    /// Creates a complete game with players, buy-ins, and cash-outs.
    /// Each player buys in once with the given buyIn amount and cashes out with the given cashOut amount.
    /// </summary>
    public static Game AddCompleteGame(PokerDbContext db, DateOnly date, decimal buyIn, params (Player player, decimal cashOut)[] results)
    {
        var game = AddGame(db, date, buyInAmount: buyIn);
        foreach (var (player, cashOut) in results)
        {
            AddBuyIn(db, game.Id, player.Id, buyIn);
            AddCashOut(db, game.Id, player.Id, cashOut);
        }
        return game;
    }
}
