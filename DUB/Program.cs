using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");
app.MapControllers();

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

// Словарь для хранения состояния пользователя
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

                // Проверяем, ждём ли мы от пользователя число для роз
                if (userState.ContainsKey(chatId) && userState[chatId] == "waiting_roses")
                {
                    if (int.TryParse(messageText, out int count))
                    {
                        decimal pricePerRose = 8.6m;
                        decimal total = count * pricePerRose;
                        await bot.SendTextMessageAsync(chatId, $"Цена за {count} роз: {total}₽");
                        userState.Remove(chatId); // убираем состояние
                    }
                    else
                    {
                        await bot.SendTextMessageAsync(chatId, "Пожалуйста, введите число.");
                    }
                    return;
                }

                // Остальные команды
                if (messageText.ToLower().StartsWith("/start"))
                {
                    await bot.SendTextMessageAsync(chatId, "Привет! Нажми кнопку 'Прайс', чтобы увидеть категории цветов.");
                }
                else if (messageText.ToLower().StartsWith("/price") || messageText.ToLower().StartsWith("/цена"))
                {
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("Розы", "category_roses") },
                        new [] { InlineKeyboardButton.WithCallbackData("Тюльпаны", "category_tulips") },
                        new [] { InlineKeyboardButton.WithCallbackData("Георгины", "category_dahlias") }
                    });
                    await bot.SendTextMessageAsync(chatId, "Выберите категорию:", replyMarkup: keyboard);
                }
            }
            else if (update.CallbackQuery is { Data: { } callbackData })
            {
                var chatId = update.CallbackQuery.Message.Chat.Id;

                if (callbackData == "category_roses")
                {
                    // Просто просим ввести количество
                    userState[chatId] = "waiting_roses";
                    await botClient.SendTextMessageAsync(chatId, "Введите, сколько штук вам нужно.");
                }

                // Можно добавить состояния для тюльпанов, георгин аналогично
                await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
            }
        },
        async (bot, ex, ct) => Console.WriteLine("Ошибка: " + ex.Message)
    );

    Console.WriteLine("Бот успешно запущен!");
}

app.Run();
