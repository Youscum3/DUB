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
                    await bot.SendTextMessageAsync(chatId, "Привет! Нажми кнопку 'Прайс', чтобы увидеть категории цветов.");
                }
                else if (text.StartsWith("/price") || text.StartsWith("/цена"))
                {
                    // Первый уровень меню: категории цветов
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("Тюльпаны", "category_tulips") },
                        new [] { InlineKeyboardButton.WithCallbackData("Розы", "category_roses") },
                        new [] { InlineKeyboardButton.WithCallbackData("Георгины", "category_dahlias") }
                    });

                    await bot.SendTextMessageAsync(chatId, "Выберите категорию:", replyMarkup: keyboard);
                }
            }
            else if (update.CallbackQuery is { Data: { } callbackData })
            {
                var chatId = update.CallbackQuery.Message.Chat.Id;

                // Второй уровень меню: показываем конкретные букеты и цены
                if (callbackData == "category_tulips")
                {
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("Букет 15 тюльпанов — 999₽", "price_tulips_15") },
                        new [] { InlineKeyboardButton.WithCallbackData("Букет 31 тюльпан — 1999₽", "price_tulips_31") }
                    });
                    await botClient.SendTextMessageAsync(chatId, "Выберите букет:", replyMarkup: keyboard);
                }
                else if (callbackData == "category_roses")
                {
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("Букет 31 роза — 1999₽", "price_roses_31") },
                        new [] { InlineKeyboardButton.WithCallbackData("Букет 51 роза — 2999₽", "price_roses_51") }
                    });
                    await botClient.SendTextMessageAsync(chatId, "Выберите букет:", replyMarkup: keyboard);
                }
                else if (callbackData == "category_dahlias")
                {
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("Букет 10 георгин — 899₽", "price_dahlias_10") },
                        new [] { InlineKeyboardButton.WithCallbackData("Букет 20 георгин — 1599₽", "price_dahlias_20") }
                    });
                    await botClient.SendTextMessageAsync(chatId, "Выберите букет:", replyMarkup: keyboard);
                }
                // Третий уровень: показываем цену после выбора конкретного букета
                else if (callbackData.StartsWith("price_"))
                {
                    await botClient.SendTextMessageAsync(chatId, $"Вы выбрали: {update.CallbackQuery.Data.Replace("price_", "").Replace("_", " ")}");
                }

                await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
            }
        },
        async (bot, ex, ct) => Console.WriteLine("Ошибка: " + ex.Message)
    );

    Console.WriteLine("Бот успешно запущен!");
}

app.Run();
