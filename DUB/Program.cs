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

    // СБРОС ВЕБХУКА: это заставит Telegram перестать отправлять сообщения старым ботам
    // и позволит твоему коду самому забирать сообщения (режим Long Polling)
    await botClient.DeleteWebhookAsync();

    // Запускаем простую проверку обновлений
    botClient.StartReceiving(
        async (bot, update, ct) => {
            if (update.Message is not { Text: { } messageText } message) return;
            var chatId = message.Chat.Id;

            if (messageText == "/start")
                await bot.SendTextMessageAsync(chatId, "Максим лох");
            else
                await bot.SendTextMessageAsync(chatId, $"Ты написал: {messageText}");
        },
        async (bot, ex, ct) => Console.WriteLine("Ошибка: " + ex.Message)
    );

    Console.WriteLine("Бот успешно запущен!");
}

app.Run();