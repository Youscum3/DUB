using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

var builder = WebApplication.CreateBuilder(args);

// Добавляем поддержку контроллеров
builder.Services.AddControllers();

var app = builder.Build();

// Настройка порта для Railway
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");

app.MapControllers();

// Читаем токен
var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

if (!string.IsNullOrEmpty(token))
{
    var botClient = new TelegramBotClient(token);

    // Сброс вебхука
    await botClient.DeleteWebhookAsync();

    botClient.StartReceiving(
        async (bot, update, ct) =>
        {
            if (update.Message is { Text: { } messageText } message)
            {
                var chatId = message.Chat.Id;
                var text = messageText.ToLower();

                if (text.StartsWith("/start"))
                {
                    await bot.SendTextMessageAsync(chatId, "Привет! Нажми кнопку 'Прайс', чтобы увидеть цены.");
                }
                else if (text.StartsWith("/price") || text.StartsWith("/цена"))
                {
                    // Создаём кнопки с вариантами букетов
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new []
                        {
                            InlineKeyboardButton.WithCallbackData("Букет из 31 розы", "price_31"),
                            InlineKeyboardButton.WithCallbackData("Букет из 51 розы", "price_51")
                        }
                    });

                    await bot.SendTextMessageAsync(chatId, "Выберите букет:", replyMarkup: keyboard);
                }
            }
            else if (update.CallbackQuery is { Data: { } callbackData })
            {
                var chatId = update.CallbackQuery.Message.Chat.Id;

                // Проверяем, какую кнопку нажали
                if (callbackData == "price_31")
                {
                    await botClient.SendTextMessageAsync(chatId, "Букет из 31 розы — 1999₽");
                }
                else if (callbackData == "price_51")
                {
                    await botClient.SendTextMessageAsync(chatId, "Букет из 51 розы — 2999₽");
                }

                // Можно закрыть сообщение с кнопками (необязательно)
                await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
            }
        },
        async (bot, ex, ct) => Console.WriteLine("Ошибка: " + ex.Message)
    );

    Console.WriteLine("Бот успешно запущен!");
}

app.Run();
