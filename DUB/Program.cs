using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
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

TelegramBotClient botClient = null;
if (!string.IsNullOrEmpty(token))
{
    botClient = new TelegramBotClient(token);
    await botClient.DeleteWebhookAsync();
}

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
                if (int.TryParse(messageText, out int countParsed))
                {
                    userQuantity[chatId] = countParsed;
                    userState[chatId] = "await_extras";

                    await SendExtrasMenu(chatId, countParsed, userFlower.ContainsKey(chatId) ? userFlower[chatId] : "");
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Пожалуйста, введите число.");
                }
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
            {
                await ShowDeliveryMenu(chatId);
            }
        }
        else if (update.CallbackQuery is { Data: { } data })
        {
            var chatId = update.CallbackQuery.Message.Chat.Id;

            switch (data)
            {
                // Подтверждение
                case "confirm_yes":
                    // код подтверждения
                    break;
                case "confirm_no":
                    // код отмены
                    break;

                // Пример работы с категориями цены
                case "price_category_roses_15":
                case "price_category_tulips_15":
                case "price_category_dahlias_15":
                case "price_category_roses_31":
                case "price_category_tulips_31":
                case "price_category_dahlias_31":
                case "price_category_roses_51":
                case "price_category_tulips_51":
                case "price_category_dahlias_51":
                case "price_category_roses_101":
                case "price_category_tulips_101":
                case "price_category_dahlias_101":
                    {
                        int count = int.Parse(data.Split('_')[3]);

                        decimal pricePerUnit = data.Contains("roses") ? 8.6m :
                                               data.Contains("tulips") ? 6.6m :
                                               data.Contains("dahlias") ? 13m : 0m;

                        int total = (int)Math.Round(count * pricePerUnit, 0, MidpointRounding.AwayFromZero);

                        await botClient.SendTextMessageAsync(chatId, $"💰 Цена за {count} шт.: {total}₽",
                            replyMarkup: GetBackToMenuKeyboard());
                        break;
                    }

                case "price_category_roses_other":
                case "price_category_tulips_other":
                case "price_category_dahlias_other":
                    {
                        userState[chatId] = "await_custom_quantity";
                        userFlower[chatId] = data.Contains("roses") ? "roses" :
                                             data.Contains("tulips") ? "tulips" : "dahlias";

                        await botClient.SendTextMessageAsync(chatId, "Введите количество:");
                        break;
                    }

                // Меню
                case "start_menu": await ShowMainMenu(chatId); break;
                case "start_price":
                    userState.Remove(chatId); userQuantity.Remove(chatId); userExtras.Remove(chatId); userFlower.Remove(chatId); userDate.Remove(chatId);
                    await ShowPriceMenu(chatId);
                    break;
                case "start_order":
                    userState.Remove(chatId); userQuantity.Remove(chatId); userExtras.Remove(chatId); userFlower.Remove(chatId); userDate.Remove(chatId);
                    await ShowOrderMenu(chatId);
                    break;
                case "start_contacts":
                    userState.Remove(chatId); userQuantity.Remove(chatId); userExtras.Remove(chatId); userFlower.Remove(chatId); userDate.Remove(chatId);
                    await ShowContacts(chatId);
                    break;
                case "start_delivery":
                    userState.Remove(chatId); userQuantity.Remove(chatId); userExtras.Remove(chatId); userFlower.Remove(chatId); userDate.Remove(chatId);
                    await ShowDeliveryMenu(chatId);
                    break;

                // Категории цветов и количество
                case "order_roses":
                    userFlower[chatId] = "roses";
                    userState[chatId] = "await_quantity";
                    await ShowQuantityKeyboard(chatId, "роз", "roses");
                    break;
                case "order_tulips":
                    userFlower[chatId] = "tulips";
                    userState[chatId] = "await_quantity";
                    await ShowQuantityKeyboard(chatId, "тюльпанов", "tulips");
                    break;
                case "order_dahlias":
                    userFlower[chatId] = "dahlias";
                    userState[chatId] = "await_quantity";
                    await ShowQuantityKeyboard(chatId, "георгин", "dahlias");
                    break;

                // Выбор дополнительных элементов
                case "extra_glitter":
                case "extra_picture":
                case "extra_toy":
                case "extra_butterfly":
                case "extra_ribbons":
                    if (!userExtras.ContainsKey(chatId))
                        userExtras[chatId] = new List<string>();
                    var extraName = data.Substring(6);
                    if (!userExtras[chatId].Contains(extraName))
                        userExtras[chatId].Add(extraName);
                    await botClient.SendTextMessageAsync(chatId, $"Вы добавили: {extraName}");
                    break;

                case "extras_done":
                    userState[chatId] = "await_date";
                    await ShowCalendar(chatId, DateTime.Today.Year, DateTime.Today.Month);
                    break;

                // Подтверждение
               
                default:
                    if (data.StartsWith("date_"))
                    {
                        var username = update.CallbackQuery.From.Username ?? update.CallbackQuery.From.FirstName;
                        var dateSelected = DateTime.ParseExact(data.Substring(5), "yyyy-MM-dd", null);
                        userDate[chatId] = dateSelected;

                        decimal pricePerUnit = userFlower[chatId] switch
                        {
                            "roses" => 8.6m,
                            "tulips" => 6.6m,
                            "dahlias" => 13m,
                            _ => 0m
                        };
                        decimal total = userQuantity[chatId] * pricePerUnit;
                        int rounded = (int)Math.Round(total, 0, MidpointRounding.AwayFromZero);

                        string extrasText = userExtras.ContainsKey(chatId) ? string.Join(", ", userExtras[chatId]) : "Нет";
                        string flowerName = userFlower[chatId];
                        string receipt = $"✅ Чек заказа:\n\nПродавец: Youscam\nПокупатель: @{username}\nБукет: {flowerName}\nКоличество: {userQuantity[chatId]}\nДополнительно: {extrasText}\nСумма: {rounded}₽\nДата доставки: {dateSelected:dd.MM.yyyy}";

                        await botClient.SendTextMessageAsync(chatId, receipt);

                        var confirmKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithCallbackData("✅ Да", "confirm_yes"),
                                    InlineKeyboardButton.WithCallbackData("❌ Нет", "confirm_no") }
                        });

                        await botClient.SendTextMessageAsync(chatId, "Подтвердить заказ?", replyMarkup: confirmKeyboard);
                    }
                    break;
            }

            await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
        }
    },
    async (bot, ex, ct) => Console.WriteLine(ex.Message)
);

// ===== ФУНКЦИИ =====
async Task ShowMainMenu(long chatId)
{
    userState.Remove(chatId); userQuantity.Remove(chatId); userExtras.Remove(chatId); userFlower.Remove(chatId); userDate.Remove(chatId);

    var mainKeyboard = new InlineKeyboardMarkup(new[]
    {
new [] { InlineKeyboardButton.WithCallbackData("🌹 Розы 🌹", "order_roses") },
new [] { InlineKeyboardButton.WithCallbackData("🌷 Тюльпаны 🌷", "order_tulips") },
new [] { InlineKeyboardButton.WithCallbackData("🌺 Георгины 🌺", "order_dahlias") }
    });
    await botClient.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: mainKeyboard);
}

async Task ShowPriceMenu(long chatId)
{
    userState.Remove(chatId); userQuantity.Remove(chatId); userExtras.Remove(chatId); userFlower.Remove(chatId); userDate.Remove(chatId);

    var keyboard = new InlineKeyboardMarkup(new[]
    {
new [] { InlineKeyboardButton.WithCallbackData("🌹 Розы 🌹", "order_roses") },
new [] { InlineKeyboardButton.WithCallbackData("🌷 Тюльпаны 🌷", "order_tulips") },
new [] { InlineKeyboardButton.WithCallbackData("🌺 Георгины 🌺", "order_dahlias") }
    });
    await botClient.SendTextMessageAsync(chatId, "Выберите категорию:", replyMarkup: keyboard);
}

async Task ShowOrderMenu(long chatId)
{
    var keyboard = new InlineKeyboardMarkup(new[]
    {
        new [] { InlineKeyboardButton.WithCallbackData("🌹 Розы 🌹", "category_roses") },
        new [] { InlineKeyboardButton.WithCallbackData("🌷 Тюльпаны 🌷", "category_tulips") },
        new [] { InlineKeyboardButton.WithCallbackData("🌺 Георгины 🌺", "category_dahlias") }
    });
    await botClient.SendTextMessageAsync(chatId, "Выберите букет:", replyMarkup: keyboard);
}

async Task ShowContacts(long chatId)
{
    await botClient.SendTextMessageAsync(chatId,
@"📞 Наши контакты

💬 Telegram мастера: @Vethbu  
📢 Telegram канал: https://t.me/+6a3DugGFBHwzMmJi  

🎵 TikTok: https://www.tiktok.com/@bouquet_dubossary  
📷 Instagram: https://www.instagram.com/bouquet_dubossary",
        replyMarkup: GetBackToMenuKeyboard());
}

async Task ShowDeliveryMenu(long chatId)
{
    var regionKeyboard = new InlineKeyboardMarkup(new[]
    {
        new [] { InlineKeyboardButton.WithCallbackData("ПМР", "delivery_pmr") },
        new [] { InlineKeyboardButton.WithCallbackData("Молдова", "delivery_moldova") },
        new [] { InlineKeyboardButton.WithCallbackData("Другие страны", "delivery_other") }
    });

    await botClient.SendTextMessageAsync(chatId, "Откуда вы?", replyMarkup: regionKeyboard);
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

    if (month < 12)
    {
        var nextMonth = new DateTime(year, month, 1).AddMonths(1);
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➡️ Следующий месяц", $"month_{nextMonth:yyyy-MM}") });
    }

    var calendar = new InlineKeyboardMarkup(buttons.ToArray());
    await botClient.SendTextMessageAsync(chatId, $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month)} {year}", replyMarkup: calendar);
}

InlineKeyboardMarkup GetBackToMenuKeyboard()
{
    return new InlineKeyboardMarkup(new[]
    {
        new [] { InlineKeyboardButton.WithCallbackData("🏠 Меню", "start_menu") }
    });
}

// ===== ФУНКЦИЯ ВЫБОРА КОЛИЧЕСТВА =====
async Task ShowQuantityKeyboard(long chatId, string flowerDisplayName, string flowerKey)
{
    var quantityKeyboard = new InlineKeyboardMarkup(new[]
    {
        new [] { InlineKeyboardButton.WithCallbackData("15", $"qty_{flowerKey}_15"),
                InlineKeyboardButton.WithCallbackData("31", $"qty_{flowerKey}_31"),
                InlineKeyboardButton.WithCallbackData("51", $"qty_{flowerKey}_51") },
        new [] { InlineKeyboardButton.WithCallbackData("101", $"qty_{flowerKey}_101"),
                InlineKeyboardButton.WithCallbackData("Другое количество", $"qty_{flowerKey}_custom") }
    });

    await botClient.SendTextMessageAsync(chatId, $"Выберите количество {flowerDisplayName}:", replyMarkup: quantityKeyboard);
}

// ===== ФУНКЦИЯ ДОПОЛНИТЕЛЬНЫХ ЭЛЕМЕНТОВ =====
async Task SendExtrasMenu(long chatId, int quantity, string flowerName)
{
    var extrasKeyboard = new InlineKeyboardMarkup(new[]
    {
        new [] { InlineKeyboardButton.WithCallbackData("Блёстки", "extra_glitter"), InlineKeyboardButton.WithCallbackData("Картинка", "extra_picture") },
        new [] { InlineKeyboardButton.WithCallbackData("Игрушка", "extra_toy"), InlineKeyboardButton.WithCallbackData("Бабочки", "extra_butterfly") },
        new [] { InlineKeyboardButton.WithCallbackData("Бантики", "extra_ribbons") },
        new [] { InlineKeyboardButton.WithCallbackData("✅ Готово", "extras_done") }
    });

    await botClient.SendTextMessageAsync(
        chatId,
        $"Вы выбрали: {quantity} шт. {flowerName}\n\n⚠️ ЦЕНА ДОПОЛНИТЕЛЬНЫХ УСЛУГ ОБГОВАРИВАЕТСЯ С МАСТЕРОМ\n\nВыберите дополнительные элементы:",
        replyMarkup: extrasKeyboard
    );
}

Console.WriteLine("Бот запущен!");
app.Run();
