using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");
app.MapControllers();

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

// Состояние пользователей
var userState = new Dictionary<long, string>(); // текущая категория цветка или заказ
var userQuantity = new Dictionary<long, int>(); // количество
var userFlower = new Dictionary<long, string>(); // выбранный цветок
var userExtras = new Dictionary<long, List<string>>(); // дополнительные элементы
var userDate = new Dictionary<long, DateTime>(); // дата заказа

if (!string.IsNullOrEmpty(token))
{
    var botClient = new TelegramBotClient(token);
    await botClient.DeleteWebhookAsync();

    async Task ShowMainMenu(long chatId)
    {
        var mainKeyboard = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("Цены", "price") },
            new [] { InlineKeyboardButton.WithCallbackData("Доставка", "delivery") },
            new [] { InlineKeyboardButton.WithCallbackData("Сделать заказ", "make_order") },
            new [] { InlineKeyboardButton.WithCallbackData("Контакты", "contacts") }
        });
        await botClient.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: mainKeyboard);
    }

    async Task ShowFlowerCategories(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("Розы", "category_roses") },
            new [] { InlineKeyboardButton.WithCallbackData("Тюльпаны", "category_tulips") },
            new [] { InlineKeyboardButton.WithCallbackData("Георгины", "category_dahlias") }
        });
        await botClient.SendTextMessageAsync(chatId, "Выберите категорию:", replyMarkup: keyboard);
    }

    async Task ShowExtrasMenu(long chatId)
    {
        var extrasKeyboard = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("Блёстки", "extra_glitter"),
                    InlineKeyboardButton.WithCallbackData("Картинка", "extra_picture") },
            new [] { InlineKeyboardButton.WithCallbackData("Игрушка", "extra_toy"),
                    InlineKeyboardButton.WithCallbackData("Бантики", "extra_ribbons") },
            new [] { InlineKeyboardButton.WithCallbackData("✅ Готово", "extras_done") },
            new [] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "order_cancel") }
        });
        await botClient.SendTextMessageAsync(chatId, "Выберите дополнительные элементы:", replyMarkup: extrasKeyboard);
    }

    async Task ShowRibbonsColors(long chatId)
    {
        var ribbonKeyboard = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("Красные", "ribbon_red"), InlineKeyboardButton.WithCallbackData("Розовые", "ribbon_pink") },
            new [] { InlineKeyboardButton.WithCallbackData("Бордовые", "ribbon_bordeaux"), InlineKeyboardButton.WithCallbackData("Жёлтые", "ribbon_yellow") },
            new [] { InlineKeyboardButton.WithCallbackData("Фиолетовые", "ribbon_purple") },
            new [] { InlineKeyboardButton.WithCallbackData("Назад", "extras_back") }
        });
        await botClient.SendTextMessageAsync(chatId, "Выберите цвет бантика:", replyMarkup: ribbonKeyboard);
    }

    async Task ShowPictures(long chatId)
    {
        var pictureKeyboard = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("Картинка 1", "picture_1"), InlineKeyboardButton.WithCallbackData("Картинка 2", "picture_2") },
            new [] { InlineKeyboardButton.WithCallbackData("Картинка 3", "picture_3"), InlineKeyboardButton.WithCallbackData("Назад", "extras_back") }
        });
        await botClient.SendTextMessageAsync(chatId, "Выберите картинку:", replyMarkup: pictureKeyboard);
    }

    async Task ShowCalendar(long chatId, DateTime month)
    {
        var keyboardRows = new List<InlineKeyboardButton[]>();
        int daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
        for (int day = 1; day <= daysInMonth; day += 7)
        {
            var row = new List<InlineKeyboardButton>();
            for (int i = day; i < day + 7 && i <= daysInMonth; i++)
            {
                var date = new DateTime(month.Year, month.Month, i);
                row.Add(InlineKeyboardButton.WithCallbackData(i.ToString(), $"date_{date:yyyy-MM-dd}"));
            }
            keyboardRows.Add(row.ToArray());
        }

        // Кнопка на следующий месяц
        var nextMonth = month.AddMonths(1);
        keyboardRows.Add(new[] { InlineKeyboardButton.WithCallbackData("Следующий месяц ➡️", $"month_{nextMonth:yyyy-MM}") });

        var keyboard = new InlineKeyboardMarkup(keyboardRows);
        await botClient.SendTextMessageAsync(chatId, "Выберите дату:", replyMarkup: keyboard);
    }

    botClient.StartReceiving(
        async (bot, update, ct) =>
        {
            if (update.Message is { Text: { } messageText } message)
            {
                var chatId = message.Chat.Id;
                messageText = messageText.Trim().ToLower();

                // Обработка количества цветов
                if (userState.ContainsKey(chatId) && userState[chatId].StartsWith("flower_"))
                {
                    if (int.TryParse(messageText, out int count))
                    {
                        string flowerName = userState[chatId].Substring(7);
                        decimal pricePerUnit = flowerName switch
                        {
                            "roses" => 8.6m,
                            "tulips" => 6.6m,
                            "dahlias" => 13m,
                            _ => 0
                        };
                        decimal total = count * pricePerUnit;
                        int roundedTotal = (int)Math.Round(total, 0, MidpointRounding.AwayFromZero);

                        if (!userQuantity.ContainsKey(chatId)) userQuantity[chatId] = 0;
                        userQuantity[chatId] = count;
                        userFlower[chatId] = flowerName;

                        await botClient.SendTextMessageAsync(chatId, $"Цена: {roundedTotal}₽");

                        // Сразу можно выбрать еще
                        await ShowFlowerCategories(chatId);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "Пожалуйста, введите число.");
                    }
                    return;
                }

                // Команды
                if (messageText == "/start" || messageText == "старт")
                {
                    await ShowMainMenu(chatId);
                }
                else if (messageText == "/price")
                {
                    await ShowFlowerCategories(chatId);
                }
                else if (messageText == "/makeorder")
                {
                    await ShowFlowerCategories(chatId);
                }
                else if (messageText == "/contacts" || messageText == "контакты")
                {
                    var contactsKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithUrl("Instagram: bouquet_dubossary", "https://www.instagram.com/bouquet_dubossary?igsh=ZDhzeHpzZmNiMWE5&utm_source=qr") }
                    });
                    string telegramText = "Telegram: @youscam";
                    await botClient.SendTextMessageAsync(chatId, "Наши контакты:", replyMarkup: contactsKeyboard);
                    await botClient.SendTextMessageAsync(chatId, telegramText);
                }
            }
            else if (update.CallbackQuery is { Data: { } callbackData })
            {
                var chatId = update.CallbackQuery.Message.Chat.Id;
                var data = callbackData;

                switch (data)
                {
                    // Цветы для заказа
                    case "category_roses":
                    case "category_tulips":
                    case "category_dahlias":
                        userState[chatId] = "flower_" + data.Substring(9); // flower_roses
                        await botClient.SendTextMessageAsync(chatId, "Введите, сколько штук вам нужно.");
                        break;

                    // Дополнительно
                    case "make_order":
                        await ShowFlowerCategories(chatId);
                        break;

                    case "extra_glitter":
                        if (!userExtras.ContainsKey(chatId)) userExtras[chatId] = new List<string>();
                        userExtras[chatId].Add("Блёстки");
                        await botClient.SendTextMessageAsync(chatId, "Вы выбрали: Блёстки");
                        await ShowExtrasMenu(chatId);
                        break;
                    case "extra_toy":
                        if (!userExtras.ContainsKey(chatId)) userExtras[chatId] = new List<string>();
                        userExtras[chatId].Add("Игрушка");
                        await botClient.SendTextMessageAsync(chatId, "Вы выбрали: Игрушка");
                        await ShowExtrasMenu(chatId);
                        break;
                    case "extra_picture":
                        await ShowPictures(chatId);
                        break;
                    case "picture_1":
                    case "picture_2":
                    case "picture_3":
                        if (!userExtras.ContainsKey(chatId)) userExtras[chatId] = new List<string>();
                        userExtras[chatId].Add(data.Replace("picture_", "Картинка "));
                        await botClient.SendTextMessageAsync(chatId, $"Вы выбрали: {data.Replace("picture_", "Картинка ")}");
                        await ShowExtrasMenu(chatId);
                        break;
                    case "extra_ribbons":
                        await ShowRibbonsColors(chatId);
                        break;
                    case "ribbon_red":
                    case "ribbon_pink":
                    case "ribbon_bordeaux":
                    case "ribbon_yellow":
                    case "ribbon_purple":
                        if (!userExtras.ContainsKey(chatId)) userExtras[chatId] = new List<string>();
                        userExtras[chatId].Add($"Бантик ({data.Substring(7)})");
                        await botClient.SendTextMessageAsync(chatId, $"Вы выбрали: {data.Substring(7)} бантик");
                        await ShowExtrasMenu(chatId);
                        break;
                    case "extras_back":
                        await ShowExtrasMenu(chatId);
                        break;
                    case "extras_done":
                        await botClient.SendTextMessageAsync(chatId, "Теперь выберите дату заказа:");
                        await ShowCalendar(chatId, DateTime.Now);
                        break;
                    case "order_cancel":
                        userState.Remove(chatId);
                        userQuantity.Remove(chatId);
                        userFlower.Remove(chatId);
                        userExtras.Remove(chatId);
                        userDate.Remove(chatId);
                        await botClient.SendTextMessageAsync(chatId, "Заказ отменен.");
                        await ShowMainMenu(chatId);
                        break;

                    // Календарь
                    default:
                        if (data.StartsWith("month_"))
                        {
                            var month = DateTime.Parse(data.Substring(6) + "-01");
                            await ShowCalendar(chatId, month);
                        }
                        else if (data.StartsWith("date_"))
                        {
                            var date = DateTime.Parse(data.Substring(5));
                            userDate[chatId] = date;

                            string flower = userFlower.ContainsKey(chatId) ? userFlower[chatId] : "не выбрано";
                            int quantity = userQuantity.ContainsKey(chatId) ? userQuantity[chatId] : 0;
                            string extras = userExtras.ContainsKey(chatId) ? string.Join(", ", userExtras[chatId]) : "нет";

                            string receipt = $"✅ Чек заказа:\n" +
                                             $"Продавец: Youscam\n" +
                                             $"Покупатель: {update.CallbackQuery.From.Username}\n" +
                                             $"Букет: {flower}, {quantity} шт\n" +
                                             $"Дополнительно: {extras}\n" +
                                             $"Дата: {date:dd.MM.yyyy}";

                            await botClient.SendTextMessageAsync(chatId, receipt);
                            await botClient.SendTextMessageAsync(chatId, "Ваш заказ принят! В ближайшее время с вами свяжутся.");

                            // Сброс состояния
                            userState.Remove(chatId);
                            userQuantity.Remove(chatId);
                            userFlower.Remove(chatId);
                            userExtras.Remove(chatId);
                            userDate.Remove(chatId);
                        }
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
