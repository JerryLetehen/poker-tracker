using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using poker_web_api.Auth;
using poker_web_api.Data;

namespace poker_web_api.Tests.Integration;

public class PublishGameResultsTests
{
    private sealed class FakeBot : ITelegramBotClient
    {
        public int TextCalls { get; private set; }
        public string? ChatId { get; private set; }
        public string? BotToken { get; private set; }
        public string? Text { get; private set; }
        public bool Result { get; set; } = true;

        public Task SendMessageAsync(string botToken, long chatId, string text, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> SendTextAsync(
            string botToken, string chatId, string text, bool disableWebPagePreview = true,
            string? parseMode = null, CancellationToken ct = default)
        {
            TextCalls++;
            BotToken = botToken;
            ChatId = chatId;
            Text = text;
            return Task.FromResult(Result);
        }
    }

    private sealed class PublishTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = "PublishTestDb_" + Guid.NewGuid();
        private readonly string? _resultsChatId;

        public FakeBot Bot { get; } = new();

        public PublishTestFactory(string? resultsChatId) => _resultsChatId = resultsChatId;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("TelegramAuth:PublicBotToken", "111:PUBLIC_BOT");
            builder.UseSetting("TelegramAuth:PublicBotUsername", "poker_tracker_local_bot");
            if (_resultsChatId is not null)
            {
                builder.UseSetting("TelegramAuth:ResultsChatId", _resultsChatId);
            }

            builder.ConfigureServices(services =>
            {
                var toRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<PokerDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                    .ToList();
                foreach (var d in toRemove) services.Remove(d);

                var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(PokerDbContext));
                if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

                services.AddDbContext<PokerDbContext>(options => options.UseInMemoryDatabase(_dbName));

                var botDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ITelegramBotClient));
                if (botDescriptor != null) services.Remove(botDescriptor);
                services.AddSingleton<ITelegramBotClient>(Bot);
            });

            builder.UseEnvironment("Testing");
        }

        /// <summary>Seeds a game with two players (Alice +70, Bob -20) and returns its id.</summary>
        public int SeedGame()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PokerDbContext>();
            var alice = TestDbHelper.AddPlayer(db, "Alice");
            var bob = TestDbHelper.AddPlayer(db, "Bob");
            var game = TestDbHelper.AddCompleteGame(db, new DateOnly(2024, 1, 2), buyIn: 50,
                (alice, 120m), (bob, 30m));
            return game.Id;
        }
    }

    private static StringContent EmptyJson() => new("{}", System.Text.Encoding.UTF8, "application/json");

    [Fact]
    public async Task Publish_ValidGame_SendsTextAndReturnsOk()
    {
        using var factory = new PublishTestFactory(resultsChatId: "-5526414465");
        var client = factory.CreateClient();
        var gameId = factory.SeedGame();

        var response = await client.PostAsync($"/admin/games/{gameId}/publish", EmptyJson());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.Bot.TextCalls);
        Assert.Equal("-5526414465", factory.Bot.ChatId);
        Assert.Equal("111:PUBLIC_BOT", factory.Bot.BotToken);

        var text = factory.Bot.Text!;
        Assert.StartsWith("02.01.2024", text);
        // Alice (+70) is sorted before Bob (-20).
        var aliceIdx = text.IndexOf("Alice", StringComparison.Ordinal);
        var bobIdx = text.IndexOf("Bob", StringComparison.Ordinal);
        Assert.True(aliceIdx >= 0 && bobIdx > aliceIdx);
        // Rank 1 uses the keycap emoji; value is bold (HTML), 2 decimals, no "net"/in/out words.
        Assert.Contains("1️⃣ Alice <b>+70.00</b>", text);
        Assert.Contains("2️⃣ Bob <b>-20.00</b>", text);
        Assert.DoesNotContain("net:", text);
        Assert.DoesNotContain("in:", text);
        Assert.DoesNotContain("out:", text);
        // Deep links present, with a blank line between them.
        Assert.Contains($"🎮 Game: https://t.me/poker_tracker_local_bot?startapp=game_{gameId}", text);
        Assert.Contains("\n\n🏆 Leaderboard: https://t.me/poker_tracker_local_bot?startapp=leaderboard", text);
    }

    [Fact]
    public async Task Publish_ChatIdNotConfigured_Returns400()
    {
        using var factory = new PublishTestFactory(resultsChatId: "");
        var client = factory.CreateClient();
        var gameId = factory.SeedGame();

        var response = await client.PostAsync($"/admin/games/{gameId}/publish", EmptyJson());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Bot.TextCalls);
    }

    [Fact]
    public async Task Publish_NonExistentGame_Returns404()
    {
        using var factory = new PublishTestFactory(resultsChatId: "-5526414465");
        var client = factory.CreateClient();

        var response = await client.PostAsync("/admin/games/99999/publish", EmptyJson());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, factory.Bot.TextCalls);
    }

    [Fact]
    public async Task Publish_TelegramFailure_Returns502()
    {
        using var factory = new PublishTestFactory(resultsChatId: "-5526414465");
        factory.Bot.Result = false;
        var client = factory.CreateClient();
        var gameId = factory.SeedGame();

        var response = await client.PostAsync($"/admin/games/{gameId}/publish", EmptyJson());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(1, factory.Bot.TextCalls);
    }
}
