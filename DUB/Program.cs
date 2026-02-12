using Telegram.Bot;

TelegramBotClient? botClient = null; // объявляем вне if

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

if (!string.IsNullOrEmpty(token))
{
    botClient = new TelegramBotClient(token);

    // СБРОС ВЕБХУКА
    await botClient.DeleteWebhookAsync();

    // Запускаем обработку сообщений
    botClient.StartReceiving(
        async (bot, update, ct) => {
            if (update.Message is not { Text: { } messageText } message) return;
            var chatId = message.Chat.Id;

            if (messageText == "/start")
                await bot.SendTextMessageAsync(chatId, "Максим лох");
            else if (messageText == "/price" || messageText == "/цена")
            {
                decimal price = 199.99m;
                await bot.SendTextMessageAsync(chatId, $"Текущая цена: {price}₽");
            }
            else
                await bot.SendTextMessageAsync(chatId, $"Ты написал: {messageText}");
        },
        async (bot, ex, ct) => Console.WriteLine("Ошибка: " + ex.Message)
    );

    Console.WriteLine("Бот успешно запущен!");
}
