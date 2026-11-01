using System.Net;
using System.Text.Json;
using poker_web_api.Auth;

namespace poker_web_api.Tests.Unit;

public class TelegramBotClientTests
{
    /// <summary>Captures the outgoing request and returns a canned response body.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _responseJson;
        private readonly HttpStatusCode _status;

        public HttpRequestMessage? Request { get; private set; }
        public string? CapturedBody { get; private set; }

        public CapturingHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
        {
            _responseJson = responseJson;
            _status = status;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            if (request.Content is not null)
            {
                CapturedBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_responseJson),
            };
        }
    }

    [Fact]
    public async Task SendText_Posts_To_SendMessage_And_Reports_Success()
    {
        var handler = new CapturingHandler("""{"ok":true,"result":{}}""");
        using var http = new HttpClient(handler);
        var client = new TelegramBotClient(http);

        var ok = await client.SendTextAsync("TOKEN", "-5526414465", "02.01.2024\n\n#1 Alice in:-50 out:120 net:+70");

        Assert.True(ok);
        Assert.NotNull(handler.Request);
        Assert.EndsWith("/sendMessage", handler.Request!.RequestUri!.AbsolutePath);

        var body = handler.CapturedBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("-5526414465", doc.RootElement.GetProperty("chat_id").GetString());
        Assert.Contains("#1 Alice", doc.RootElement.GetProperty("text").GetString());
        Assert.True(doc.RootElement.GetProperty("disable_web_page_preview").GetBoolean());
    }

    [Fact]
    public async Task SendText_Reports_Failure_When_Telegram_Not_Ok()
    {
        var handler = new CapturingHandler("""{"ok":false,"description":"nope"}""");
        using var http = new HttpClient(handler);
        var client = new TelegramBotClient(http);

        var ok = await client.SendTextAsync("TOKEN", "-100", "hi");

        Assert.False(ok);
    }

    [Fact]
    public async Task SendText_Reports_Failure_On_Empty_Token_Or_Chat()
    {
        var handler = new CapturingHandler("""{"ok":true}""");
        using var http = new HttpClient(handler);
        var client = new TelegramBotClient(http);

        Assert.False(await client.SendTextAsync("", "-100", "hi"));
        Assert.False(await client.SendTextAsync("TOKEN", "", "hi"));
        Assert.Null(handler.Request); // never sent
    }
}
