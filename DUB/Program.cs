using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");
app.MapControllers();

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

// ===== ХРАНИЛИЩЕ СОСТОЯНИЙ =====

var userState = new Dictionary<long, string>();
var userQuantity = new Dictionary<long, int>();
var userFlower = new Dictionary<long, string>();
var userExtras = new Dictionary<long, List<string>>();
var userDate = new Dictionary<long, string>();

if (!string.IsNullOrEmpty(token))
{
    var botClient = new TelegramBotClient(token);
    await botClient.DeleteWebhookAsync();

    botClient.StartReceiving(async (bot, update, ct) =>
    {
        // =====================================================
        // ================== ТЕКСТОВЫЕ СООБЩЕНИЯ ==============
        // =====================================================

        if (update.Message is { Text: { } messageText } message)
        {
            var chatId = message.Chat.Id;
            var text = messageText.ToLower();

            // ===== ВВОД КОЛИЧЕСТВА (ЦЕНА ИЛИ ЗАКАЗ) =====

            if (userState.ContainsKey(chatId) &&
               (userState[chatId].StartsWith("price_") ||
                userState[chatId].StartsWith("order_")))
            {
                if (int.TryParse(messageText, out int count))
                {
                    string flower = userState[chatId].Substring(6);

                    decimal price = flower switch
                    {
                        "roses" => 8.6m,
                        "tulips" => 6.6m,
                        "dahlias" => 13m,
                        _ => 0
                    };

                    int total = (int)Math.Round(count * price, 0,
                        MidpointRounding.AwayFromZero);

                    await bot.SendTextMessageAsync(chatId, $"Цена: {total}₽");

                    // ===== ЕСЛИ ЭТО ЗАКАЗ =====
                    if (userState[chatId].StartsWith("order_"))
                    {
                        userQuantity[chatId] = count;
                        userFlower[chatId] = flower;
                        await ShowExtrasMenu(chatId);
                    }
                    else
                    {
                        await ShowPriceMenu(chatId);
                    }
                }
                else
                {
                    await bot.SendTextMessageAsync(chatId, "Введите число.");
                }
                return;
            }

            // ===== КОМАНДЫ =====

            if (text.StartsWith("/start"))
            {
                await ShowMainMenu(chatId);
            }
            else if (text.StartsWith("/price"))
            {
                await ShowPriceMenu(chatId);
            }
            else if (text.StartsWith("/order"))
            {
                await ShowOrderMenu(chatId);
            }
            else if (text.StartsWith("/cancel"))
            {
                CancelOrder(chatId);
                await bot.SendTextMessageAsync(chatId, "Заказ отменён.");
            }
        }

        // =====================================================
        // =================== CALLBACK КНОПКИ =================
        // =====================================================

        else if (update.CallbackQuery is { Data: { } data } cb)
        {
            var chatId = cb.Message.Chat.Id;

            switch (data)
            {
                // ===== ГЛАВНОЕ МЕНЮ =====

                case "menu_price":
                    await ShowPriceMenu(chatId);
                    break;

                case "menu_order":
                    await ShowOrderMenu(chatId);
                    break;

                case "menu_contacts":
                    var contactsKeyboard = new InlineKeyboardMarkup(
                        InlineKeyboardButton.WithUrl(
                            "Instagram: bouquet_dubossary",
                            "https://www.instagram.com/bouquet_dubossary?igsh=ZDhzeHpzZmNiMWE5&utm_source=qr"));

                    await botClient.SendTextMessageAsync(chatId,
                        "Наши контакты:",
                        replyMarkup: contactsKeyboard);

                    await botClient.SendTextMessageAsync(chatId,
                        "Telegram: @youscum1");
                    break;

                // ===== ЦЕНЫ =====

                case "price_roses":
                case "price_tulips":
                case "price_dahlias":
                    userState[chatId] = data;
                    await botClient.SendTextMessageAsync(chatId,
                        "Введите количество:");
                    break;

                // ===== ЗАКАЗ =====

                case "order_roses":
                case "order_tulips":
                case "order_dahlias":
                    userState[chatId] = data;
                    await botClient.SendTextMessageAsync(chatId,
                        "Введите количество для заказа:");
                    break;

                // ===== ДОПОЛНИТЕЛЬНО =====

                case "extra_glitter":
                    AddExtra(chatId, "Блёстки");
                    await ShowExtrasMenu(chatId);
                    break;

                case "extra_toy":
                    AddExtra(chatId, "Игрушка");
                    await ShowExtrasMenu(chatId);
                    break;

                case "extra_picture":
                    var picKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] {
                            InlineKeyboardButton.WithCallbackData("Картинка 1","pic1"),
                            InlineKeyboardButton.WithCallbackData("Картинка 2","pic2")
                        },
                        new [] {
                            InlineKeyboardButton.WithCallbackData("Своя картинка","pic_custom")
                        }
                    });
                    await botClient.SendTextMessageAsync(chatId,
                        "Выберите картинку:",
                        replyMarkup: picKeyboard);
                    break;

                case "pic1": AddExtra(chatId, "Картинка 1"); await ShowExtrasMenu(chatId); break;
                case "pic2": AddExtra(chatId, "Картинка 2"); await ShowExtrasMenu(chatId); break;
                case "pic_custom": AddExtra(chatId, "Своя картинка"); await ShowExtrasMenu(chatId); break;

                // ===== БАНТИКИ =====

                case "extra_ribbons":
                    var ribbonKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] {
                            InlineKeyboardButton.WithCallbackData("Красные","r_red"),
                            InlineKeyboardButton.WithCallbackData("Розовые","r_pink")
                        },
                        new [] {
                            InlineKeyboardButton.WithCallbackData("Бордовые","r_bord"),
                            InlineKeyboardButton.WithCallbackData("Жёлтые","r_yellow")
                        },
                        new [] {
                            InlineKeyboardButton.WithCallbackData("Фиолетовые","r_purple")
                        }
                    });
                    await botClient.SendTextMessageAsync(chatId,
                        "Выберите цвет бантика:",
                        replyMarkup: ribbonKeyboard);
                    break;

                case "r_red": AddExtra(chatId, "Бантик красный"); await ShowExtrasMenu(chatId); break;
                case "r_pink": AddExtra(chatId, "Бантик розовый"); await ShowExtrasMenu(chatId); break;
                case "r_bord": AddExtra(chatId, "Бантик бордовый"); await ShowExtrasMenu(chatId); break;
                case "r_yellow": AddExtra(chatId, "Бантик жёлтый"); await ShowExtrasMenu(chatId); break;
                case "r_purple": AddExtra(chatId, "Бантик фиолетовый"); await ShowExtrasMenu(chatId); break;

                // ===== ГОТОВО → КАЛЕНДАРЬ =====

                case "extras_done":
                    await ShowCalendar(chatId);
                    break;

                case "order_cancel":
                    CancelOrder(chatId);
                    await botClient.SendTextMessageAsync(chatId, "Заказ отменён.");
                    break;

                // ===== ВЫБОР ДАТЫ =====

                default:
                    if (data.StartsWith("date_"))
                    {
                        string date = data.Substring(5);
                        userDate[chatId] = date;

                        await SendReceipt(chatId, cb.From);
                    }
                    break;
            }

            await botClient.AnswerCallbackQueryAsync(cb.Id);
        }

    }, async (bot, ex, ct) =>
    {
        Console.WriteLine(ex.Message);
    });

    Console.WriteLine("Бот запущен");
}

app.Run();


// =====================================================
// =================== МЕНЮ =============================
// =====================================================

async Task ShowMainMenu(long chatId)
{
    var kb = new InlineKeyboardMarkup(new[]
    {
        new [] { InlineKeyboardButton.WithCallbackData("Цены","menu_price") },
        new [] { InlineKeyboardButton.WithCallbackData("Сделать заказ","menu_order") },
        new [] { InlineKeyboardButton.WithCallbackData("Контакты","menu_contacts") }
    });

    await new TelegramBotClient(token)
        .SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: kb);
}

async Task ShowPriceMenu(long chatId)
{
    var kb = new InlineKeyboardMarkup(new[]
    {
        new [] { InlineKeyboardButton.WithCallbackData("Розы","price_roses") },
        new [] { InlineKeyboardButton.WithCallbackData("Тюльпаны","price_tulips") },
        new [] { InlineKeyboardButton.WithCallbackData("Георгины","price_dahlias") }
    });

    await new TelegramBotClient(token)
        .SendTextMessageAsync(chatId, "Выберите цветы:");
}

async Task ShowOrderMenu(long chatId)
{
    var kb = new InlineKeyboardMarkup(new[]
    {
        new [] { InlineKeyboardButton.WithCallbackData("Розы","order_roses") },
        new [] { InlineKeyboardButton.WithCallbackData("Тюльпаны","order_tulips") },
        new [] { InlineKeyboardButton.WithCallbackData("Георгины","order_dahlias") }
    });

    await new TelegramBotClient(token)
        .SendTextMessageAsync(chatId, "Выберите букет:", replyMarkup: kb);
}

// =====================================================
// ================= ДОПОЛНИТЕЛЬНО =====================
// =====================================================

void AddExtra(long chatId, string item)
{
    if (!userExtras.ContainsKey(chatId))
        userExtras[chatId] = new List<string>();

    userExtras[chatId].Add(item);
}

async Task ShowExtrasMenu(long chatId)
{
    var kb = new InlineKeyboardMarkup(new[]
    {
        new [] {
            InlineKeyboardButton.WithCallbackData("Блёстки","extra_glitter"),
            InlineKeyboardButton.WithCallbackData("Картинка","extra_picture")
        },
        new [] {
            InlineKeyboardButton.WithCallbackData("Игрушка","extra_toy"),
            InlineKeyboardButton.WithCallbackData("Бантики","extra_ribbons")
        },
        new [] {
            InlineKeyboardButton.WithCallbackData("Готово","extras_done")
        },
        new [] {
            InlineKeyboardButton.WithCallbackData("Отмена","order_cancel")
        }
    });

    await new TelegramBotClient(token)
        .SendTextMessageAsync(chatId, "Дополнительно:", replyMarkup: kb);
}

// =====================================================
// ================= КАЛЕНДАРЬ =========================
// =====================================================

async Task ShowCalendar(long chatId)
{
    var now = DateTime.Now;
    var buttons = new List<InlineKeyboardButton[]>();

    for (int i = 0; i < 7; i++)
    {
        var d = now.AddDays(i);
        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(
                d.ToString("dd.MM.yyyy"),
                "date_" + d.ToString("dd.MM.yyyy"))
        });
    }

    await new TelegramBotClient(token)
        .SendTextMessageAsync(chatId,
            "Выберите дату:",
            replyMarkup: new InlineKeyboardMarkup(buttons));
}

// =====================================================
// ================= ЧЕК ===============================
// =====================================================

async Task SendReceipt(long chatId, Telegram.Bot.Types.User user)
{
    string flower = userFlower.ContainsKey(chatId) ? userFlower[chatId] : "";
    int qty = userQuantity.ContainsKey(chatId) ? userQuantity[chatId] : 0;

    decimal price = flower switch
    {
        "roses" => 8.6m,
        "tulips" => 6.6m,
        "dahlias" => 13m,
        _ => 0
    };

    int total = (int)Math.Round(qty * price, 0, MidpointRounding.AwayFromZero);

    string extras = userExtras.ContainsKey(chatId)
        ? string.Join(", ", userExtras[chatId])
        : "Нет";

    string buyer = user.Username ?? "Без username";
    string date = userDate[chatId];

    string receipt =
$@"ЧЕК

Продавец: Youscam
Покупатель: @{buyer}

Товар: {flower}
Количество: {qty}
Дополнительно: {extras}

Сумма: {total}₽
Дата: {date}";

    var confirmKb = new InlineKeyboardMarkup(new[]
    {
        new [] {
            InlineKeyboardButton.WithCallbackData("Да","order_cancel"),
            InlineKeyboardButton.WithCallbackData("Нет","order_ok")
        }
    });

    var bot = new TelegramBotClient(token);

    await bot.SendTextMessageAsync(chatId, receipt);
    await bot.SendTextMessageAsync(chatId,
        "Вы хотите отменить заказ?",
        replyMarkup: confirmKb);
}

// =====================================================
// ================= ОТМЕНА ============================
// =====================================================

void CancelOrder(long chatId)
{
    userState.Remove(chatId);
    userQuantity.Remove(chatId);
    userFlower.Remove(chatId);
    userExtras.Remove(chatId);
    userDate.Remove(chatId);
}
