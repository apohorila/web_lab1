using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using server.Integrations.Telegram;
using Xunit;

namespace server.Tests;


public class TelegramTests
{


    private sealed class OptionsMonitor : IOptionsMonitor<TelegramOptions>
    {
        public OptionsMonitor(TelegramOptions value)
        {
            CurrentValue = value;
        }

        public TelegramOptions CurrentValue { get; set; }

        public TelegramOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<TelegramOptions, string?> listener) => null;
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = new();

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public Exception? Throw { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request, body));

            if (Throw != null)
            {
                throw Throw;
            }

            return new HttpResponseMessage(Status);
        }
    }

    private sealed class FakeTelegram : ITelegramClient
    {
        public bool IsConfigured { get; set; } = true;

        public bool ThrowOnSend { get; set; }

        public List<string> Sent { get; } = new();

        public Task<bool> SendMessageAsync(string htmlText, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSend)
            {
                throw new InvalidOperationException("boom");
            }

            Sent.Add(htmlText);
            return Task.FromResult(true);
        }
    }

    private static TelegramOptions Configured(string baseUrl = "https://api.telegram.org") =>
        new()
        {
            BotToken = "123:ABC",
            ChatId = "-100500",
            BaseUrl = baseUrl,
        };

    private static TelegramClient ClientFor(FakeHandler handler, TelegramOptions options) =>
        new(new HttpClient(handler), new OptionsMonitor(options), NullLogger<TelegramClient>.Instance);

    private static OrderNotification SampleOrder(int lines = 1, bool guest = false, string name = "Іван Петренко") =>
        new(
            12,
            name,
            "+380991112233",
            "ivan@example.com",
            "Київ",
            "Відділення №1",
            7200m,
            Enumerable.Range(1, lines).Select(i => new OrderLine($"Сумка Alma {i}", 2, 3600m)).ToList(),
            guest
        );



    [Fact]
    public async Task Client_PostsToSendMessage_WithChatIdTextAndHtmlParseMode()
    {
        var handler = new FakeHandler();

        var sent = await ClientFor(handler, Configured()).SendMessageAsync("<b>Привіт</b>");

        Assert.True(sent);
        Assert.Equal(1, handler.Calls.Count);
        var (request, body) = handler.Calls[0];
        Assert.Equal("POST", request.Method.Method);
        Assert.Equal("https://api.telegram.org/bot123:ABC/sendMessage", request.RequestUri!.ToString());

        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal("-100500", json.GetProperty("chat_id").GetString());
        Assert.Equal("<b>Привіт</b>", json.GetProperty("text").GetString());
        Assert.Equal("HTML", json.GetProperty("parse_mode").GetString());
    }

    [Fact]
    public async Task Client_WhenTelegramRejectsTheToken_ReturnsFalseWithoutThrowing()
    {
        var handler = new FakeHandler { Status = HttpStatusCode.Unauthorized };

        Assert.False(await ClientFor(handler, Configured()).SendMessageAsync("x"));
    }

    [Fact]
    public async Task Client_WhenTheNetworkFails_ReturnsFalseWithoutThrowing()
    {
        var handler = new FakeHandler { Throw = new HttpRequestException("Name or service not known") };

        Assert.False(await ClientFor(handler, Configured()).SendMessageAsync("x"));
    }

    [Fact]
    public async Task Client_WhenNotConfigured_DoesNotTouchTheNetwork()
    {
        var handler = new FakeHandler();
        var client = ClientFor(handler, new TelegramOptions());

        Assert.False(client.IsConfigured);
        Assert.False(await client.SendMessageAsync("x"));
        Assert.Equal(0, handler.Calls.Count);
    }

    [Fact]
    public async Task Client_UsesTheConfiguredBaseUrl()
    {
        var handler = new FakeHandler();

        await ClientFor(handler, Configured("http://localhost:9999/")).SendMessageAsync("x");

        Assert.Equal("http://localhost:9999/bot123:ABC/sendMessage", handler.Calls[0].Request.RequestUri!.ToString());
    }



    [Fact]
    public void NewOrderMessage_ContainsTheKeyFacts()
    {
        var text = TelegramMessages.NewOrder(SampleOrder());

        Assert.Contains("Нове замовлення #12", text);
        Assert.Contains("Іван Петренко", text);
        Assert.Contains("+380991112233", text);
        Assert.Contains("ivan@example.com", text);
        Assert.Contains("Київ, Відділення №1", text);
        Assert.Contains("• Сумка Alma 1 × 2 — ₴7 200", text);
        Assert.Contains("Разом: ₴7 200", text);
        Assert.DoesNotContain("(гість)", text);
    }

    [Fact]
    public void GuestOrders_AreMarked()
    {
        Assert.Contains("(гість)", TelegramMessages.NewOrder(SampleOrder(guest: true)));
    }

    [Fact]
    public void UserProvidedText_IsHtmlEscaped()
    {
        var text = TelegramMessages.NewOrder(SampleOrder(name: "<b>Хакер</b> & <a href=x>link</a>"));

        Assert.Contains("&lt;b&gt;Хакер&lt;/b&gt; &amp; &lt;a href=x&gt;link&lt;/a&gt;", text);
        Assert.DoesNotContain("<a href", text);
    }

    [Fact]
    public void LongOrders_AreTruncatedToStayWithinTelegramLimits()
    {
        var text = TelegramMessages.NewOrder(SampleOrder(lines: 20));

        Assert.Contains("• Сумка Alma 15 ×", text);
        Assert.DoesNotContain("• Сумка Alma 16 ×", text);
        Assert.Contains("… і ще 5 поз.", text);
        Assert.True(text.Length < 4096);
    }

    [Fact]
    public void CancelledMessage_SaysCancelled()
    {
        Assert.Contains("Замовлення #12 скасовано", TelegramMessages.OrderCancelled(SampleOrder()));
    }



    private static (OrderNotifier notifier, FakeTelegram telegram, OptionsMonitor options) NotifierSetup()
    {
        var options = new OptionsMonitor(new TelegramOptions { Enabled = true });
        var telegram = new FakeTelegram();
        return (new OrderNotifier(telegram, options, NullLogger<OrderNotifier>.Instance), telegram, options);
    }

    [Fact]
    public async Task NewOrder_IsSent_WhenEnabledAndConfigured()
    {
        var (notifier, telegram, _) = NotifierSetup();

        await notifier.OrderCreatedAsync(SampleOrder());

        Assert.Equal(1, telegram.Sent.Count);
        Assert.Contains("Нове замовлення #12", telegram.Sent[0]);
    }

    [Fact]
    public async Task CancelledOrder_IsSent()
    {
        var (notifier, telegram, _) = NotifierSetup();

        await notifier.OrderCancelledAsync(SampleOrder());

        Assert.Contains("скасовано", telegram.Sent[0]);
    }

    [Fact]
    public async Task NothingIsSent_WhenTelegramIsDisabledInSettings()
    {
        var (notifier, telegram, options) = NotifierSetup();
        options.CurrentValue = new TelegramOptions { Enabled = false };

        await notifier.OrderCreatedAsync(SampleOrder());

        Assert.Equal(0, telegram.Sent.Count);
    }

    [Fact]
    public async Task NothingIsSent_WhenTheBotIsNotConfigured()
    {
        var (notifier, telegram, _) = NotifierSetup();
        telegram.IsConfigured = false;

        await notifier.OrderCreatedAsync(SampleOrder());

        Assert.Equal(0, telegram.Sent.Count);
    }

    [Fact]
    public async Task ATelegramFailure_NeverBreaksTheOrder()
    {
        var (notifier, telegram, _) = NotifierSetup();
        telegram.ThrowOnSend = true;

        await notifier.OrderCreatedAsync(SampleOrder()); // не має кинути виняток

        Assert.Equal(0, telegram.Sent.Count);
    }
}
