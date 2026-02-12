using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// Добавляем поддержку контроллеров (чтобы TelegramController заработал)
builder.Services.AddControllers();

var app = builder.Build();

// Настройка порта для Railway
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");

app.MapControllers();

// Читаем токен
var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

if (!string.IsNullOrEmpty(token))
{
    var botClient = new TelegramBotClient(token);

    // СБРОС ВЕБХУКА
    await botClient.DeleteWebhookAsync();

    // Запускаем простую проверку обновлений
    botClient.StartReceiving(
        async (bot, update, ct) => {
            if (update.Message is not { Text: { } messageText } message) return;
            var chatId = message.Chat.Id;

            // Приводим текст к нижнему регистру, чтобы команды работали независимо от регистра
            var text = messageText.ToLower();

            if (text.StartsWith("/start"))
            {
                await bot.SendTextMessageAsync(chatId, "Максим лох");
            }
            else if (text.StartsWith("/price") || text.StartsWith("/цена"))
            {
                // Пример цены — можно менять на любую
                decimal price = 199.99m;
                await bot.SendTextMessageAsync(chatId, $"Текущая цена: {price}₽");
            }
            else
            {
                await bot.SendTextMessageAsync(chatId, $"Ты написал: {messageText}");
            }
        },
        async (bot, ex, ct) => Console.WriteLine("Ошибка: " + ex.Message)
    );

    Console.WriteLine("Бот успешно запущен!");
}

app.Run();
