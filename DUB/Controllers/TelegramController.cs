using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

[ApiController]
[Route("api/update")]
public class TelegramController : ControllerBase
{
    private readonly TelegramBotClient bot;

    public TelegramController()
    {
        var token = Environment.GetEnvironmentVariable("BOT_TOKEN")
                    ?? throw new Exception("BOT_TOKEN не найден");
        bot = new TelegramBotClient(token);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Update update)
    {
        if (update.Type != UpdateType.Message || update.Message?.Text == null)
            return Ok();

        var chatId = update.Message.Chat.Id;
        var text = update.Message.Text;

        if (text == "/start")
            await bot.SendTextMessageAsync(chatId, "Привет 👋\nЯ работаю на бесплатном сервере 🚀");
        else if (text == "/help")
            await bot.SendTextMessageAsync(chatId, "/start - Запуск\n/help - Помощь");
        else
            await bot.SendTextMessageAsync(chatId, $"Ты написал: {text}");

        return Ok();
    }
}
