using System.Text.Json;
using poker_web_api.Auth;

namespace poker_web_api.Tests.Unit;

public class BotWebhookServiceTests
{
    private sealed class FakeBot : ITelegramBotClient
    {
        public long? ChatId { get; private set; }
        public string? Text { get; private set; }
        public int Calls { get; private set; }

        public Task SendMessageAsync(string botToken, long chatId, string text, CancellationToken ct = default)
        {
            Calls++;
            ChatId = chatId;
            Text = text;
            return Task.CompletedTask;
        }
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task Start_Replies_With_User_Id()
    {
        var bot = new FakeBot();
        var service = new BotWebhookService(bot);

        var update = Parse("""
            {"message":{"text":"/start","from":{"id":12345},"chat":{"id":12345}}}
            """);

        var result = await service.HandleUpdateAsync("token", update);

        Assert.Equal(1, bot.Calls);
        Assert.Equal(12345, bot.ChatId);
        Assert.Contains("12345", bot.Text!);
        Assert.Contains("replied id 12345", result);
    }

    [Fact]
    public async Task NonStart_Message_Is_Ignored()
    {
        var bot = new FakeBot();
        var service = new BotWebhookService(bot);

        var update = Parse("""
            {"message":{"text":"hello","from":{"id":1},"chat":{"id":1}}}
            """);

        var result = await service.HandleUpdateAsync("token", update);

        Assert.Equal(0, bot.Calls);
        Assert.Contains("ignored", result);
    }

    [Fact]
    public async Task Update_Without_Message_Is_Ignored()
    {
        var bot = new FakeBot();
        var service = new BotWebhookService(bot);

        var result = await service.HandleUpdateAsync("token", Parse("""{"edited_channel_post":{}}"""));

        Assert.Equal(0, bot.Calls);
        Assert.Contains("ignored", result);
    }
}
