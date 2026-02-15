using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

var bot = new TelegramBotClient("ТВОЙ_ТОКЕН");

var userState = new Dictionary<long, string>();
var order = new Dictionary<long, OrderData>();

bot.StartReceiving(Update, Error);

Console.ReadLine();

async Task Update(ITelegramBotClient bot, Update update, CancellationToken ct)
{
    // ================= CALLBACK =================
    if (update.Type == UpdateType.CallbackQuery)
    {
        var chatId = update.CallbackQuery.Message.Chat.Id;
        var data = update.CallbackQuery.Data;

        // ===== НАЧАЛО ЗАКАЗА =====
        if (data == "order")
        {
            userState[chatId] = "choose_flower";

            await bot.SendTextMessageAsync(chatId,
                "Выберите букет:",
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🌹 Розы", "rose"),
                        InlineKeyboardButton.WithCallbackData("🌷 Тюльпаны", "tulip"),
                        InlineKeyboardButton.WithCallbackData("🌼 Георгины", "dahlia")
                    }
                }));
            return;
        }

        // ===== ВЫБОР ЦВЕТОВ =====
        if (data == "rose" || data == "tulip" || data == "dahlia")
        {
            order[chatId] = new OrderData { Flower = data };
            userState[chatId] = "count";

            await bot.SendTextMessageAsync(chatId,
                "Введите количество:");
            return;
        }

        // ===== ДОПОЛНИТЕЛЬНО =====
        if (data == "extras_done")
        {
            userState[chatId] = "calendar";
            await ShowCalendar(bot, chatId, DateTime.Now);
            return;
        }

        if (data.StartsWith("extra_"))
        {
            order[chatId].Extras.Add(data.Replace("extra_", ""));
            return;
        }

        // ===== БАНТИКИ ЦВЕТ =====
        if (data.StartsWith("bow_"))
        {
            order[chatId].Extras.Add("Бантики: " + data.Replace("bow_", ""));
            return;
        }

        // ===== КАЛЕНДАРЬ =====
        if (data.StartsWith("date_"))
        {
            order[chatId].Date = data.Replace("date_", "");

            await SendCheck(bot, chatId);
            return;
        }

        if (data.StartsWith("month_"))
        {
            var date = DateTime.Parse(data.Replace("month_", ""));
            await ShowCalendar(bot, chatId, date);
            return;
        }

        // ===== ОТМЕНА =====
        if (data == "cancel_yes")
        {
            await bot.SendTextMessageAsync(chatId, "❌ Заказ отменён");
            userState.Remove(chatId);
            order.Remove(chatId);
            return;
        }

        if (data == "cancel_no")
        {
            await bot.SendTextMessageAsync(chatId,
                "💖 Спасибо! С вами свяжутся в ближайшее время.");
            userState.Remove(chatId);
            return;
        }
    }

    // ================= ТЕКСТ =================
    if (update.Type != UpdateType.Message) return;

    var msg = update.Message;
    var chat = msg.Chat.Id;
    var text = msg.Text;

    // ===== СТАРТ =====
    if (text == "/start")
    {
        await bot.SendTextMessageAsync(chat,
            "Главное меню:",
            replyMarkup: new ReplyKeyboardMarkup(new[]
            {
                new[] { "💰 Прайс", "🚚 Доставка" },
                new[] { "🛒 Сделать заказ", "📞 Контакты" }
            })
            { ResizeKeyboard = true });

        return;
    }

    // ===== СДЕЛАТЬ ЗАКАЗ =====
    if (text == "🛒 Сделать заказ" || text == "/order")
    {
        userState[chat] = "order";

        await bot.SendTextMessageAsync(chat,
            "Нажмите кнопку ниже 👇",
            replyMarkup: new InlineKeyboardMarkup(
                InlineKeyboardButton.WithCallbackData("🛒 Выбрать букет", "order")));

        return;
    }

    // ===== КОНТАКТЫ =====
    if (text == "📞 Контакты" || text == "/contact")
    {
        await bot.SendTextMessageAsync(chat,
            "Instagram:\nhttps://www.instagram.com/bouquet_dubossary\n\nTelegram:\n@youscum1");
        return;
    }

    // ===== ДОСТАВКА =====
    if (text == "🚚 Доставка")
    {
        await bot.SendTextMessageAsync(chat,
            "Откуда вы?",
            replyMarkup: new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("ПМР", "pmr"),
                    InlineKeyboardButton.WithCallbackData("Молдова", "md"),
                    InlineKeyboardButton.WithCallbackData("Другие страны", "other")
                }
            }));
        return;
    }

    // ===== ВВОД КОЛИЧЕСТВА =====
    if (userState.ContainsKey(chat) && userState[chat] == "count")
    {
        if (!int.TryParse(text, out int count)) return;

        order[chat].Count = count;

        double pricePer = order[chat].Flower switch
        {
            "rose" => 8.6,
            "tulip" => 6.6,
            "dahlia" => 13,
            _ => 0
        };

        var total = Math.Round(pricePer * count / 10) * 10;
        order[chat].Price = total;

        userState[chat] = "extras";

        await bot.SendTextMessageAsync(chat,
            $"Цена: {total} R\n\nВыберите дополнительно:",
            replyMarkup: new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("✨ Блёстки", "extra_Блёстки"),
                    InlineKeyboardButton.WithCallbackData("🖼 Картинка", "extra_Картинка")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🦋 Бабочки", "extra_Бабочки"),
                    InlineKeyboardButton.WithCallbackData("🎀 Бантики", "bows")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("✅ Далее", "extras_done")
                }
            }));

        return;
    }
}

async Task ShowCalendar(ITelegramBotClient bot, long chatId, DateTime month)
{
    var buttons = new List<List<InlineKeyboardButton>>();

    int days = DateTime.DaysInMonth(month.Year, month.Month);
    int day = 1;

    while (day <= days)
    {
        var row = new List<InlineKeyboardButton>();

        for (int i = 0; i < 7 && day <= days; i++)
        {
            var date = new DateTime(month.Year, month.Month, day);
            row.Add(InlineKeyboardButton.WithCallbackData(
                day.ToString(),
                "date_" + date.ToString("dd.MM.yyyy")));
            day++;
        }

        buttons.Add(row);
    }

    var nav = new List<InlineKeyboardButton>();

    if (month > DateTime.Now)
        nav.Add(InlineKeyboardButton.WithCallbackData("⬅️",
            "month_" + month.AddMonths(-1).ToString("yyyy-MM-01")));

    nav.Add(InlineKeyboardButton.WithCallbackData("➡️",
        "month_" + month.AddMonths(1).ToString("yyyy-MM-01")));

    buttons.Add(nav);

    await bot.SendTextMessageAsync(chatId,
        $"📅 Выберите дату ({month:MMMM yyyy})",
        replyMarkup: new InlineKeyboardMarkup(buttons));
}

async Task SendCheck(ITelegramBotClient bot, long chatId)
{
    var o = order[chatId];

    var extras = o.Extras.Count > 0
        ? string.Join(", ", o.Extras)
        : "Нет";

    var buyer = o.UserName ?? "Без username";

    await bot.SendTextMessageAsync(chatId,
$@"✅ ЧЕК-ЗАКАЗ

Продавец: Youscam
Покупатель: @{buyer}

Букет: {o.Flower}
Количество: {o.Count}
Дополнительно: {extras}

Сумма: {o.Price} R
Дата: {o.Date}",
        replyMarkup: new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("❌ Да", "cancel_yes"),
                InlineKeyboardButton.WithCallbackData("✔️ Нет", "cancel_no")
            }
        }));
}

Task Error(ITelegramBotClient bot, Exception ex, CancellationToken ct)
{
    Console.WriteLine(ex.Message);
    return Task.CompletedTask;
}

class OrderData
{
    public string Flower { get; set; }
    public int Count { get; set; }
    public double Price { get; set; }
    public string Date { get; set; }
    public List<string> Extras { get; set; } = new();
    public string UserName { get; set; }
}
