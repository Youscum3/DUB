using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");
app.MapControllers();

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

// состояние пользователя
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
                var text = messageText.ToLower();

                // ==============================
                // 📌 ГЛАВНОЕ МЕНЮ
                // ==============================

                if (text == "/start")
                {
                    userState.Remove(chatId);

                    var menu = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { "💰 Прайс", "🚚 Доставка" },
                        new KeyboardButton[] { "🛒 Сделать заказ", "📞 Контакты" }
                    })
                    { ResizeKeyboard = true };

                    await bot.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: menu);
                    return;
                }

                // ==============================
                // 📌 ПРАЙС (без заказа)
                // ==============================

                if (text == "💰 прайс")
                {
                    await bot.SendTextMessageAsync(chatId,
                        "Цены за 1 цветок:\n🌹 Розы — 8.6 ₽\n🌷 Тюльпаны — 6.6 ₽\n🌼 Георгины — 13 ₽");
                    return;
                }

                // ==============================
                // 📌 СДЕЛАТЬ ЗАКАЗ — ШАГ 1
                // ==============================

                if (text == "🛒 сделать заказ")
                {
                    userState[chatId] = "choose_flower";

                    var flowers = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { "🌹 Розы", "🌷 Тюльпаны", "🌼 Георгины" }
                    })
                    { ResizeKeyboard = true };

                    await bot.SendTextMessageAsync(chatId, "Выберите букет:", replyMarkup: flowers);
                    return;
                }

                // ==============================
                // 📌 ВЫБОР БУКЕТА — ШАГ 2
                // ==============================

                if (userState.ContainsKey(chatId) && userState[chatId] == "choose_flower")
                {
                    if (text.Contains("роз"))
                        userState[chatId] = "roses_count";
                    else if (text.Contains("тюльпан"))
                        userState[chatId] = "tulips_count";
                    else if (text.Contains("георгин"))
                        userState[chatId] = "dahlias_count";
                    else
                    {
                        await bot.SendTextMessageAsync(chatId, "Выберите цветок кнопкой.");
                        return;
                    }

                    await bot.SendTextMessageAsync(chatId, "Введите количество:");
                    return;
                }

                // ==============================
                // 📌 ВВОД КОЛИЧЕСТВА — ШАГ 3
                // ==============================

                if (userState.ContainsKey(chatId) &&
                    (userState[chatId] == "roses_count" ||
                     userState[chatId] == "tulips_count" ||
                     userState[chatId] == "dahlias_count"))
                {
                    if (!int.TryParse(messageText, out int count) || count <= 0)
                    {
                        await bot.SendTextMessageAsync(chatId, "Введите корректное число.");
                        return;
                    }

                    decimal price = 0;

                    switch (userState[chatId])
                    {
                        case "roses_count": price = 8.6m; break;
                        case "tulips_count": price = 6.6m; break;
                        case "dahlias_count": price = 13m; break;
                    }

                    int total = (int)Math.Round(count * price);

                    userState.Remove(chatId);

                    await bot.SendTextMessageAsync(chatId,
                        $"💰 Цена заказа: {total} ₽\n\nДля оформления напишите адрес доставки 🌸");

                    return;
                }

                // ==============================
                // 📌 ДОСТАВКА
                // ==============================

                if (text == "🚚 доставка")
                {
                    var deliveryKeyboard = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { "ПМР", "Молдова", "Другие страны" }
                    })
                    { ResizeKeyboard = true };

                    await bot.SendTextMessageAsync(chatId, "Откуда вы?", replyMarkup: deliveryKeyboard);
                    return;
                }

                if (text == "пмр")
                {
                    await bot.SendTextMessageAsync(chatId,
                        "Каменка, Рыбница, Дубоссары, Григориополь, Тирасполь, Бендеры, Слободзея");
                    return;
                }

                if (text == "молдова")
                {
                    await bot.SendTextMessageAsync(chatId,
                        "Доставка: Nova Poshta или маршрутка");
                    return;
                }

                if (text == "другие страны")
                {
                    await bot.SendTextMessageAsync(chatId,
                        "Доставка только по ПМР и Молдове.");
                    return;
                }

                // ==============================
                // 📌 КОНТАКТЫ
                // ==============================

                if (text == "📞 контакты")
                {
                    await bot.SendTextMessageAsync(chatId,
                        "Instagram:\nhttps://www.instagram.com/bouquet_dubossary\n\nTelegram: @youscum1");
                    return;
                }
            }
        },
        async (bot, ex, ct) => Console.WriteLine("Ошибка: " + ex.Message)
    );

    Console.WriteLine("Бот запущен!");
}

app.Run();
