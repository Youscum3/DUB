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
    botClient = new TelegramBotClient(token); // теперь это та же переменная, что снаружи
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

                        // Показать дополнительные товары
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
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("ПМР", "delivery_pmr") },
                        new [] { InlineKeyboardButton.WithCallbackData("Молдова", "delivery_moldova") },
                        new [] { InlineKeyboardButton.WithCallbackData("Другие страны", "delivery_other") }
                    });
                    await bot.SendTextMessageAsync(chatId, "Откуда вы?", replyMarkup: keyboard);
                }
            }
            else if (update.CallbackQuery is { Data: { } data })
            {
                var chatId = update.CallbackQuery.Message.Chat.Id;

                switch (data)
                {
                    // Прямо после category_* кейсов
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
                            // Определяем количество
                            int count = int.Parse(data.Split('_')[3]); // "price_category_roses_15" -> 15

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
                            // Запоминаем, что ждём ввод числа
                            userState[chatId] = "await_custom_quantity";
                            userFlower[chatId] = data.Contains("roses") ? "roses" :
                                                 data.Contains("tulips") ? "tulips" : "dahlias";

                            await botClient.SendTextMessageAsync(chatId, "Введите количество:");
                            break;
                        }
                    case "start_menu":
                        await ShowMainMenu(chatId);
                        break;

                    case "start_price":
                        // 💥 Добавляем сброс состояния прямо здесь
                        userState.Remove(chatId);
                        userQuantity.Remove(chatId);
                        userExtras.Remove(chatId);
                        userFlower.Remove(chatId);
                        userDate.Remove(chatId);

                        await ShowPriceMenu(chatId);
                        break;

                    case "start_order":
                        userState.Remove(chatId);
                        userQuantity.Remove(chatId);
                        userExtras.Remove(chatId);
                        userFlower.Remove(chatId);
                        userDate.Remove(chatId);

                        await ShowOrderMenu(chatId);
                        break;

                    case "start_contacts":
                        userState.Remove(chatId);
                        userQuantity.Remove(chatId);
                        userExtras.Remove(chatId);
                        userFlower.Remove(chatId);
                        userDate.Remove(chatId);

                        await ShowContacts(chatId);
                        break;

                    case "start_delivery":
                        userState.Remove(chatId);
                        userQuantity.Remove(chatId);
                        userExtras.Remove(chatId);
                        userFlower.Remove(chatId);
                        userDate.Remove(chatId);

                        await ShowDeliveryMenu(chatId);
                        break;
                    case "category_roses":
                    case "category_tulips":
                    case "category_dahlias":
                        {
                            string flowerName = data switch
                            {
                                "category_roses" => "Розы",
                                "category_tulips" => "Тюльпаны",
                                "category_dahlias" => "Георгины",
                                _ => ""
                            };

                            decimal pricePerUnit = data switch
                            {
                                "category_roses" => 8.6m,
                                "category_tulips" => 6.6m,
                                "category_dahlias" => 13m,
                                _ => 0m
                            };

                            // Создаём **новое имя переменной**, чтобы не конфликтовало
                            var priceQuantityKeyboard = new InlineKeyboardMarkup(new[]
                            {
            new [] { InlineKeyboardButton.WithCallbackData("15", $"price_{data}_15"),
                    InlineKeyboardButton.WithCallbackData("31", $"price_{data}_31"),
                    InlineKeyboardButton.WithCallbackData("51", $"price_{data}_51") },
            new [] { InlineKeyboardButton.WithCallbackData("101", $"price_{data}_101"),
                    InlineKeyboardButton.WithCallbackData("Другое количество", $"price_{data}_other") }
        });

                            await botClient.SendTextMessageAsync(chatId,
                                $"\nВыберите количество:",
                                replyMarkup: priceQuantityKeyboard);

                            break;
                        }
                    // Доставка
                    case "delivery_pmr":
                        await botClient.SendTextMessageAsync(chatId,
                        @"🚚 Способы доставки по ПМР: 

👥 Личная встреча возможна в Тирасполе и Дубоссарах

🚌 Маршруткой  
— быстрая доставка в другие города  
— оплата при получении  

📮 Почтой  
— доставка по всей стране  
— срок 2–5 дней",
                        replyMarkup: GetBackToMenuKeyboard()
                        );
                        break;



                    case "delivery_moldova":
                        await botClient.SendTextMessageAsync(chatId,
                        @"🚚 Способы доставки по Молдове:

🚌 Маршруткой  
— быстрая доставка в города  
— оплата при получении  

📦 Nova Poshta  
— доставка в отделение  
— срок 2–5 дней",
                        replyMarkup: GetBackToMenuKeyboard() // <- добавляем кнопку
                        );
                        break;


                    case "delivery_other":
                        await botClient.SendTextMessageAsync(chatId, "К сожалению, доставка только по ПМР и Молдове.",
                            replyMarkup: GetBackToMenuKeyboard()
                        );
                        break;


                    case "pmr_kamenka":
                    case "pmr_rybnica":
                    case "pmr_grigoriopol":
                    case "pmr_bendery":
                    case "pmr_slobodeya":
                    case "pmr_knopki":

                        await botClient.SendTextMessageAsync(chatId,
                    @"🚚 Способы доставки:

 🚌 Маршруткой  
 — быстрая доставка в другие города  
 — оплата при получении  

 📮 Почтой  
 — доставка по всей стране  
 — срок 2–5 дней");

                        break;
                    case "pmr_dubossary":
                    case "pmr_tiraspol":
                        await botClient.SendTextMessageAsync(chatId, "Личная встреча");
                        break;

                    // Категории цветов
                    case "order_roses":
                        userFlower[chatId] = "roses";
                        userState[chatId] = "await_quantity";

                        var quantityKeyboard = new InlineKeyboardMarkup(new[]
                        {
        new [] { InlineKeyboardButton.WithCallbackData("15", "qty_15"),
                InlineKeyboardButton.WithCallbackData("31", "qty_31"),
                InlineKeyboardButton.WithCallbackData("51", "qty_51") },
        new [] { InlineKeyboardButton.WithCallbackData("101", "qty_101"),
                InlineKeyboardButton.WithCallbackData("Другое количество", "qty_custom") }
    });

                        await botClient.SendTextMessageAsync(chatId, "Выберите количество роз:", replyMarkup: quantityKeyboard);
                        break;
                    // ==== КНОПКИ КОЛИЧЕСТВА ====
                    case "qty_15":
                    case "qty_31":
                    case "qty_51":
                    case "qty_101":
                        {
                            int quantity = int.Parse(data.Split('_')[1]);

                            userQuantity[chatId] = quantity;
                            userState[chatId] = "await_extras";

                            var extrasKeyboard = new InlineKeyboardMarkup(new[]
                            {
        new [] { InlineKeyboardButton.WithCallbackData("Блёстки", "extra_glitter"), InlineKeyboardButton.WithCallbackData("Картинка", "extra_picture") },
        new [] { InlineKeyboardButton.WithCallbackData("Игрушка", "extra_toy"), InlineKeyboardButton.WithCallbackData("Бабочки", "extra_butterfly") },
        new [] { InlineKeyboardButton.WithCallbackData("Бантики", "extra_ribbons") },
        new [] { InlineKeyboardButton.WithCallbackData("✅ Готово", "extras_done") }
    });

                            await botClient.SendTextMessageAsync(
    chatId,
    $"Вы выбрали: {quantity} шт.\n\n⚠️ ЦЕНА ДОПОЛНИТЕЛЬНЫХ УСЛУГ ОБГОВАРИВАЕТСЯ С МАСТЕРОМ\n\nВыберите дополнительные элементы:",
    replyMarkup: extrasKeyboard
);

                            break;
                        }


                    // ==== ДРУГОЕ КОЛИЧЕСТВО ====
                    case "qty_custom":
                        userState[chatId] = "await_quantity";
                        await botClient.SendTextMessageAsync(chatId, "Введите нужное количество:");
                        break;


                    case "order_tulips":
                        userFlower[chatId] = "tulips";
                        userState[chatId] = "await_quantity";

                        var quantityKeyboardTulips = new InlineKeyboardMarkup(new[]
                        {
        new [] { InlineKeyboardButton.WithCallbackData("15", "qty_15"),
                InlineKeyboardButton.WithCallbackData("31", "qty_31"),
                InlineKeyboardButton.WithCallbackData("51", "qty_51") },
        new [] { InlineKeyboardButton.WithCallbackData("101", "qty_101"),
                InlineKeyboardButton.WithCallbackData("Другое количество", "qty_custom") }
    });

                        await botClient.SendTextMessageAsync(chatId, "Выберите количество тюльпанов:", replyMarkup: quantityKeyboardTulips);
                        break;

                    case "order_dahlias":
                        userFlower[chatId] = "dahlias";
                        userState[chatId] = "await_quantity";

                        var quantityKeyboardDahlias = new InlineKeyboardMarkup(new[]
                        {
        new [] { InlineKeyboardButton.WithCallbackData("15", "qty_15"),
                InlineKeyboardButton.WithCallbackData("31", "qty_31"),
                InlineKeyboardButton.WithCallbackData("51", "qty_51") },
        new [] { InlineKeyboardButton.WithCallbackData("101", "qty_101"),
                InlineKeyboardButton.WithCallbackData("Другое количество", "qty_custom") }
    });

                        await botClient.SendTextMessageAsync(chatId, "Выберите количество георгин:", replyMarkup: quantityKeyboardDahlias);
                        break;


                    // Дополнительно
                    case "extra_glitter":
                    case "extra_picture":
                    case "extra_toy":
                    case "extra_butterfly":
                    case "extra_ribbons":
                        if (!userExtras.ContainsKey(chatId))
                            userExtras[chatId] = new List<string>();
                        var extraName = data.Substring(6); // extra_glitter -> glitter
                        if (!userExtras[chatId].Contains(extraName))
                            userExtras[chatId].Add(extraName);
                        await botClient.SendTextMessageAsync(chatId, $"Вы добавили: {extraName}");
                        break;

                    case "extras_done":
                        userState[chatId] = "await_date";
                        await ShowCalendar(chatId, DateTime.Today.Year, DateTime.Today.Month);
                        break;
                    case "confirm_yes":

                        var successKeyboard = new InlineKeyboardMarkup(new[]
                        {
        new []
        {
            InlineKeyboardButton.WithCallbackData("🏠 Меню", "start_menu"),
            InlineKeyboardButton.WithCallbackData("💬 Контакты", "start_contacts")
        }
    });

                        await botClient.SendTextMessageAsync(
                            chatId,
                            "✅ Заказ подтверждён!\nВ ближайшее время с вами свяжутся для уточнения деталей.",
                            replyMarkup: successKeyboard
                        );

                        userState.Remove(chatId);
                        userQuantity.Remove(chatId);
                        userExtras.Remove(chatId);
                        userFlower.Remove(chatId);
                        userDate.Remove(chatId);

                        break;
                    case "confirm_no":

                        var cancelKeyboard = new InlineKeyboardMarkup(new[]
                        {
        new []
        {
            InlineKeyboardButton.WithCallbackData("🏠 Меню", "start_menu")
        }
    });

                        await botClient.SendTextMessageAsync(
                            chatId,
                            "❌ Заказ отменён.",
                            replyMarkup: cancelKeyboard
                        );

                        userState.Remove(chatId);
                        userQuantity.Remove(chatId);
                        userExtras.Remove(chatId);
                        userFlower.Remove(chatId);
                        userDate.Remove(chatId);

                        break;


                    // Календарь и завершение
                    default:
                        if (data.StartsWith("date_"))
                        {
                            // НЕ объявляем chatId заново, используем существующий
                            // var chatId = update.CallbackQuery.Message.Chat.Id; // <-- убираем

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

                            // Кнопки подтверждения
                            var confirmKeyboard = new InlineKeyboardMarkup(new[]
                            {
                                new []
                             {
                                      InlineKeyboardButton.WithCallbackData("✅ Да", "confirm_yes"),
                                  InlineKeyboardButton.WithCallbackData("❌ Нет", "confirm_no")
                              }
                                });

                            await botClient.SendTextMessageAsync(chatId, "Подтвердить заказ?", replyMarkup: confirmKeyboard);

                        }
                        else if (data.StartsWith("month_"))
                        {
                            var parts = data.Substring(6).Split('-');
                            int year = int.Parse(parts[0]);
                            int month = int.Parse(parts[1]);
                            await ShowCalendar(chatId, year, month);
                        }
                        else if (data.EndsWith("_bus"))
                            await botClient.SendTextMessageAsync(chatId, "Вы выбрали доставку по маршрутке.");
                        else if (data.EndsWith("_mail"))
                            await botClient.SendTextMessageAsync(chatId, "Вы выбрали доставку по почте.");
                        else if (data == "moldova_nova")
                            await botClient.SendTextMessageAsync(chatId, "Вы выбрали доставку через Nova Poshta.");
                        else if (data == "moldova_bus")
                            await botClient.SendTextMessageAsync(chatId, "Вы выбрали доставку по маршрутке.");
                        break;
                }

                await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
            }
        },
        async (bot, ex, ct) => Console.WriteLine(ex.Message)
    );

// ==== МЕНЮ ====
// ==== МЕНЮ ====

async Task ShowMainMenu(long chatId)
{
    userState.Remove(chatId);
    userQuantity.Remove(chatId);
    userExtras.Remove(chatId);
    userFlower.Remove(chatId);
    userDate.Remove(chatId);

    var mainKeyboard = new InlineKeyboardMarkup(new[]
    {
        new [] { InlineKeyboardButton.WithCallbackData("Цены", "start_price") },
        new [] { InlineKeyboardButton.WithCallbackData("Доставка", "start_delivery") },
        new [] { InlineKeyboardButton.WithCallbackData("Контакты", "start_contacts") },
        new [] { InlineKeyboardButton.WithCallbackData("Сделать заказ", "start_order") }
    });
    await botClient.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: mainKeyboard);
}

async Task ShowPriceMenu(long chatId)
{
    userState.Remove(chatId);
    userQuantity.Remove(chatId);
    userExtras.Remove(chatId);
    userFlower.Remove(chatId);
    userDate.Remove(chatId);

    var keyboard = new InlineKeyboardMarkup(new[]
    {
    new [] { InlineKeyboardButton.WithCallbackData("🌹🌹 Розы 🌹🌹", "category_roses") },
    new [] { InlineKeyboardButton.WithCallbackData("🌷🌷 Тюльпаны 🌷🌷", "category_tulips") },
    new [] { InlineKeyboardButton.WithCallbackData("🌺🌺 Георгины 🌺🌺", "category_dahlias") }
    });
    await botClient.SendTextMessageAsync(chatId, "Выберите категорию:", replyMarkup: keyboard);
}

async Task ShowOrderMenu(long chatId)
{
    var keyboard = new InlineKeyboardMarkup(new[]
    {
    new [] { InlineKeyboardButton.WithCallbackData("🌹🌹 Розы 🌹🌹", "category_roses") },
    new [] { InlineKeyboardButton.WithCallbackData("🌷🌷 Тюльпаны 🌷🌷", "category_tulips") },
    new [] { InlineKeyboardButton.WithCallbackData("🌺🌺 Георгины 🌺🌺", "category_dahlias") }
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

// ==== Исправленный ShowDeliveryMenu ====
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


Console.WriteLine("Бот запущен!");
app.Run();
