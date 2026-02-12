using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");
app.MapControllers();

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

// Словарь для хранения состояния пользователя (для цветов)
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

                // Если ждём количество цветов
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

                        await bot.SendTextMessageAsync(chatId, $"Цена: {roundedTotal}₽");
                        userState.Remove(chatId);
                    }
                    else
                    {
                        await bot.SendTextMessageAsync(chatId, "Пожалуйста, введите число.");
                    }
                    return;
                }

                // Команды /start и /price
                if (messageText.ToLower().StartsWith("/start"))
                {
                    await bot.SendTextMessageAsync(chatId, "Привет! Нажми кнопку 'Прайс', чтобы выбрать категорию цветов, или 'Delivery' для доставки.");
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
                else if (messageText.ToLower().StartsWith("delivery"))
                {
                    // Первый уровень доставки
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("ПМР", "delivery_pmr") },
                        new [] { InlineKeyboardButton.WithCallbackData("Молдова", "delivery_moldova") },
                        new [] { InlineKeyboardButton.WithCallbackData("Другие страны", "delivery_other") }
                    });
                    await bot.SendTextMessageAsync(chatId, "Откуда вы?", replyMarkup: keyboard);
                }
            }
            else if (update.CallbackQuery is { Data: { } callbackData })
            {
                var chatId = update.CallbackQuery.Message.Chat.Id;

                // Цветы
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

                    // Доставка
                    case "delivery_pmr":
                        // Кнопки городов ПМР
                        var citiesKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithCallbackData("Кнопки", "pmr_knopki") },
                            new [] { InlineKeyboardButton.WithCallbackData("Каменка", "pmr_kamenka") },
                            new [] { InlineKeyboardButton.WithCallbackData("Рыбница", "pmr_rybnica") },
                            new [] { InlineKeyboardButton.WithCallbackData("Дубоссары", "pmr_dubossary") },
                            new [] { InlineKeyboardButton.WithCallbackData("Григориополь", "pmr_grigoriopol") },
                            new [] { InlineKeyboardButton.WithCallbackData("Тирасполь", "pmr_tiraspol") },
                            new [] { InlineKeyboardButton.WithCallbackData("Бендеры", "pmr_bendery") },
                            new [] { InlineKeyboardButton.WithCallbackData("Слободея", "pmr_slobodeya") }
                        });
                        await botClient.SendTextMessageAsync(chatId, "Выберите город:", replyMarkup: citiesKeyboard);
                        break;

                    case "delivery_moldova":
                        // Кнопки способов доставки
                        var moldovaKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithCallbackData("Nova Poshta", "moldova_nova") },
                            new [] { InlineKeyboardButton.WithCallbackData("Маршрутки", "moldova_bus") }
                        });
                        await botClient.SendTextMessageAsync(chatId, "Выберите способ доставки:", replyMarkup: moldovaKeyboard);
                        break;

                    case "delivery_other":
                        await botClient.SendTextMessageAsync(chatId, "К сожалению, доставка только по ПМР и Молдове.");
                        break;

                    // Ответы на выбор городов ПМР
                    case "pmr_knopki":
                    case "pmr_kamenka":
                    case "pmr_rybnica":
                    case "pmr_dubossary":
                    case "pmr_grigoriopol":
                    case "pmr_tiraspol":
                    case "pmr_bendery":
                    case "pmr_slobodeya":
                        await botClient.SendTextMessageAsync(chatId, $"Вы выбрали город: {callbackData.Replace("pmr_", "").Replace("_", " ")}");
                        break;

                    // Ответы на выбор способа доставки в Молдове
                    case "moldova_nova":
                        await botClient.SendTextMessageAsync(chatId, "Вы выбрали доставку через Nova Poshta.");
                        break;
                    case "moldova_bus":
                        await botClient.SendTextMessageAsync(chatId, "Вы выбрали доставку по маршрутке.");
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
