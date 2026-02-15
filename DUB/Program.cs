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
                    var menu = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { "💰 Прайс", "🚚 Доставка" },
                        new KeyboardButton[] { "🛒 Сделать заказ", "📞 Контакты" }
                    })
                    {
                        ResizeKeyboard = true
                    };

                    await bot.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: menu);
                    return;
                }

                // ==============================
                // 📌 ПРАЙС
                // ==============================

                if (text == "💰 прайс" || text == "/price")
                {
                    var priceKeyboard = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { "Розы", "Тюльпаны", "Георгины" }
                    })
                    {
                        ResizeKeyboard = true
                    };

                    await bot.SendTextMessageAsync(chatId, "Выберите цветы:", replyMarkup: priceKeyboard);
                    return;
                }

                if (text == "розы")
                {
                    userState[chatId] = "roses";
                    await bot.SendTextMessageAsync(chatId, "Сколько роз?");
                    return;
                }

                if (text == "тюльпаны")
                {
                    userState[chatId] = "tulips";
                    await bot.SendTextMessageAsync(chatId, "Сколько тюльпанов?");
                    return;
                }

                if (text == "георгины")
                {
                    userState[chatId] = "dahlias";
                    await bot.SendTextMessageAsync(chatId, "Сколько георгинов?");
                    return;
                }

                // ввод количества
                if (userState.ContainsKey(chatId))
                {
                    if (int.TryParse(messageText, out int count))
                    {
                        decimal price = 0;

                        switch (userState[chatId])
                        {
                            case "roses": price = 8.6m; break;
                            case "tulips": price = 6.6m; break;
                            case "dahlias": price = 13m; break;
                        }

                        int total = (int)Math.Round(count * price);
                        await bot.SendTextMessageAsync(chatId, $"Цена: {total} ₽");
                    }
                    else
                    {
                        await bot.SendTextMessageAsync(chatId, "Введите число.");
                    }
                    return;
                }

                // ==============================
                // 📌 ДОСТАВКА
                // ==============================

                if (text == "🚚 доставка" || text == "/delivery")
                {
                    var deliveryKeyboard = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { "ПМР", "Молдова", "Другие страны" }
                    })
                    {
                        ResizeKeyboard = true
                    };

                    await bot.SendTextMessageAsync(chatId, "Откуда вы?", replyMarkup: deliveryKeyboard);
                    return;
                }

                if (text == "пмр")
                {
                    await bot.SendTextMessageAsync(chatId,
                        "Города ПМР:\nКаменка, Рыбница, Дубоссары, Григориополь, Тирасполь, Бендеры, Слободзея");
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
                        "К сожалению, доставка только по ПМР и Молдове.");
                    return;
                }

                // ==============================
                // 📌 КОНТАКТЫ
                // ==============================

                if (text == "📞 контакты" || text == "/contacts")
                {
                    await bot.SendTextMessageAsync(chatId,
                        "Наши контакты:\n\n" +
                        "Instagram:\nhttps://www.instagram.com/bouquet_dubossary\n\n" +
                        "Telegram: @youscum1");
                    return;
                }

                // ==============================
                // 📌 СДЕЛАТЬ ЗАКАЗ
                // ==============================

                if (text == "🛒 сделать заказ" || text == "/order")
                {
                    await bot.SendTextMessageAsync(chatId,
                        "Напишите, какой букет хотите заказать 🌸");
                    return;
                }
            }
        },
        async (bot, ex, ct) => Console.WriteLine("Ошибка: " + ex.Message)
    );

    Console.WriteLine("Бот запущен!");
}

app.Run();
