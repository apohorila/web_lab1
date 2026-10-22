using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using server.Integrations.Telegram;

namespace server.Controllers;

[ApiController]
[Route("api/integrations/telegram")]
[Authorize(Roles = "Admin")]
public class TelegramController : ControllerBase
{
    private readonly ITelegramClient _telegram;
    private readonly IOptionsMonitor<TelegramOptions> _options;

    public TelegramController(ITelegramClient telegram, IOptionsMonitor<TelegramOptions> options)
    {
        _telegram = telegram;
        _options = options;
    }


    [HttpGet("status")]
    public IActionResult GetStatus() =>
        Ok(new { configured = _telegram.IsConfigured, enabled = _options.CurrentValue.Enabled });


    [HttpPost("test")]
    public async Task<IActionResult> SendTest()
    {
        if (!_telegram.IsConfigured)
        {
            return BadRequest(
                new { sent = false, message = "Вкажіть Telegram:BotToken та Telegram:ChatId" }
            );
        }

        var sent = await _telegram.SendMessageAsync(TelegramMessages.Test());

        return sent
            ? Ok(new { sent = true })
            : StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    sent = false,
                    message = "Telegram не прийняв повідомлення (перевірте токен і chat id)",
                }
            );
    }
}
