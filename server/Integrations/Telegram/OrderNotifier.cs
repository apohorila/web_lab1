using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace server.Integrations.Telegram;

public interface IOrderNotifier
{
    Task OrderCreatedAsync(OrderNotification order, CancellationToken cancellationToken = default);

    Task OrderCancelledAsync(OrderNotification order, CancellationToken cancellationToken = default);
}

public class OrderNotifier : IOrderNotifier
{
    private readonly ITelegramClient _telegram;
    private readonly IOptionsMonitor<TelegramOptions> _options;
    private readonly ILogger<OrderNotifier> _logger;

    public OrderNotifier(
        ITelegramClient telegram,
        IOptionsMonitor<TelegramOptions> options,
        ILogger<OrderNotifier> logger
    )
    {
        _telegram = telegram;
        _options = options;
        _logger = logger;
    }

    public Task OrderCreatedAsync(
        OrderNotification order,
        CancellationToken cancellationToken = default
    ) => SendAsync(TelegramMessages.NewOrder(order), cancellationToken);

    public Task OrderCancelledAsync(
        OrderNotification order,
        CancellationToken cancellationToken = default
    ) => SendAsync(TelegramMessages.OrderCancelled(order), cancellationToken);

    private async Task SendAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.CurrentValue.Enabled || !_telegram.IsConfigured)
            {
                return;
            }

            await _telegram.SendMessageAsync(text, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Сповіщення в Telegram пропущено: {Reason}", ex.Message);
        }
    }
}
