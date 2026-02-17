using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");
app.MapControllers();

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

// Состояние пользователя
var userState = new Dictionary<long, string>();
var userQuantity = new Dictionary<long, int>();
var userExtras = new Dictionary<long, List<string>>();
var userFlower = new Dictionary<long, string>();
var userDate = new Dictionary<long, DateTime>();

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
                var username = message.From.Username ?? message.From.FirstName;

                // --- СТАРЫЙ КОД: КАЛЬКУЛЯТОР ЦЕН ---
                if (userState.ContainsKey(chatId) && (userState[chatId] == "roses" || userState[chatId] == "tulips" || userState[chatId] == "dahlias"))
                {
                    if (int.TryParse(messageText, out int count))
                    {
                        decimal pricePerUnit = 0;
                        string flower = userState[chatId];

                        switch (flower)
                        {
                            case "roses": pricePerUnit = 8.6m; break;
                            case "tulips": pricePerUnit = 6.6m; break;
                            case "dahlias": pricePerUnit = 13m; break;
                        }

                        decimal total = count * pricePerUnit;
                        int rounded = (int)Math.Round(total, 0, MidpointRounding.AwayFromZero);

                        await bot.SendTextMessageAsync(
                            chatId,
                            $"Цена: {rounded}₽\n\nМожете ввести другое количество."
                        );
                    }
                    else
                    {
                        await bot.SendTextMessageAsync(chatId, "Введите число.");
                    }
                    return;
                }

                // Ввод количества для ЗАКАЗА (новый код)
                if (userState.ContainsKey(chatId) && userState[chatId] == "await_quantity")
                {
                    if (int.TryParse(messageText, out int count))
                    {
                        userQuantity[chatId] = count;
                        userState[chatId] = "await_extras";

                        var extrasKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithCallbackData("Блёстки", "extra_glitter"), InlineKeyboardButton.WithCallbackData("Картинка", "extra_picture") },
                            new [] { InlineKeyboardButton.WithCallbackData("Игрушка", "extra_toy"), InlineKeyboardButton.WithCallbackData("Бабочки", "extra_butterfly") },
                            new [] { InlineKeyboardButton.WithCallbackData("Бантики", "extra_ribbons") },
                            new [] { InlineKeyboardButton.WithCallbackData("✅ Готово", "extras_done") }
                        });

                        await botClient.SendTextMessageAsync(chatId, "Выберите дополнительные элементы (можно несколько):", replyMarkup: extrasKeyboard);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "Пожалуйста, введите число.");
                    }
                    return;
                }

                // Команды
                if (messageText.ToLower().StartsWith("/start"))
                    await ShowMainMenu(chatId);
                else if (messageText.ToLower().StartsWith("/price"))
                    await ShowPriceMenu(chatId);
                else if (messageText.ToLower().StartsWith("/order"))
                    await ShowOrderMenu(chatId);
                else if (messageText.ToLower().StartsWith("/contacts"))
                    await ShowContacts(chatId);
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
            }
            else if (update.CallbackQuery is { Data: { } data })
            {
                var chatId = update.CallbackQuery.Message.Chat.Id;
                var callbackData = data;

                switch (callbackData)
                {
                    case "start_price":
                        await ShowPriceMenu(chatId);
                        break;
                    case "start_order": await ShowOrderMenu(chatId); break;
                    case "start_contacts": await ShowContacts(chatId); break;
                    case "start_delivery": await ShowDeliveryMenu(chatId); break;

                    // --- СТАРЫЙ КОД: ВЫБОР КАТЕГОРИИ ДЛЯ ЦЕНЫ ---
                    case "category_roses":
                        userState[chatId] = "roses";
                        await botClient.SendTextMessageAsync(chatId, "Введите количество роз:");
                        break;
                    case "category_tulips":
                        userState[chatId] = "tulips";
                        await botClient.SendTextMessageAsync(chatId, "Введите количество тюльпанов:");
                        break;
                    case "category_dahlias":
                        userState[chatId] = "dahlias";
                        await botClient.SendTextMessageAsync(chatId, "Введите количество георгин:");
                        break;

                    // Доставка
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

                    // Категории цветов (ДЛЯ ЗАКАЗА)
                    case "order_roses":
                    case "order_tulips":
                    case "order_dahlias":
                        userFlower[chatId] = callbackData.Substring(6);
                        userState[chatId] = "await_quantity";
                        await botClient.SendTextMessageAsync(chatId, "Введите количество:");
                        break;

                    case "extras_done":
                        userState[chatId] = "await_date";
                        await ShowCalendar(chatId, DateTime.Today.Year, DateTime.Today.Month);
                        break;

                    default:
                        if (callbackData.StartsWith("date_"))
                        {
                            // ... (Логика оформления чека)
                            var dateSelected = DateTime.ParseExact(callbackData.Substring(5), "yyyy-MM-dd", null);
                            decimal pricePerUnit = userFlower[chatId] switch { "roses" => 8.6m, "tulips" => 6.6m, "dahlias" => 13m, _ => 0m };
                            int rounded = (int)Math.Round(userQuantity[chatId] * pricePerUnit, 0);
                            string username = update.CallbackQuery.From.Username ?? update.CallbackQuery.From.FirstName;
                            await botClient.SendTextMessageAsync(chatId, $"✅ Чек: {userFlower[chatId]}, Сумма: {rounded}₽, Дата: {dateSelected:dd.MM.yyyy}");
                            userState.Remove(chatId);
                        }
                        else if (callbackData.EndsWith("_bus"))
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
        async (bot, ex, ct) => Console.WriteLine(ex.Message)
    );

    // ==== МЕНЮ ====
    async Task ShowMainMenu(long chatId)
    {
        var mainKeyboard = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("Цены", "start_price") },
            new [] { InlineKeyboardButton.WithCallbackData("Доставка", "start_delivery") },
            new [] { InlineKeyboardButton.WithCallbackData("Контакты", "start_contacts") },
            new [] { InlineKeyboardButton.WithCallbackData("Сделать заказ", "start_order") }
        });
        await botClient.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: mainKeyboard);
    }

    async Task ShowPriceMenu(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("Розы", "category_roses") },
            new [] { InlineKeyboardButton.WithCallbackData("Тюльпаны", "category_tulips") },
            new [] { InlineKeyboardButton.WithCallbackData("Георгины", "category_dahlias") }
        });
        await botClient.SendTextMessageAsync(chatId, "Выберите категорию:", replyMarkup: keyboard);
    }

    async Task ShowOrderMenu(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("Розы", "order_roses") },
            new [] { InlineKeyboardButton.WithCallbackData("Тюльпаны", "order_tulips") },
            new [] { InlineKeyboardButton.WithCallbackData("Георгины", "order_dahlias") }
        });
        await botClient.SendTextMessageAsync(chatId, "Выберите букет:", replyMarkup: keyboard);
    }

    async Task ShowContacts(long chatId)
    {
        await botClient.SendTextMessageAsync(chatId, "Telegram: @Youscam");
    }

    async Task ShowDeliveryMenu(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("ПМР", "delivery_pmr") },
            new [] { InlineKeyboardButton.WithCallbackData("Молдова", "delivery_moldova") }
        });
        await botClient.SendTextMessageAsync(chatId, "Выберите регион:", replyMarkup: keyboard);
    }

    async Task ShowCalendar(long chatId, int year, int month) { /* ... код календаря ... */ }

    Console.WriteLine("Бот запущен!");
}
app.Run();