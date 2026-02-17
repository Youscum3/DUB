using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    // --- НАСТРОЙКИ ---
    private static ITelegramBotClient botClient = new TelegramBotClient("8534528756:AAFee9Aq2tqnU9oSh1QdUZQUmoxUVnnoTfg");
    private static readonly long[] AdminIds = {  }; // ID @youscum1 и @Vethbu

    // Цены
    private static readonly Dictionary<string, double> Prices = new()
    {
        { "Розы", 8.6 },
        { "Тюльпаны", 6.6 },
        { "Георгины", 13.0 }
    };

    // Хранилище состояний пользователей
    private static Dictionary<long, UserState> UserData = new();

    static async Task Main(string[] args)
    {
        using var cts = new CancellationTokenSource();

        botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
            cancellationToken: cts.Token
        );

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Бот @{me.Username} запущен. Нажми Enter для выхода.");
        Console.ReadLine();
        cts.Cancel();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Type == UpdateType.Message && update.Message?.Text != null)
        {
            var msg = update.Message;
            if (msg.Text == "/start")
            {
                UserData.Remove(msg.Chat.Id);
                await bot.SendTextMessageAsync(msg.Chat.Id,
                    "Здравствуйте, это телеграмм бот для заказа букета из атласных лент.\nМы работаем по ПМР и МОЛДОВЕ.\nВыберите что вас интересует",
                    replyMarkup: GetMainKeyboard(), cancellationToken: ct);
                return;
            }

            // Обработка ручного ввода количества
            if (UserData.TryGetValue(msg.Chat.Id, out var state) && state.Step == "WaitingQty")
            {
                if (int.TryParse(msg.Text, out int qty))
                {
                    await ProcessTotal(bot, msg.Chat.Id, qty, ct);
                }
                else await bot.SendTextMessageAsync(msg.Chat.Id, "Введите числовое значение.");
            }
            // Обработка ввода даты
            else if (state != null && state.Step == "WaitingDate")
            {
                await FinishOrder(bot, msg, ct);
            }
        }

        if (update.Type == UpdateType.CallbackQuery)
        {
            var cb = update.CallbackQuery;
            long chatId = cb.Message.Chat.Id;

            if (cb.Data == "start_over")
            {
                UserData.Remove(chatId);
                await bot.EditMessageTextAsync(chatId, cb.Message.MessageId, "Выберите что вас интересует:", replyMarkup: GetMainKeyboard(), cancellationToken: ct);
            }
            else if (cb.Data == "delivery")
            {
                await bot.EditMessageTextAsync(chatId, cb.Message.MessageId, "🚚 Мы работаем по ПМР и МОЛДОВЕ, доставка за ваш счет удобным вам способом! 🚚", replyMarkup: GetBackKb(), cancellationToken: ct);
            }
            else if (cb.Data == "contacts")
            {
                string text = "☎️ Наши контакты ☎️:\nTelegram мастера: @Vethbu\nКанал: https://t.me/+6a3DugGFBHwzMmJi\nTikTok: [Открыть](https://www.tiktok.com/@bouquet_dubossary)\nInstagram: [Открыть](https://www.instagram.com/bouquet_dubossary)";
                await bot.EditMessageTextAsync(chatId, cb.Message.MessageId, text, parseMode: ParseMode.Markdown, replyMarkup: GetBackKb(), cancellationToken: ct);
            }
            else if (cb.Data == "price_list" || cb.Data == "make_order")
            {
                string mode = cb.Data == "make_order" ? "order" : "price";
                await bot.EditMessageTextAsync(chatId, cb.Message.MessageId, "Выберите вид цветов:", replyMarkup: GetFlowersKb(mode), cancellationToken: ct);
            }
            else if (cb.Data.StartsWith("sel_")) // Выбор цветка
            {
                var parts = cb.Data.Split('_');
                UserData[chatId] = new UserState { Mode = parts[1], Flower = parts[2] };
                await bot.EditMessageTextAsync(chatId, cb.Message.MessageId, $"🌹 Выберите количество ({parts[2]}) 🌹", replyMarkup: GetQtyKb(), cancellationToken: ct);
            }
            else if (cb.Data.StartsWith("qty_")) // Выбор количества
            {
                string val = cb.Data.Split('_')[1];
                if (val == "custom")
                {
                    UserData[chatId].Step = "WaitingQty";
                    await bot.EditMessageTextAsync(chatId, cb.Message.MessageId, "Введите количество:", cancellationToken: ct);
                }
                else await ProcessTotal(bot, chatId, int.Parse(val), ct, cb.Message.MessageId);
            }
            else if (cb.Data == "confirm")
            {
                UserData[chatId].Step = "WaitingDate";
                await bot.EditMessageTextAsync(chatId, cb.Message.MessageId, "📆 Напишите дату на которую вам нужен букет, так же откуда вы. 📆", cancellationToken: ct);
            }
        }
    }

    // --- ЛОГИКА РАСЧЕТА И ЗАВЕРШЕНИЯ ---
    static async Task ProcessTotal(ITelegramBotClient bot, long chatId, int qty, CancellationToken ct, int msgId = 0)
    {
        var state = UserData[chatId];
        state.Quantity = qty;
        state.TotalPrice = Math.Round(qty * Prices[state.Flower], 2);

        string text = $"💰 Стоимость вашего букета будет {state.TotalPrice} рублей 💰";
        InlineKeyboardMarkup kb = state.Mode == "order"
            ? new(new[] { new[] { InlineKeyboardButton.WithCallbackData("✅ Продолжить ✅", "confirm") }, new[] { InlineKeyboardButton.WithCallbackData("❌ Меню ❌", "start_over") } })
            : GetBackKb();

        if (msgId != 0) await bot.EditMessageTextAsync(chatId, msgId, text, replyMarkup: kb, cancellationToken: ct);
        else await bot.SendTextMessageAsync(chatId, text, replyMarkup: kb, cancellationToken: ct);
    }

    static async Task FinishOrder(ITelegramBotClient bot, Message msg, CancellationToken ct)
    {
        var state = UserData[msg.Chat.Id];
        string adminMsg = $"🌸 **Новый заказ!**\n\n💰 Цена: {state.TotalPrice} руб\n🔢 Кол-во: {state.Quantity}\n💐 Вид: {state.Flower}\n📆 Дата: {msg.Text}\n👤 От: @{msg.From.Username}\n🆔 ID: `{msg.From.Id}`";

        foreach (var id in AdminIds)
            await bot.SendTextMessageAsync(id, adminMsg, parseMode: ParseMode.Markdown, cancellationToken: ct);

        await bot.SendTextMessageAsync(msg.Chat.Id, "Ваш заказ принят! Мастер свяжется с вами.", replyMarkup: GetMainKeyboard(), cancellationToken: ct);
        UserData.Remove(msg.Chat.Id);
    }

    // --- КЛАВИАТУРЫ ---
    static InlineKeyboardMarkup GetMainKeyboard() => new(new[] {
        new[] { InlineKeyboardButton.WithCallbackData("🌹Сделать заказ🌹", "make_order") },
        new[] { InlineKeyboardButton.WithCallbackData("🚚Доставка🚚", "delivery") },
        new[] { InlineKeyboardButton.WithCallbackData("☎️Контакты☎️", "contacts") },
        new[] { InlineKeyboardButton.WithCallbackData("💰Цена💰", "price_list") }
    });

    static InlineKeyboardMarkup GetFlowersKb(string mode) => new(new[] {
        new[] { InlineKeyboardButton.WithCallbackData("🌷Тюльпаны🌷", $"sel_{mode}_Тюльпаны") },
        new[] { InlineKeyboardButton.WithCallbackData("🌺Георгины🌺", $"sel_{mode}_Георгины") },
        new[] { InlineKeyboardButton.WithCallbackData("🌹Розы🌹", $"sel_{mode}_Розы") }
    });

    static InlineKeyboardMarkup GetQtyKb() => new(new[] {
        new[] { InlineKeyboardButton.WithCallbackData("15", "qty_15"), InlineKeyboardButton.WithCallbackData("31", "qty_31") },
        new[] { InlineKeyboardButton.WithCallbackData("51", "qty_51"), InlineKeyboardButton.WithCallbackData("101", "qty_101") },
        new[] { InlineKeyboardButton.WithCallbackData("Другое количество", "qty_custom") }
    });

    static InlineKeyboardMarkup GetBackKb() => new(new[] { InlineKeyboardButton.WithCallbackData("Вернуться в меню", "start_over") });

    static Task HandlePollingErrorAsync(ITelegramBotClient b, Exception e, CancellationToken c) { Console.WriteLine(e); return Task.CompletedTask; }
}

class UserState
{
    public string Mode { get; set; } // "order" или "price"
    public string Flower { get; set; }
    public string Step { get; set; }
    public int Quantity { get; set; }
    public double TotalPrice { get; set; }
}