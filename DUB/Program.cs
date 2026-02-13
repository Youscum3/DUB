using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");
app.MapControllers();

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

// Словарь для состояния цветов
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

                // Цветы
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

                // /start — главное меню
                if (messageText.ToLower().StartsWith("/start"))
                {
                    var mainKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("Цены", "/price") },
                        new [] { InlineKeyboardButton.WithCallbackData("Доставка", "delivery") },
                        new [] { InlineKeyboardButton.WithCallbackData("Контакты", "contacts") }
                    });
                    await bot.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: mainKeyboard);
                }
                // Прайс
                else if (messageText.ToLower().StartsWith("/price"))
                {
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("Розы", "category_roses") },
                        new [] { InlineKeyboardButton.WithCallbackData("Тюльпаны", "category_tulips") },
                        new [] { InlineKeyboardButton.WithCallbackData("Георгины", "category_dahlias") }
                    });
                    await bot.SendTextMessageAsync(chatId, "Выберите категорию:", replyMarkup: keyboard);
                }
                // Доставка
                else if (messageText.ToLower().StartsWith("/delivery"))
                {
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("ПМР", "delivery_pmr") },
                        new [] { InlineKeyboardButton.WithCallbackData("Молдова", "delivery_moldova") },
                        new [] { InlineKeyboardButton.WithCallbackData("Другие страны", "delivery_other") }
                    });
                    await bot.SendTextMessageAsync(chatId, "Откуда вы?", replyMarkup: keyboard);
                }
                // Контакты через текст
                else if (messageText.ToLower() == "контакты" || messageText.ToLower() == "/contacts")
                {
                    var contactsKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithUrl("Instagram: bouquet_dubossary", "https://www.instagram.com/bouquet_dubossary?igsh=ZDhzeHpzZmNiMWE5&utm_source=qr") }
                    });

                    string telegramText = "Telegram: @youscum1";

                    await bot.SendTextMessageAsync(chatId, "Наши контакты:", replyMarkup: contactsKeyboard);
                    await bot.SendTextMessageAsync(chatId, telegramText);
                }
            }
            else if (update.CallbackQuery is { Data: { } callbackData })
            {
                var chatId = update.CallbackQuery.Message.Chat.Id;

                switch (callbackData)
                {
                    // Цветы
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

                    // Доставка — первый уровень
                    case "delivery_pmr":
                        var pmrCities = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithCallbackData("Каменка", "pmr_kamenka") },
                            new [] { InlineKeyboardButton.WithCallbackData("Рыбница", "pmr_rybnica") },
                            new [] { InlineKeyboardButton.WithCallbackData("Дубоссары", "pmr_dubossary") },
                            new [] { InlineKeyboardButton.WithCallbackData("Григориополь", "pmr_grigoriopol") },
                            new [] { InlineKeyboardButton.WithCallbackData("Тирасполь", "pmr_tiraspol") },
                            new [] { InlineKeyboardButton.WithCallbackData("Бендеры", "pmr_bendery") },
                            new [] { InlineKeyboardButton.WithCallbackData("Слободзея", "pmr_slobodeya") },
                        });
                        await botClient.SendTextMessageAsync(chatId, "Выберите город:", replyMarkup: pmrCities);
                        break;

                    case "delivery_moldova":
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

                    // Города ПМР — два типа
                    case "pmr_kamenka":
                    case "pmr_rybnica":
                    case "pmr_grigoriopol":
                    case "pmr_bendery":
                    case "pmr_slobodeya":
                    case "pmr_knopki":
                        var pmrDeliveryKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithCallbackData("Маршрутка", $"{callbackData}_bus") },
                            new [] { InlineKeyboardButton.WithCallbackData("Почта", $"{callbackData}_mail") }
                        });
                        await botClient.SendTextMessageAsync(chatId, "Выберите способ доставки:", replyMarkup: pmrDeliveryKeyboard);
                        break;

                    case "pmr_dubossary":
                    case "pmr_tiraspol":
                        await botClient.SendTextMessageAsync(chatId, "Личная встреча");
                        break;

                    // Контакты через кнопку
                    case "contacts":
                        var contactsKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithUrl("Instagram: bouquet_dubossary", "https://www.instagram.com/bouquet_dubossary?igsh=ZDhzeHpzZmNiMWE5&utm_source=qr") }
                        });
                        string telegramText = "Telegram: @youscum1";
                        await botClient.SendTextMessageAsync(chatId, "Наши контакты:", replyMarkup: contactsKeyboard);
                        await botClient.SendTextMessageAsync(chatId, telegramText);
                        break;

                    // Доставка по выбранному способу
                    default:
                        if (callbackData.EndsWith("_bus"))
                            await botClient.SendTextMessageAsync(chatId, "Вы выбрали доставку по маршрутке.");
                        else if (callbackData.EndsWith("_mail"))
                            await botClient.SendTextMessageAsync(chatId, "Вы выбрали доставку по почте.");
                        else if (callbackData == "moldova_nova")
                            await botClient.SendTextMessageAsync(chatId, "Вы выбрали доставку через Nova Poshta.");
                        else if (callbackData == "moldova_bus")
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
