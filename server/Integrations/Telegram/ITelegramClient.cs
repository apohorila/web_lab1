namespace server.Integrations.Telegram;

public interface ITelegramClient
{

    bool IsConfigured { get; }

    Task<bool> SendMessageAsync(string htmlText, CancellationToken cancellationToken = default);
}
