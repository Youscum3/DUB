using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");
app.MapControllers();

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

// Словари для состояния
var userState = new Dictionary<long, string>();
var orderData = new Dictionary<long, OrderInfo>();

if (!string.IsNullOrEmpty(token))
{
    var botClient = new TelegramBotClient(token);
    await botClient.DeleteWebhookAsync();

    // ------------------ Функции для календаря ------------------
    void SendCalendar(long chatId, int year, int month)
    {
        var firstDay = new DateTime(year, month, 1);
        int daysInMonth = DateTime.DaysInMonth(year, month);

        var buttons = new List<List<InlineKeyboardButton>>();

        // Заголовок месяца
        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData($"{firstDay:MMMM yyyy}", "calendar_header")
        });

        // Кнопки с днями
        for (int day = 1; day <= daysInMonth; day += 7)
        {
            var weekButtons = new List<InlineKeyboardButton>();
            for (int i = day; i <= Math.Min(day + 6, daysInMonth); i++)
            {
                weekButtons.Add(InlineKeyboardButton.WithCallbackData(i.ToString(), $"calendar_{year}_{month}_{i}"));
            }
            buttons.Add(weekButtons);
        }

        // Кнопки листания месяцев
        var prevMonth = firstDay.AddMonths(-1);
        var nextMonth = firstDay.AddMonths(1);

        var navButtons = new List<InlineKeyboardButton>();
        if (prevMonth >= DateTime.Today) // не листаем в прошлое
            navButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️", $"calendar_nav_{prevMonth.Year}_{prevMonth.Month}"));
        navButtons.Add(InlineKeyboardButton.WithCallbackData("➡️", $"calendar_nav_{nextMonth.Year}_{nextMonth.Month}"));

        buttons.Add(navButtons);

        var keyboard = new InlineKeyboardMarkup(buttons);
        botClient.SendTextMessageAsync(chatId, "Выберите дату доставки:", replyMarkup: keyboard);
    }

    botClient.StartReceiving(
        async (bot, update, ct) =>
        {
            if (update.Message is { Text: { } messageText } message)
            {
                var chatId = message.Chat.Id;

                // Если пользователь вводит количество цветов для прайса
                if (userState.ContainsKey(chatId) && userState[chatId] != "order_date_text")
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

                // Если пользователь вводит количество цветов для заказа
                if (userState.ContainsKey(chatId) && userState[chatId] == "order_count")
                {
                    if (int.TryParse(messageText, out int count))
                    {
                        orderData[chatId].Count = count;
                        userState.Remove(chatId);
                        // После количества идем на дополнительные элементы
                        var extrasKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithCallbackData("Ленточки", "extra_ribbons") },
                            new [] { InlineKeyboardButton.WithCallbackData("Бантики", "extra_bows") },
                            new [] { InlineKeyboardButton.WithCallbackData("Игрушка", "extra_toy") },
                            new [] { InlineKeyboardButton.WithCallbackData("Бабочки", "extra_butterfly") },
                            new [] { InlineKeyboardButton.WithCallbackData("Карточка", "extra_card") },
                            new [] { InlineKeyboardButton.WithCallbackData("Продолжить", "order_date") }
                        });
                        await bot.SendTextMessageAsync(chatId, "Выберите дополнительные элементы:", replyMarkup: extrasKeyboard);
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
                        new [] { InlineKeyboardButton.WithCallbackData("Прайс", "/price") },
                        new [] { InlineKeyboardButton.WithCallbackData("Delivery", "delivery") },
                        new [] { InlineKeyboardButton.WithCallbackData("Контакты", "contacts") },
                        new [] { InlineKeyboardButton.WithCallbackData("Сделать заказ", "order_start") } // новая кнопка
                    });
                    await bot.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: mainKeyboard);
                }
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
                else if (userState.ContainsKey(chatId) && userState[chatId] == "order_date_text")
                {
                    // пользователь пишет дату текстом
                    orderData[chatId].Date = messageText;
                    userState.Remove(chatId);
                    // показываем чек
                    var order = orderData[chatId];
                    string extrasText = order.Extras.Count > 0 ? string.Join(", ", order.Extras) : "Нет";
                    int price = 0;
                    switch (order.Flower)
                    {
                        case "roses": price = 9 * order.Count; break;
                        case "tulips": price = 7 * order.Count; break;
                        case "dahlias": price = 13 * order.Count; break;
                    }

                    string check = $"Продавец: @bouquet_dubossary\n" +
                                   $"Покупатель: @{message.From.Username}\n" +
                                   $"Цветы: {order.Flower}\n" +
                                   $"Количество: {order.Count}\n" +
                                   $"Дополнительно: {extrasText}\n" +
                                   $"Сумма: {price}₽\n" +
                                   $"Дата доставки: {order.Date}";

                    var confirmKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("Да, отменить заказ", "order_cancel_yes") },
                        new [] { InlineKeyboardButton.WithCallbackData("Нет", "order_cancel_no") }
                    });

                    await bot.SendTextMessageAsync(chatId, check, replyMarkup: confirmKeyboard);
                }
            }
            else if (update.CallbackQuery is { Data: { } callbackData })
            {
                var chatId = update.CallbackQuery.Message.Chat.Id;

                switch (callbackData)
                {
                    // ------------------ Прайс ------------------
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

                    // ------------------ Доставка ------------------
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

                    // ------------------ Города ПМР ------------------
                    case "pmr_kamenka":
                    case "pmr_rybnica":
                    case "pmr_grigoriopol":
                    case "pmr_bendery":
                    case "pmr_slobodeya":
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

                    // ------------------ Контакты ------------------
                    case "contacts":
                        var contactsKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithUrl("Instagram: bouquet_dubossary", "https://www.instagram.com/bouquet_dubossary?igsh=ZDhzeHpzZmNiMWE5&utm_source=qr") }
                        });
                        string telegramText = "Telegram: @youscum1";
                        await botClient.SendTextMessageAsync(chatId, "Наши контакты:", replyMarkup: contactsKeyboard);
                        await botClient.SendTextMessageAsync(chatId, telegramText);
                        break;

                    // ------------------ Сделать заказ ------------------
                    case "order_start":
                        var orderKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithCallbackData("Розы", "order_roses") },
                            new [] { InlineKeyboardButton.WithCallbackData("Тюльпаны", "order_tulips") },
                            new [] { InlineKeyboardButton.WithCallbackData("Георгины", "order_dahlias") }
                        });
                        await botClient.SendTextMessageAsync(chatId, "Выберите цветы для заказа:", replyMarkup: orderKeyboard);
                        orderData[chatId] = new OrderInfo();
                        break;

                    case "order_roses":
                    case "order_tulips":
                    case "order_dahlias":
                        orderData[chatId].Flower = callbackData.Replace("order_", "");
                        await botClient.SendTextMessageAsync(chatId, "Введите количество штук:");
                        userState[chatId] = "order_count";
                        break;

                    // ------------------ Дополнительные элементы ------------------
                    case "extra_ribbons":
                        orderData[chatId].Extras.Add("Ленточки");
                        goto case "extras";
                    case "extra_bows":
                        orderData[chatId].Extras.Add("Бантики");
                        goto case "extras";
                    case "extra_toy":
                        orderData[chatId].Extras.Add("Игрушка");
                        goto case "extras";
                    case "extra_butterfly":
                        orderData[chatId].Extras.Add("Бабочки");
                        goto case "extras";
                    case "extra_card":
                        orderData[chatId].Extras.Add("Карточка");
                        goto case "extras";
                    case "extras":
                        var extrasKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithCallbackData("Ленточки", "extra_ribbons") },
                            new [] { InlineKeyboardButton.WithCallbackData("Бантики", "extra_bows") },
                            new [] { InlineKeyboardButton.WithCallbackData("Игрушка", "extra_toy") },
                            new [] { InlineKeyboardButton.WithCallbackData("Бабочки", "extra_butterfly") },
                            new [] { InlineKeyboardButton.WithCallbackData("Карточка", "extra_card") },
                            new [] { InlineKeyboardButton.WithCallbackData("Продолжить", "order_date") }
                        });
                        await botClient.SendTextMessageAsync(chatId, "Выберите дополнительные элементы:", replyMarkup: extrasKeyboard);
                        break;

                    // ------------------ Календарь ------------------
                    case "order_date":
                        var today = DateTime.Today;
                        SendCalendar(chatId, today.Year, today.Month);
                        break;

                    default:
                        // Навигация по месяцам
                        if (callbackData.StartsWith("calendar_nav_"))
                        {
                            var parts = callbackData.Split('_');
                            int year = int.Parse(parts[2]);
                            int month = int.Parse(parts[3]);
                            SendCalendar(chatId, year, month);
                        }
                        // Выбор конкретного дня
                        else if (callbackData.StartsWith("calendar_") && !callbackData.StartsWith("calendar_nav"))
                        {
                            var parts = callbackData.Split('_');
                            int year = int.Parse(parts[1]);
                            int month = int.Parse(parts[2]);
                            int day = int.Parse(parts[3]);

                            orderData[chatId].Date = new DateTime(year, month, day).ToString("dd.MM.yyyy");

                            // показываем чек после даты
                            var order = orderData[chatId];
                            string extrasText = order.Extras.Count > 0 ? string.Join(", ", order.Extras) : "Нет";
                            int price = 0;
                            switch (order.Flower)
                            {
                                case "roses": price = 9 * order.Count; break;
                                case "tulips": price = 7 * order.Count; break;
                                case "dahlias": price = 13 * order.Count; break;
                            }

                            string check = $"Продавец: @bouquet_dubossary\n" +
                                           $"Покупатель: @{update.CallbackQuery.From.Username}\n" +
                                           $"Цветы: {order.Flower}\n" +
                                           $"Количество: {order.Count}\n" +
                                           $"Дополнительно: {extrasText}\n" +
                                           $"Сумма: {price}₽\n" +
                                           $"Дата доставки: {order.Date}";

                            var confirmKeyboard = new InlineKeyboardMarkup(new[]
                            {
                                new [] { InlineKeyboardButton.WithCallbackData("Да, отменить заказ", "order_cancel_yes") },
                                new [] { InlineKeyboardButton.WithCallbackData("Нет", "order_cancel_no") }
                            });

                            await botClient.SendTextMessageAsync(chatId, check, replyMarkup: confirmKeyboard);
                        }
                        // Доставка по выбранному способу
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

                // ------------------ Отмена заказа ------------------
                if (callbackData == "order_cancel_no")
                {
                    await botClient.SendTextMessageAsync(chatId, "Спасибо! С вами свяжутся в ближайшее время.");
                    orderData.Remove(chatId);
                }
                if (callbackData == "order_cancel_yes")
                {
                    await botClient.SendTextMessageAsync(chatId, "Заказ отменён.");
                    orderData.Remove(chatId);
                }

                await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
            }
        },
        async (bot, ex, ct) => Console.WriteLine("Ошибка: " + ex.Message)
    );

    Console.WriteLine("Бот успешно запущен!");
}

app.Run();

// ------------------ Класс для заказа ------------------
class OrderInfo
{
    public string Flower { get; set; }
    public int Count { get; set; }
    public List<string> Extras { get; set; } = new();
    public string Date { get; set; }
}
