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
long adminChatId = 1453388711; // сюда поставь свой Telegram ID

if (!string.IsNullOrEmpty(token))
{
    botClient = new TelegramBotClient(token);
    await botClient.DeleteWebhookAsync();

    botClient.StartReceiving(
        async (bot, update, ct) =>
        {
            if (update.Message is { Text: { } messageText } message)
            {
                var chatId = message.Chat.Id;
                var username = message.From.Username ?? message.From.FirstName;

                // --- ОБРАБОТКА ВВОДА ЧИСЛА (КАЛЬКУЛЯТОР ЦЕН) ---
                if (userState.ContainsKey(chatId) && userState[chatId] == "await_custom_quantity")
                {
                    if (int.TryParse(messageText, out int count))
                    {
                        decimal pricePerUnit = userFlower.ContainsKey(chatId) ? (userFlower[chatId] switch
                        {
                            "roses" => 8.6m,
                            "tulips" => 6.6m,
                            "dahlias" => 13m,
                            _ => 0m
                        }) : 0m;

                        int total = (int)Math.Round(count * pricePerUnit, 0, MidpointRounding.AwayFromZero);
                        await botClient.SendTextMessageAsync(chatId, $"💰 Цена за {count} шт.: {total}₽\n\nВы можете ввести другое число или нажать кнопку меню.", replyMarkup: GetBackToMenuKeyboard());
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "Пожалуйста, введите число цифрами.");
                    }
                    return;
                }

                // --- ОБРАБОТКА ВВОДА ЧИСЛА (ЗАКАЗ) ---
                if (userState.ContainsKey(chatId) && userState[chatId] == "await_quantity")
                {
                    if (int.TryParse(messageText, out int countParsed))
                    {
                        userQuantity[chatId] = countParsed;
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
                    await ShowDeliveryMenu(chatId);
            }
            else if (update.CallbackQuery is { Data: { } data })
            {
                var chatId = update.CallbackQuery.Message.Chat.Id;

                switch (data)
                {
                    // Логика кнопок калькулятора (цены)
                    case string d when d.StartsWith("price_category_") && d.EndsWith("_other"):
                        userState[chatId] = "await_custom_quantity";
                        userFlower[chatId] = d.Contains("roses") ? "roses" : d.Contains("tulips") ? "tulips" : "dahlias";
                        await botClient.SendTextMessageAsync(chatId, "Введите количество:");
                        break;

                    case string d when d.StartsWith("price_category_"):
                        var parts = d.Split('_');
                        int countPrice = int.Parse(parts[3]);
                        decimal unitPrice = d.Contains("roses") ? 8.6m : d.Contains("tulips") ? 6.6m : d.Contains("dahlias") ? 13m : 0m;
                        int totalResult = (int)Math.Round(countPrice * unitPrice, 0, MidpointRounding.AwayFromZero);
                        await botClient.SendTextMessageAsync(chatId, $"💰 Цена за {countPrice} шт.: {totalResult}₽", replyMarkup: GetBackToMenuKeyboard());
                        break;

                    case "start_menu":
                        await ShowMainMenu(chatId);
                        break;

                    case "start_price":
                        await ShowPriceMenu(chatId);
                        break;

                    case "start_order":
                        await ShowOrderMenu(chatId);
                        break;

                    case "start_contacts":
                        await ShowContacts(chatId);
                        break;

                    case "start_delivery":
                        await ShowDeliveryMenu(chatId);
                        break;

                    case "category_roses":
                    case "category_tulips":
                    case "category_dahlias":
                        var priceQuantityKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithCallbackData("15", $"price_{data}_15"), InlineKeyboardButton.WithCallbackData("31", $"price_{data}_31"), InlineKeyboardButton.WithCallbackData("51", $"price_{data}_51") },
                            new [] { InlineKeyboardButton.WithCallbackData("101", $"price_{data}_101"), InlineKeyboardButton.WithCallbackData("Другое количество", $"price_{data}_other") }
                        });
                        await botClient.SendTextMessageAsync(chatId, "Выберите количество:", replyMarkup: priceQuantityKeyboard);
                        break;

                    case "delivery_pmr":
                        await botClient.SendTextMessageAsync(chatId, "🚚 Способы доставки по ПМР:\n\n👥 Личная встреча возможна в Тирасполе и Дубоссарах\n\n🚌 Маршруткой\n— быстрая доставка в другие города\n— оплата при получении\n\n📮 Почтой\n— доставка по всей стране\n— срок 2–5 дней", replyMarkup: GetBackToMenuKeyboard());
                        break;

                    case "delivery_moldova":
                        await botClient.SendTextMessageAsync(chatId, "🚚 Способы доставки по Молдове:\n\n🚌 Маршруткой\n— быстрая доставка в города\n— оплата при получении\n\n📦 Nova Poshta\n— доставка в отделение\n— срок 2–5 дней", replyMarkup: GetBackToMenuKeyboard());
                        break;

                    case "delivery_other":
                        await botClient.SendTextMessageAsync(chatId, "К сожалению, доставка только по ПМР и Молдове.", replyMarkup: GetBackToMenuKeyboard());
                        break;

                    case "order_roses":
                    case "order_tulips":
                    case "order_dahlias":
                        userFlower[chatId] = data.Replace("order_", "");
                        userState[chatId] = "await_quantity";
                        var qtyKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithCallbackData("15", "qty_15"), InlineKeyboardButton.WithCallbackData("31", "qty_31"), InlineKeyboardButton.WithCallbackData("51", "qty_51") },
                            new [] { InlineKeyboardButton.WithCallbackData("101", "qty_101"), InlineKeyboardButton.WithCallbackData("Другое количество", "qty_custom") }
                        });
                        await botClient.SendTextMessageAsync(chatId, $"Выберите количество {(data.Contains("roses") ? "роз" : data.Contains("tulips") ? "тюльпанов" : "георгин")}:", replyMarkup: qtyKeyboard);
                        break;

                    case "qty_15":
                    case "qty_31":
                    case "qty_51":
                    case "qty_101":
                        int qty = int.Parse(data.Split('_')[1]);
                        userQuantity[chatId] = qty;
                        userState[chatId] = "await_extras";
                        var exKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithCallbackData("⭐️Блёстки⭐️", "extra_glitter"), InlineKeyboardButton.WithCallbackData("🖼️Картинка🖼️", "extra_picture") },
                            new [] { InlineKeyboardButton.WithCallbackData("🧸Игрушка🧸", "extra_toy"), InlineKeyboardButton.WithCallbackData("🦋Бабочки🦋", "extra_butterfly") },
                            new [] { InlineKeyboardButton.WithCallbackData("🎀Бантики🎀", "extra_ribbons") },
                            new [] { InlineKeyboardButton.WithCallbackData("✅ Готово", "extras_done") }
                        });
                        await botClient.SendTextMessageAsync(chatId, $"Вы выбрали: {qty} шт.\n\n⚠️ ЦЕНА ДОП. УСЛУГ ОБГОВАРИВАЕТСЯ С МАСТЕРОМ\n\nВыберите дополнения:", replyMarkup: exKeyboard);
                        break;

                    case "qty_custom":
                        userState[chatId] = "await_quantity";
                        await botClient.SendTextMessageAsync(chatId, "Введите нужное количество:");
                        break;

                    case "extra_glitter":
                    case "extra_picture":
                    case "extra_toy":
                    case "extra_butterfly":
                    case "extra_ribbons":
                        if (!userExtras.ContainsKey(chatId)) userExtras[chatId] = new List<string>();
                        string ex = data.Substring(6);
                        if (!userExtras[chatId].Contains(ex)) userExtras[chatId].Add(ex);
                        await botClient.SendTextMessageAsync(chatId, $"Добавлено: {ex}");
                        break;

                    case "extras_done":
                        await ShowCalendar(chatId, DateTime.Today.Year, DateTime.Today.Month);
                        break;

                    case "confirm_yes":
                        await botClient.SendTextMessageAsync(chatId, "✅ Заказ подтверждён! Мастер скоро напишет.", replyMarkup: GetBackToMenuKeyboard());

                        // Здесь можно использовать adminChatId
                        string extras = userExtras.ContainsKey(chatId) && userExtras[chatId].Count > 0
                ? string.Join(", ", userExtras[chatId])
                : "Нет";
                        string date = userDate.ContainsKey(chatId) ? userDate[chatId].ToString("dd.MM.yyyy") : "не выбрана";
                        decimal pricePerUnit = userFlower[chatId] switch
                        {
                            "roses" => 8.6m,
                            "tulips" => 6.6m,
                            "dahlias" => 13m,
                            _ => 0m
                        };
                        int totalPrice = (int)Math.Round(pricePerUnit * userQuantity[chatId], 0);

                        string orderInfo = $"📌 Новый заказ!\n" +
                                           $"Пользователь: @{update.CallbackQuery.From.Username ?? update.CallbackQuery.From.FirstName}\n" +
                                           $"Букет: {userFlower[chatId]}\n" +
                                           $"Количество: {userQuantity[chatId]}\n" +
                                           $"Дополнения: {extras}\n" +
                                           $"Дата: {date}\n" +
                                           $"💰 Итого: {totalPrice}₽";

                        ClearUser(chatId);
                        break;

                    case "confirm_no":
                        await botClient.SendTextMessageAsync(chatId, "❌ Заказ отменён.", replyMarkup: GetBackToMenuKeyboard());
                        ClearUser(chatId);
                        break;

                    default:
                        if (data.StartsWith("date_"))
                        {
                            var dateSelected = DateTime.ParseExact(data.Substring(5), "yyyy-MM-dd", null);
                            userDate[chatId] = dateSelected;
                            decimal p = userFlower[chatId] switch { "roses" => 8.6m, "tulips" => 6.6m, "dahlias" => 13m, _ => 0m };
                            int total = (int)Math.Round(userQuantity[chatId] * p, 0);
                            string receipt = $"✅ Чек заказа:\n\nБукет: {userFlower[chatId]}\n📦 Кол-во: {userQuantity[chatId]}\n✨ Доп: {(userExtras.ContainsKey(chatId) ? string.Join(", ", userExtras[chatId]) : "Нет")}\n💰 Итого: {total}₽\n📅 Дата: {dateSelected:dd.MM.yyyy}";
                            await botClient.SendTextMessageAsync(chatId, receipt);
                            await botClient.SendTextMessageAsync(chatId, "Подтвердить заказ?", replyMarkup: new InlineKeyboardMarkup(new[] {
                                new [] { InlineKeyboardButton.WithCallbackData("✅ Да", "confirm_yes"), InlineKeyboardButton.WithCallbackData("❌ Нет", "confirm_no") }
                            }));
                        }
                        else if (data.StartsWith("month_"))
                        {
                            var p = data.Substring(6).Split('-');
                            await ShowCalendar(chatId, int.Parse(p[0]), int.Parse(p[1]));
                        }
                        break;
                }
                await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
            }
        },
        async (bot, ex, ct) => Console.WriteLine(ex.Message)
    );
}

void ClearUser(long chatId)
{
    userState.Remove(chatId);
    userQuantity.Remove(chatId);
    userExtras.Remove(chatId);
    userFlower.Remove(chatId);
    userDate.Remove(chatId);
}

async Task ShowMainMenu(long chatId)
{
    ClearUser(chatId);
    var mk = new InlineKeyboardMarkup(new[] {
       new [] { InlineKeyboardButton.WithCallbackData("💰 Цены", "start_price") },
new [] { InlineKeyboardButton.WithCallbackData("🚚 Доставка", "start_delivery") },
new [] { InlineKeyboardButton.WithCallbackData("📞 Контакты", "start_contacts") },
new [] { InlineKeyboardButton.WithCallbackData("🌸 Сделать заказ", "start_order") }
    });
    await botClient.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: mk);
}

async Task ShowPriceMenu(long chatId)
{
    ClearUser(chatId);
    var k = new InlineKeyboardMarkup(new[] {
        new [] { InlineKeyboardButton.WithCallbackData("🌹 Розы 🌹", "category_roses") },
        new [] { InlineKeyboardButton.WithCallbackData("🌷 Тюльпаны 🌷", "category_tulips") },
        new [] { InlineKeyboardButton.WithCallbackData("🌺 Георгины 🌺", "category_dahlias") }
    });
    await botClient.SendTextMessageAsync(chatId, "Выберите категорию:", replyMarkup: k);
}

async Task ShowOrderMenu(long chatId)
{
    var k = new InlineKeyboardMarkup(new[] {
        new [] { InlineKeyboardButton.WithCallbackData("🌹 Розы 🌹", "order_roses") },
        new [] { InlineKeyboardButton.WithCallbackData("🌷 Тюльпаны 🌷", "order_tulips") },
        new [] { InlineKeyboardButton.WithCallbackData("🌺 Георгины 🌺", "order_dahlias") }
    });
    await botClient.SendTextMessageAsync(chatId, "Выберите букет:", replyMarkup: k);
}

async Task ShowContacts(long chatId)
{
    await botClient.SendTextMessageAsync(chatId, "📞 Наши контакты\n\n💬 Telegram мастера: @Vethbu\n📢 Канал: https://t.me/+6a3DugGFBHwzMmJi\n🎵 TikTok: bouquet_dubossary\n📷 Insta: bouquet_dubossary", replyMarkup: GetBackToMenuKeyboard());
}

async Task ShowDeliveryMenu(long chatId)
{
    var rk = new InlineKeyboardMarkup(new[] {
        new [] { InlineKeyboardButton.WithCallbackData("ПМР", "delivery_pmr"), InlineKeyboardButton.WithCallbackData("Молдова", "delivery_moldova") },
        new [] { InlineKeyboardButton.WithCallbackData("Другие страны", "delivery_other") }
    });
    await botClient.SendTextMessageAsync(chatId, "Откуда вы?", replyMarkup: rk);
}

async Task ShowCalendar(long chatId, int year, int month)
{
    var days = DateTime.DaysInMonth(year, month);
    var buttons = new List<InlineKeyboardButton[]>();
    for (int d = 1; d <= days; d += 7)
    {
        var week = new List<InlineKeyboardButton>();
        for (int i = d; i < d + 7 && i <= days; i++)
            week.Add(InlineKeyboardButton.WithCallbackData(i.ToString(), $"date_{year}-{month:D2}-{i:D2}"));
        buttons.Add(week.ToArray());
    }
    if (month < 12) buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➡️ След. месяц", $"month_{year}-{month + 1}") });
    await botClient.SendTextMessageAsync(chatId, $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month)} {year}", replyMarkup: new InlineKeyboardMarkup(buttons));
}

InlineKeyboardMarkup GetBackToMenuKeyboard() => new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("🏠 Меню", "start_menu") } });

Console.WriteLine("Бот запущен!");
app.Run();