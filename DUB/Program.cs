using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");
app.MapControllers();

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

// Словарь для хранения состояния пользователя
// Значение: "roses", "tulips", "dahlias" — чтобы бот знал, что считать
var userState = new Dictionary<long, string>();

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

                // Проверяем, ждём ли мы от пользователя количество цветов
                if (userState.ContainsKey(chatId))
                {
                    if (int.TryParse(messageText, out int count))
                    {
                        decimal pricePerUnit = 0;
                        string flowerName = userState[chatId];

                        switch (flowerName)
                        {
                            case "roses": pricePerUnit = 8.6m; break;
                            case "tulips": pricePerUnit = 6.6m; break;
                            case "dahlias": pricePerUnit = 13m; break;
                        }

                        decimal total = count * pricePerUnit;
                        int roundedTotal = (int)Math.Round(total, 0, MidpointRounding.AwayFromZero);

                        await bot.SendTextMessageAsync(chatId, $"Цена за {count} {flowerName}: {roundedTotal}₽");
                        userState.Remove(chatId); // убираем состояние
                    }
                    else
                    {
                        await bot.SendTextMessageAsync(chatId, "Пожалуйста, введите число.");
                    }
                    return;
                }

                if (messageText.ToLower().StartsWith("/start"))
                {
                    await bot.SendTextMessageAsync(chatId, "Привет! Нажми кнопку 'Прайс', чтобы увидеть категории цветов.");
                }
                else if (messageText.ToLower().StartsWith("/price") || messageText.ToLower().StartsWith("/цена"))
                {
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("Розы", "category_roses") },
                        new [] { InlineKeyboardButton.WithCallbackData("Тюльпаны", "category_tulips") },
                        new [] { InlineKeyboardButton.WithCallbackData("Георгины", "category_dahlias") }
                    });
                    await bot.SendTextMessageAsync(chatId, "Выберите категорию:", replyMarkup: keyboard);
                }
            }
            else if (update.CallbackQuery is { Data: { } callbackData })
            {
                var chatId = update.CallbackQuery.Message.Chat.Id;

                // Устанавливаем состояние в зависимости от выбранной категории
                switch (callbackData)
                {
                    case "category_roses":
                        userState[chatId] = "roses";
                        await botClient.SendTextMessageAsync(chatId, "Введите, сколько штук вам нужно.");
                        break;

                    case "category_tulips":
                        userState[chatId] = "tulips";
                        await botClient.SendTextMessageAsync(chatId, "Введите, сколько штук вам нужно.");
                        break;

                    case "category_dahlias":
                        userState[chatId] = "dahlias";
                        await botClient.SendTextMessageAsync(chatId, "Введите, сколько штук вам нужно.");
                        break;
                }

                await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
            }
        },
        async (bot, ex, ct) => Console.WriteLine("Ошибка: " + ex.Message)
    );

    Console.WriteLine("Бот успешно запущен!");
}

app.Run();
