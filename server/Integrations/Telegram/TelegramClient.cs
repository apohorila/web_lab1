using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace server.Integrations.Telegram;


public class TelegramClient : ITelegramClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<TelegramOptions> _options;
    private readonly ILogger<TelegramClient> _logger;

    public TelegramClient(
        HttpClient http,
        IOptionsMonitor<TelegramOptions> options,
        ILogger<TelegramClient> logger
    )
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public bool IsConfigured => _options.CurrentValue.IsConfigured;

    public async Task<bool> SendMessageAsync(
        string htmlText,
        CancellationToken cancellationToken = default
    )
    {
        var options = _options.CurrentValue;
        if (!options.IsConfigured)
        {
            return false;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);

            var url = $"{options.BaseUrl.TrimEnd('/')}/bot{options.BotToken}/sendMessage";
            using var response = await _http.PostAsJsonAsync(
                url,
                new
                {
                    chat_id = options.ChatId,
                    text = htmlText,
                    parse_mode = "HTML",
                    disable_web_page_preview = true,
                },
                timeout.Token
            );

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogWarning(
                "Telegram відхилив повідомлення: HTTP {Status}",
                (int)response.StatusCode
            );
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Не вдалося надіслати повідомлення в Telegram: {Reason}", ex.Message);
            return false;
        }
    }
}
