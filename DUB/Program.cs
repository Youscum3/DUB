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

                // Ввод количества
                if (userState.ContainsKey(chatId) && userState[chatId] == "await_quantity")
                {
                    if (int.TryParse(messageText, out int count))
                    {
                        userQuantity[chatId] = count;
                        userState[chatId] = "await_extras";

                        // Показать дополнительные товары
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

                // Команды и старт
                if (messageText.ToLower().StartsWith("/start"))
                    await ShowMainMenu(chatId);
                else if (messageText.ToLower().StartsWith("/price"))
                    await ShowPriceMenu(chatId);
                else if (messageText.ToLower().StartsWith("/order"))
                    await ShowOrderMenu(chatId);
                else if (messageText.ToLower().StartsWith("/contacts"))
                    await ShowContacts(chatId);
                else if (messageText.ToLower().StartsWith("/delivery"))
                    await ShowDeliveryMenu(chatId);
            }
            else if (update.CallbackQuery is { Data: { } data })
            {
                var chatId = update.CallbackQuery.Message.Chat.Id;
                var username = update.CallbackQuery.From.Username ?? update.CallbackQuery.From.FirstName;

                switch (data)
                {
                    case "start_price": await ShowPriceMenu(chatId); break;
                    case "start_order": await ShowOrderMenu(chatId); break;
                    case "start_contacts": await ShowContacts(chatId); break;
                    case "start_delivery": await ShowDeliveryMenu(chatId); break;

                    // Категории цветов
                    case "order_roses":
                    case "order_tulips":
                    case "order_dahlias":
                        userFlower[chatId] = data.Substring(6); // order_roses -> roses
                        userState[chatId] = "await_quantity";
                        await botClient.SendTextMessageAsync(chatId, "Введите количество:");
                        break;

                    // Дополнительно
                    case "extra_glitter":
                    case "extra_picture":
                    case "extra_toy":
                    case "extra_butterfly":
                    case "extra_ribbons":
                        if (!userExtras.ContainsKey(chatId))
                            userExtras[chatId] = new List<string>();
                        var extraName = data.Substring(6); // extra_glitter -> glitter
                        if (!userExtras[chatId].Contains(extraName))
                            userExtras[chatId].Add(extraName);
                        await botClient.SendTextMessageAsync(chatId, $"Вы добавили: {extraName}");
                        break;

                    case "extras_done":
                        userState[chatId] = "await_date";
                        await ShowCalendar(chatId, DateTime.Today.Year, DateTime.Today.Month);
                        break;

                    // Календарь
                    default:
                        if (data.StartsWith("date_"))
                        {
                            var dateSelected = DateTime.ParseExact(data.Substring(5), "yyyy-MM-dd", null);
                            userDate[chatId] = dateSelected;

                            // Расчет суммы
                            decimal pricePerUnit = userFlower[chatId] switch
                            {
                                "roses" => 8.6m,
                                "tulips" => 6.6m,
                                "dahlias" => 13m,
                                _ => 0m
                            };
                            decimal total = userQuantity[chatId] * pricePerUnit;
                            int rounded = (int)Math.Round(total, 0, MidpointRounding.AwayFromZero);

                            // Чек
                            string extrasText = userExtras.ContainsKey(chatId) ? string.Join(", ", userExtras[chatId]) : "Нет";
                            string flowerName = userFlower[chatId];
                            string receipt = $"✅ Чек заказа:\n\nПродавец: Youscam\nПокупатель: @{username}\nБукет: {flowerName}\nКоличество: {userQuantity[chatId]}\nДополнительно: {extrasText}\nСумма: {rounded}₽\nДата доставки: {dateSelected:dd.MM.yyyy}";

                            await botClient.SendTextMessageAsync(chatId, receipt);
                            await botClient.SendTextMessageAsync(chatId, "В ближайшее время с вами свяжется.");

                            // Очистка состояния для нового заказа
                            userState.Remove(chatId);
                            userQuantity.Remove(chatId);
                            userExtras.Remove(chatId);
                            userFlower.Remove(chatId);
                            userDate.Remove(chatId);
                        }
                        else if (data.StartsWith("month_"))
                        {
                            var parts = data.Substring(6).Split('-'); // month_2026-03
                            int year = int.Parse(parts[0]);
                            int month = int.Parse(parts[1]);
                            await ShowCalendar(chatId, year, month);
                        }
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
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithUrl("Instagram: bouquet_dubossary", "https://www.instagram.com/bouquet_dubossary") }
        });

        await botClient.SendTextMessageAsync(chatId, "Наши контакты:", replyMarkup: keyboard);
        await botClient.SendTextMessageAsync(chatId, "Telegram: @Youscam");
    }

    async Task ShowDeliveryMenu(long chatId)
    {
        await botClient.SendTextMessageAsync(chatId, "Раздел доставки пока без изменений.");
    }

    async Task ShowCalendar(long chatId, int year, int month)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var buttons = new List<InlineKeyboardButton[]>();

        for (int d = 1; d <= daysInMonth; d += 7)
        {
            var week = new List<InlineKeyboardButton>();
            for (int i = d; i < d + 7 && i <= daysInMonth; i++)
            {
                var date = new DateTime(year, month, i);
                week.Add(InlineKeyboardButton.WithCallbackData(i.ToString(), $"date_{date:yyyy-MM-dd}"));
            }
            buttons.Add(week.ToArray());
        }

        // Кнопка "Следующий месяц" если не декабрь
        if (month < 12)
        {
            var nextMonth = new DateTime(year, month, 1).AddMonths(1);
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➡️ Следующий месяц", $"month_{nextMonth:yyyy-MM}") });
        }

        var calendar = new InlineKeyboardMarkup(buttons.ToArray());
        await botClient.SendTextMessageAsync(chatId, $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month)} {year}", replyMarkup: calendar);
    }

    Console.WriteLine("Бот запущен!");
}

app.Run();
