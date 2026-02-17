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

// Словари состояний
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

                // 1. ПРОВЕРКА КОМАНД (Сначала проверяем их, чтобы сбросить ожидание числа)
                if (messageText.StartsWith("/"))
                {
                    userState.Remove(chatId); // Сбрасываем старое состояние

                    if (messageText.ToLower().StartsWith("/start"))
                        await ShowMainMenu(chatId, botClient);
                    else if (messageText.ToLower().StartsWith("/price"))
                        await ShowPriceMenu(chatId, botClient);
                    else if (messageText.ToLower().StartsWith("/order"))
                        await ShowOrderMenu(chatId, botClient);
                    else if (messageText.ToLower().StartsWith("/contacts"))
                        await ShowContacts(chatId, botClient);
                    else if (messageText.ToLower().StartsWith("/delivery"))
                        await ShowDeliveryMenu(chatId, botClient);

                    return;
                }

                // 2. ПРОВЕРКА СОСТОЯНИЙ (Ожидание ввода числа)

                // Калькулятор цен
                if (userState.ContainsKey(chatId) && (userState[chatId] == "roses" || userState[chatId] == "tulips" || userState[chatId] == "dahlias"))
                {
                    if (int.TryParse(messageText, out int count))
                    {
                        decimal pricePerUnit = userState[chatId] switch { "roses" => 8.6m, "tulips" => 6.6m, "dahlias" => 13m, _ => 0 };
                        int rounded = (int)Math.Round(count * pricePerUnit, 0);
                        await bot.SendTextMessageAsync(chatId, $"Цена: {rounded}₽\n\nМожете ввести другое количество.");
                    }
                    else
                    {
                        await bot.SendTextMessageAsync(chatId, "Введите число.");
                    }
                    return;
                }

                // Ввод количества для ЗАКАЗА
                if (userState.ContainsKey(chatId) && userState[chatId] == "await_quantity")
                {
                    if (int.TryParse(messageText, out int count))
                    {
                        userQuantity[chatId] = count;
                        userState[chatId] = "await_extras";

                        var extrasKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] { InlineKeyboardButton.WithCallbackData("Блёстки", "extra_glitter"), InlineKeyboardButton.WithCallbackData("Картинка", "extra_picture") },
                            new [] { InlineKeyboardButton.WithCallbackData("Игрушка", "extra_toy"), InlineKeyboardButton.WithCallbackData("Бабочки", "extra_butterfly") },
                            new [] { InlineKeyboardButton.WithCallbackData("Бантики", "extra_ribbons") },
                            new [] { InlineKeyboardButton.WithCallbackData("✅ Готово", "extras_done") }
                        });

                        await bot.SendTextMessageAsync(chatId, "Выберите дополнительные элементы (можно несколько):", replyMarkup: extrasKeyboard);
                    }
                    else
                    {
                        await bot.SendTextMessageAsync(chatId, "Пожалуйста, введите число.");
                    }
                    return;
                }
            }
            else if (update.CallbackQuery is { Data: { } data })
            {
                var chatId = update.CallbackQuery.Message.Chat.Id;

                switch (data)
                {
                    case "start_price": await ShowPriceMenu(chatId, botClient); break;
                    case "start_order": await ShowOrderMenu(chatId, botClient); break;
                    case "start_contacts": await ShowContacts(chatId, botClient); break;
                    case "start_delivery": await ShowDeliveryMenu(chatId, botClient); break;

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

                    case "order_roses":
                    case "order_tulips":
                    case "order_dahlias":
                        userFlower[chatId] = data.Substring(6);
                        userState[chatId] = "await_quantity";
                        await botClient.SendTextMessageAsync(chatId, "Введите количество:");
                        break;

                    case "extras_done":
                        userState[chatId] = "await_date";
                        await ShowCalendar(chatId, botClient, DateTime.Today.Year, DateTime.Today.Month);
                        break;

                        // Тут можно добавить обработку кнопок доставки (delivery_pmr и т.д.)
                }
                await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
            }
        },
        async (bot, ex, ct) => Console.WriteLine(ex.Message)
    );
}

// Вспомогательные методы (вынесены за пределы StartReceiving)

async Task ShowMainMenu(long chatId, ITelegramBotClient botClient)
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

async Task ShowPriceMenu(long chatId, ITelegramBotClient botClient)
{
    var keyboard = new InlineKeyboardMarkup(new[]
    {
        new [] { InlineKeyboardButton.WithCallbackData("Розы", "category_roses") },
        new [] { InlineKeyboardButton.WithCallbackData("Тюльпаны", "category_tulips") },
        new [] { InlineKeyboardButton.WithCallbackData("Георгины", "category_dahlias") }
    });
    await botClient.SendTextMessageAsync(chatId, "Выберите категорию для расчета цены:", replyMarkup: keyboard);
}

async Task ShowOrderMenu(long chatId, ITelegramBotClient botClient)
{
    var keyboard = new InlineKeyboardMarkup(new[]
    {
        new [] { InlineKeyboardButton.WithCallbackData("Розы", "order_roses") },
        new [] { InlineKeyboardButton.WithCallbackData("Тюльпаны", "order_tulips") },
        new [] { InlineKeyboardButton.WithCallbackData("Георгины", "order_dahlias") }
    });
    await botClient.SendTextMessageAsync(chatId, "Выберите цветы для заказа:", replyMarkup: keyboard);
}

async Task ShowContacts(long chatId, ITelegramBotClient botClient)
{
    await botClient.SendTextMessageAsync(chatId, "Наши контакты:\nTelegram: @Youscam");
}

async Task ShowDeliveryMenu(long chatId, ITelegramBotClient botClient)
{
    var keyboard = new InlineKeyboardMarkup(new[]
    {
        new [] { InlineKeyboardButton.WithCallbackData("ПМР", "delivery_pmr") },
        new [] { InlineKeyboardButton.WithCallbackData("Молдова", "delivery_moldova") }
    });
    await botClient.SendTextMessageAsync(chatId, "Откуда вы?", replyMarkup: keyboard);
}

async Task ShowCalendar(long chatId, ITelegramBotClient botClient, int year, int month)
{
    await botClient.SendTextMessageAsync(chatId, $"Календарь на {month}.{year} (в разработке)");
}

app.Run();