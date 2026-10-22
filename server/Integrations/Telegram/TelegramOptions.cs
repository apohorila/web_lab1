namespace server.Integrations.Telegram;

public class TelegramOptions
{

    public bool Enabled { get; set; } = true;

    public string BotToken { get; set; } = string.Empty;


    public string ChatId { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.telegram.org";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(ChatId);
}
