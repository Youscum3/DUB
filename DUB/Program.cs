using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");
app.MapControllers();

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

// Словарь состояния
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

                // 🔥 ЕСЛИ пользователь уже в режиме расчёта
                if (userState.ContainsKey(chatId))
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
                            $"Цена: {rounded}₽\n"
                        );

                        // ❌ НЕ удаляем состояние!
                    }
                    else
                    {
                        await bot.SendTextMessageAsync(chatId, "Введите число.");
                    }

                    return;
                }

                // /start — главное меню
                if (messageText.ToLower().StartsWith("/start"))
                {
                    var mainKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("Цены", "start_price") },
                        new [] { InlineKeyboardButton.WithCallbackData("Доставка", "start_delivery") },
                        new [] { InlineKeyboardButton.WithCallbackData("Контакты", "start_contacts") },
                        new [] { InlineKeyboardButton.WithCallbackData("Сделать заказ", "start_order") }
                    });

                    await bot.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: mainKeyboard);
                }

                // Команды
                else if (messageText.ToLower().StartsWith("/price"))
                    await ShowPriceMenu(chatId);

                else if (messageText.ToLower().StartsWith("/delivery"))
                    await ShowDeliveryMenu(chatId);

                else if (messageText.ToLower().StartsWith("/contacts"))
                    await ShowContacts(chatId);

                else if (messageText.ToLower().StartsWith("/order"))
                    await ShowOrderMenu(chatId);
            }

            else if (update.CallbackQuery is { Data: { } data })
            {
                var chatId = update.CallbackQuery.Message.Chat.Id;

                switch (data)
                {
                    // Главные кнопки
                    case "start_price": await ShowPriceMenu(chatId); break;
                    case "start_delivery": await ShowDeliveryMenu(chatId); break;
                    case "start_contacts": await ShowContacts(chatId); break;
                    case "start_order": await ShowOrderMenu(chatId); break;

                    // Категории цветов — включаем режим расчёта
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

                    // Заказ — тоже включает режим расчёта
                    case "order_roses":
                        userState[chatId] = "roses";
                        await botClient.SendTextMessageAsync(chatId, "Заказ роз. Введите количество:");
                        break;

                    case "order_tulips":
                        userState[chatId] = "tulips";
                        await botClient.SendTextMessageAsync(chatId, "Заказ тюльпанов. Введите количество:");
                        break;

                    case "order_dahlias":
                        userState[chatId] = "dahlias";
                        await botClient.SendTextMessageAsync(chatId, "Заказ георгин. Введите количество:");
                        break;
                }

                await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
            }
        },
        async (bot, ex, ct) => Console.WriteLine(ex.Message)
    );

    // ===== МЕНЮ =====

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
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithUrl("Instagram: bouquet_dubossary", "https://www.instagram.com/bouquet_dubossary") }
        });

        await botClient.SendTextMessageAsync(chatId, "Наши контакты:", replyMarkup: keyboard);
        await botClient.SendTextMessageAsync(chatId, "Telegram: @youscum1");
    }

    async Task ShowDeliveryMenu(long chatId)
    {
        await botClient.SendTextMessageAsync(chatId, "Раздел доставки пока без изменений.");
    }

    Console.WriteLine("Бот запущен!");
}

app.Run();
