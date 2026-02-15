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

// Словарь для хранения текущего шага пользователя
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
                var text = messageText.Trim(); // Убираем лишние пробелы

                // 1. КОМАНДА /START
                if (text == "/start")
                {
                    userState[chatId] = "choose_flower"; // Устанавливаем состояние ожидания выбора

                    var flowers = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { "🌹 Розы", "🌷 Тюльпаны", "🌼 Георгины" }
                    })
                    { ResizeKeyboard = true, OneTimeKeyboard = true };

                    await bot.SendTextMessageAsync(chatId, "Выберите букет из меню:", replyMarkup: flowers);
                    return;
                }

                // 2. ОБРАБОТКА ВЫБОРА КАТЕГОРИИ
                if (userState.TryGetValue(chatId, out var state) && state == "choose_flower")
                {
                    if (text.Contains("Розы")) userState[chatId] = "roses_count";
                    else if (text.Contains("Тюльпаны")) userState[chatId] = "tulips_count";
                    else if (text.Contains("Георгины")) userState[chatId] = "dahlias_count";
                    else
                    {
                        await bot.SendTextMessageAsync(chatId, "Пожалуйста, выберите цветок, нажав на кнопку.");
                        return;
                    }

                    await bot.SendTextMessageAsync(chatId, $"Вы выбрали {text}. Введите количество штук (числом):",
                        replyMarkup: new ReplyKeyboardRemove()); // Убираем кнопки
                    return;
                }

                // 3. ОБРАБОТКА ВВОДА КОЛИЧЕСТВА И РАСЧЕТ
                if (userState.TryGetValue(chatId, out var currentState) && currentState.EndsWith("_count"))
                {
                    if (!int.TryParse(text, out int count) || count <= 0)
                    {
                        await bot.SendTextMessageAsync(chatId, "Ошибка! Введите целое число больше нуля.");
                        return;
                    }

                    decimal pricePerOne = currentState switch
                    {
                        "roses_count" => 8.6m,
                        "tulips_count" => 6.6m,
                        "dahlias_count" => 13m,
                        _ => 0
                    };

                    decimal total = count * pricePerOne;

                    // Очищаем состояние после расчета
                    userState.Remove(chatId);

                    await bot.SendTextMessageAsync(chatId,
                        $"✅ Расчет готов!\nКоличество: {count} шт.\nИтоговая цена: {total:F2} ₽\n\nДля нового заказа введите /start");
                }
            }
        },
        async (bot, ex, ct) => Console.WriteLine("Ошибка: " + ex.Message)
    );

    Console.WriteLine("Бот успешно запущен!");
}

app.Run();