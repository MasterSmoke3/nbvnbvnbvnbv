using Amazon.Runtime.Internal.Auth;
using Amazon.SecurityToken;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Tele_Bot.Services;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.ReplyMarkups;
using Timer = System.Timers.Timer;

namespace Tele_Bot
{
    class Program
    {
        private static string token { get; set; } = "7055767364:AAHbA2neU5q1BCRyzZ02iz_-XhRGcm2RFfw";
        private static TelegramBotClient client;

        // Хранение розыгрышей: ключ — ID чата (админ), значение — список розыгрышей
        private static Dictionary<long, List<(string Title, DateTime Time, bool HasStarted, string Description, string ImageUrl)>> giveaways = new Dictionary<long, List<(string, DateTime, bool, string, string)>>();
        // Хранение участников розыгрышей: ключ — название розыгрыша, значение — список участников (ID пользователей)
        private static Dictionary<string, List<long>> participants = new Dictionary<string, List<long>>();
        //private static MongoService? conection = new MongoService();

        // Хранение состояний для создания/редактирования розыгрышей
        private static Dictionary<long, string> userStates = new Dictionary<long, string>();

        private static Timer giveawayTimer;
        static async Task Main(string[] args)
        {
            client = new TelegramBotClient(token);

            var me = await client.GetMeAsync();
            Console.WriteLine($"Запущен бот {me.FirstName}");

            // Устанавливаем обработчик сообщений
            client.OnMessage += OnMessageHandler;

            // Запускаем получение обновлений
            client.StartReceiving();
            Console.WriteLine("Бот ждет...");

            // Чтобы бот продолжал работать
            Console.ReadLine();

            // Останавливаем получение обновлений при завершении программы
            client.StopReceiving();
        }
private static async void CheckGiveawayTimes(object sender, ElapsedEventArgs e)
        {
            var now = DateTime.UtcNow;
            var moscowTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
            var moscowNow = TimeZoneInfo.ConvertTimeFromUtc(now, moscowTimeZone);

            foreach (var adminGiveaways in giveaways.ToList())
            {
                for (int i = 0; i < adminGiveaways.Value.Count; i++)
                {
                    var giveaway = adminGiveaways.Value[i];

                    // Проверяем, стартовал ли розыгрыш
                    if (!giveaway.HasStarted && giveaway.Time <= moscowNow)
                    {
                        adminGiveaways.Value[i] = (giveaway.Title, giveaway.Time, true, giveaway.Description, giveaway.ImageUrl); // Помечаем розыгрыш как стартовавший

                        await client.SendTextMessageAsync(adminGiveaways.Key, $"Розыгрыш '{giveaway.Title}' стартовал!\nОписание: {giveaway.Description}\nИзображение: {giveaway.ImageUrl}");

                        // Отправляем уведомление участникам
                        if (participants.ContainsKey(giveaway.Title) && participants[giveaway.Title].Count > 0)
                        {
                            foreach (var participantId in participants[giveaway.Title])
                            {
                                await client.SendTextMessageAsync(participantId, $"Розыгрыш '{giveaway.Title}' стартовал!\nОписание: {giveaway.Description}\nИзображение: {giveaway.ImageUrl}");
                            }
                        }
                    }

                    // Проверка окончания розыгрыша
                    if (giveaway.HasStarted && giveaway.Time <= moscowNow)
                    {
                        var winner = await PickRandomWinner(giveaway.Title);
                        if (winner != null)
                        {
                            await client.SendTextMessageAsync(adminGiveaways.Key, $"Розыгрыш '{giveaway.Title}' завершен! Победитель: {winner}");
                        }
                        else
                        {
                            await client.SendTextMessageAsync(adminGiveaways.Key, $"Розыгрыш '{giveaway.Title}' завершен! Нет участников.");
                        }

                        // Удаляем завершенный розыгрыш
                        adminGiveaways.Value.RemoveAt(i);
                        participants.Remove(giveaway.Title);
                        i--;
                    }
                }
            }
        }


        private static async System.Threading.Tasks.Task<string> PickRandomWinner(string giveawayTitle)
        {
            if (participants.ContainsKey(giveawayTitle) && participants[giveawayTitle].Count > 0)
            {
                Random rand = new Random();
                int winnerIndex = rand.Next(participants[giveawayTitle].Count);
                long winnerId = participants[giveawayTitle][winnerIndex];

                var winner = await client.GetChatAsync(winnerId);
                string winnerName = !string.IsNullOrEmpty(winner.Username) ? $"@{winner.Username}" : winner.FirstName;

                return winnerName;
            }
            return null;
        }

        private static async void OnMessageHandler(object? sender, MessageEventArgs e)
        {
            var msg = e.Message;
            if (msg.Text != null)
            {
                Console.WriteLine($"Пришло сообщение: {msg.Text}");

                if (userStates.ContainsKey(msg.Chat.Id))
                {
                    await HandleUserState(msg);
                    return;
                }

                switch (msg.Text)
                {
                    // Команды администратора
                    case "/admin":
                        await client.SendTextMessageAsync(msg.Chat.Id, "Выберите действие:", replyMarkup: GetAdminButtons());
                        break;

                    case "Создать розыгрыш":
                        userStates[msg.Chat.Id] = "create_title";
                        await client.SendTextMessageAsync(msg.Chat.Id, "Введите название розыгрыша.", replyMarkup: new ReplyKeyboardRemove());
                        break;

                    case "Редактирование/удаление":
                        await ShowGiveawaysForAdmin(msg.Chat.Id);
                        break;

                    case "Показать розыгрыши":
                        await ShowActiveGiveaways(msg.Chat.Id);
                        break;

                    // Команды пользователя
                    case "/user":
                        await ShowGiveawaysToUser(msg.Chat.Id);
                        break;

                    case "Отменить участие":
                        await CancelParticipation(msg.Chat.Id);
                        break;

                    default:
                        await client.SendTextMessageAsync(msg.Chat.Id, "Неизвестная команда. Введите /admin ");
                        break;
                }
            }
            else if (msg.Photo != null) // Если было отправлено фото
            {
                await HandlePhotoUpload(msg);
            }
        }
        private static async System.Threading.Tasks.Task HandlePhotoUpload(Telegram.Bot.Types.Message msg)
        {
            // Получаем изображение
            if (userStates.ContainsKey(msg.Chat.Id) && userStates[msg.Chat.Id] == "upload_image")
            {
                var file = await client.GetFileAsync(msg.Photo.Last().FileId);
                string imageUrl = file.FilePath; // URL изображения
                var currentGiveaway = giveaways[msg.Chat.Id].Last();

                // Обновляем запись розыгрыша с URL изображения
                giveaways[msg.Chat.Id][giveaways[msg.Chat.Id].Count - 1] = (currentGiveaway.Title, currentGiveaway.Time, currentGiveaway.HasStarted, currentGiveaway.Description, imageUrl);

                userStates[msg.Chat.Id] = "create_description"; // Переход к описанию
                await client.SendTextMessageAsync(msg.Chat.Id, "Введите описание розыгрыша.");
            }
        }

        private static async System.Threading.Tasks.Task ShowGiveawaysForAdmin(long adminId)
        {
            var moscowTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
            if (giveaways.ContainsKey(adminId) && giveaways[adminId].Count > 0)
            {
                string giveawayList = "Ваши розыгрыши:\n";
                foreach (var giveaway in giveaways[adminId])
                {
                    // Конвертируем время в московское время
                    var moscowTime = TimeZoneInfo.ConvertTimeFromUtc(giveaway.Time, moscowTimeZone);
                    giveawayList += $"{giveaway.Title} - Время начала: {moscowTime} (по МСК)\n";
                }
                giveawayList += "\nРедактирование/удаление.";

                userStates[adminId] = "edit_giveaway";
                await client.SendTextMessageAsync(adminId, giveawayList);
            }
            else
            {
                await client.SendTextMessageAsync(adminId, "нет активных розыгрышей.");
            }
        }

        private static async System.Threading.Tasks.Task ShowActiveGiveaways(long adminId)
        {
            if (giveaways.ContainsKey(adminId) && giveaways[adminId].Count > 0)
            {
                string giveawayList = "Ваши активные розыгрыши:\n";
                foreach (var giveaway in giveaways[adminId])
                {
                    giveawayList += $"{giveaway.Title} - Время начала: {giveaway.Time}\n";
                }
                await client.SendTextMessageAsync(adminId, giveawayList);
            }
            else
            {
                await client.SendTextMessageAsync(adminId, "нет активных розыгрышей.");
            }
        }

        private static async System.Threading.Tasks.Task HandleUserState(Telegram.Bot.Types.Message msg)
        {
            var moscowTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");

            switch (userStates[msg.Chat.Id])
            {
                case "create_title":
                    if (!giveaways.ContainsKey(msg.Chat.Id))
                    {
                        giveaways[msg.Chat.Id] = new List<(string, DateTime, bool, string, string)>();
                    }

                    giveaways[msg.Chat.Id].Add((msg.Text, DateTime.MinValue, false, string.Empty, string.Empty)); 
                    userStates[msg.Chat.Id] = "upload_image"; // Переход к загрузке изображения
                    await client.SendTextMessageAsync(msg.Chat.Id, "Загрузите изображение для розыгрыша.");
                    break;

                case "upload_image":
                    await HandlePhotoUpload(msg);
                    break;

                case "create_description":
                    var currentGiveaway = giveaways[msg.Chat.Id].Last();
                    giveaways[msg.Chat.Id][giveaways[msg.Chat.Id].Count - 1] = (currentGiveaway.Title, currentGiveaway.Time, currentGiveaway.HasStarted, msg.Text, currentGiveaway.ImageUrl);
                    userStates[msg.Chat.Id] = "create_time"; // Переход к запросу времени
                    await client.SendTextMessageAsync(msg.Chat.Id, "Введите время начала розыгрыша (например, 2024-10-10 18:00).");
                    break;

                case "create_time":
                    if (giveaways.ContainsKey(msg.Chat.Id) && giveaways[msg.Chat.Id].LastOrDefault().Title != null)
                    {
                        if (DateTime.TryParse(msg.Text, out DateTime time))
                        {
                            // Конвертируем введенное время в UTC
                            DateTime utcTime = TimeZoneInfo.ConvertTimeToUtc(time, moscowTimeZone);
                            if (utcTime > DateTime.UtcNow)
                            {
                                var index = giveaways[msg.Chat.Id].Count - 1;
                                giveaways[msg.Chat.Id][index] = (giveaways[msg.Chat.Id][index].Title, utcTime, false, giveaways[msg.Chat.Id][index].Description, giveaways[msg.Chat.Id][index].ImageUrl); // Обновляем запись

                                // Создаем новый таймер для данного розыгрыша
                                var giveawayTimer = new Timer((utcTime - DateTime.UtcNow).TotalMilliseconds);
                                giveawayTimer.Elapsed += async (sender, e) => await StartGiveaway(msg.Chat.Id, giveaways[msg.Chat.Id][index]);
                                giveawayTimer.AutoReset = false;
                                giveawayTimer.Start();

                                participants[giveaways[msg.Chat.Id][index].Title] = new List<long>(); // Инициализируем список участников
                                var moscowTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, moscowTimeZone); // Для вывода по МСК
                                await client.SendTextMessageAsync(msg.Chat.Id, $"Розыгрыш '{giveaways[msg.Chat.Id][index].Title}' создан!\nВремя начала: {moscowTime} (по МСК)");
                                userStates.Remove(msg.Chat.Id);
                            }
                            else
                            {
                                await client.SendTextMessageAsync(msg.Chat.Id, "Введите время начала розыгрыша заново.");
                            }
                        }
                        else
                        {
                            await client.SendTextMessageAsync(msg.Chat.Id, "Неверный формат времени. Используйте формат: ГГГГ-ММ-ДД ЧЧ:ММ.");
                        }
                    }
                    break;
                case "edit_giveaway":
                    if (giveaways[msg.Chat.Id].Any(g => g.Title == msg.Text))
                    {
                        userStates[msg.Chat.Id] = "choose_edit_option";
                        userStates[msg.Chat.Id + 1000] = msg.Text; // Сохраняем название розыгрыша для редактирования
                        await client.SendTextMessageAsync(msg.Chat.Id, "Выберите действие для розыгрыша: удалить/редактировать.", replyMarkup: GetEditButtons());
                    }
                    else
                    {
                        await client.SendTextMessageAsync(msg.Chat.Id, "Розыгрыш не найден.");
                    }
                    break;
                case "choose_giveaway":
                    if (giveaways.Count > 0)
                    {
                        var foundGiveaway = giveaways.Values.SelectMany(g => g).FirstOrDefault(g => g.Title == msg.Text);
                        if (foundGiveaway.Title != null)
                        {
                            if (!participants.ContainsKey(foundGiveaway.Title))
                            {
                                participants[foundGiveaway.Title] = new List<long>();
                            }

                            // Добавляем пользователя в список участников
                            participants[foundGiveaway.Title].Add(msg.Chat.Id);
                            await client.SendTextMessageAsync(msg.Chat.Id, $"Вы успешно записаны на розыгрыш '{foundGiveaway.Title}'.");
                            userStates.Remove(msg.Chat.Id);
                        }
                        else
                        {
                            await client.SendTextMessageAsync(msg.Chat.Id, "Розыгрыш с таким названием не найден. Введите корректное название.");
                        }
                    }
                    else
                    {
                        await client.SendTextMessageAsync(msg.Chat.Id, "Нет активных розыгрышей.");
                    }
                    break;

                case "choose_edit_option":
                    string giveawayTitle = userStates[msg.Chat.Id + 1000]; // Достаем название розыгрыша
                    if (msg.Text == "Удалить")
                    {
                        giveaways[msg.Chat.Id].RemoveAll(g => g.Title == giveawayTitle);
                        participants.Remove(giveawayTitle);
                        await client.SendTextMessageAsync(msg.Chat.Id, $"Розыгрыш '{giveawayTitle}' был удален.");
                        userStates.Remove(msg.Chat.Id);
                        userStates.Remove(msg.Chat.Id + 1000);
                    }
                    else if (msg.Text == "Редактировать")
                    {
                        userStates[msg.Chat.Id] = "edit_time";
                        await client.SendTextMessageAsync(msg.Chat.Id, "Введите новое время начала розыгрыша (например, 2024-10-10 18:00).");
                    }
                    break;

                case "edit_time":
                    giveawayTitle = userStates[msg.Chat.Id + 1000];
                    if (DateTime.TryParse(msg.Text, out DateTime newTime))
                    {
                        DateTime newUtcTime = TimeZoneInfo.ConvertTimeToUtc(newTime, moscowTimeZone);
                        if (newUtcTime > DateTime.UtcNow)
                        {
                            var index = giveaways[msg.Chat.Id].FindIndex(g => g.Title == giveawayTitle);
                            if (index != -1)
                            {
                                var (Title, Time, HasStarted, Description, ImageUrl) = giveaways[msg.Chat.Id][index];
                                giveaways[msg.Chat.Id][index] = (Title, newUtcTime, HasStarted, Description, ImageUrl);

                                await client.SendTextMessageAsync(msg.Chat.Id, $"Время розыгрыша '{giveawayTitle}' было изменено.");
                                userStates.Remove(msg.Chat.Id);
                                userStates.Remove(msg.Chat.Id + 1000);
                            }
                            else
                            {
                                await client.SendTextMessageAsync(msg.Chat.Id, $"Розыгрыш '{giveawayTitle}' не найден.");
                            }
                        }
                        else
                        {
                            await client.SendTextMessageAsync(msg.Chat.Id, "Время должно быть в будущем. Пожалуйста, введите время заново.");
                        }
                    }
                    else
                    {
                        await client.SendTextMessageAsync(msg.Chat.Id, "Неверный формат времени. Пожалуйста, используйте формат: ГГГГ-ММ-ДД ЧЧ:ММ.");
                    }
                    break;

                default:
                    await client.SendTextMessageAsync(msg.Chat.Id, "Произошла ошибка.");
                    userStates.Remove(msg.Chat.Id);
                    break;
            }
        }

        private static async System.Threading.Tasks.Task StartGiveaway(long adminId, (string Title, DateTime Time, bool HasStarted, string Description, string ImageUrl) giveaway)
        {
            // Обновляем статус розыгрыша
            giveaway.HasStarted = true;

            await client.SendTextMessageAsync(adminId, $"Розыгрыш '{giveaway.Title}' стартовал!\nОписание: {giveaway.Description}\nИзображение: {giveaway.ImageUrl}");

            // Отправляем уведомление участникам
            if (participants.ContainsKey(giveaway.Title) && participants[giveaway.Title].Count > 0)
            {
                foreach (var participantId in participants[giveaway.Title])
                {
                    await client.SendTextMessageAsync(participantId, $"Розыгрыш '{giveaway.Title}' стартовал!\nОписание: {giveaway.Description}\nИзображение: {giveaway.ImageUrl}");
                }
            }

            
            var finishTimer = new Timer(10000); 
            finishTimer.Elapsed += async (sender, e) => await EndGiveaway(adminId, giveaway.Title);
            finishTimer.AutoReset = false;
            finishTimer.Start();
        }

        private static async System.Threading.Tasks.Task EndGiveaway(long adminId, string giveawayTitle)
        {
            var winner = await PickRandomWinner(giveawayTitle);
            if (winner != null)
            {
                await client.SendTextMessageAsync(adminId, $"Розыгрыш '{giveawayTitle}' завершен! Победитель: {winner}");
            }
            else
            {
                await client.SendTextMessageAsync(adminId, $"Розыгрыш '{giveawayTitle}' завершен! Нет участников.");
            }

            // Удаляем завершенный розыгрыш
            var giveaway = giveaways[adminId].FirstOrDefault(g => g.Title == giveawayTitle);
            giveaways[adminId].Remove(giveaway);
            participants.Remove(giveawayTitle);
        }
        private static async System.Threading.Tasks.Task ShowGiveawaysToUser(long userId)
        {
            if (giveaways.Count > 0)
            {
                var moscowTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
                string giveawayList = "Доступные розыгрыши:\n";
                foreach (var adminGiveaways in giveaways)
                {
                    foreach (var giveaway in adminGiveaways.Value)
                    {
                        int participantCount = participants.ContainsKey(giveaway.Title) ? participants[giveaway.Title].Count : 0;
                        // Конвертируем время розыгрыша в московское время
                        var moscowTime = TimeZoneInfo.ConvertTimeFromUtc(giveaway.Time, moscowTimeZone);
                        giveawayList += $"{giveaway.Title} - Время начала: {moscowTime} (по МСК) - Участников: {participantCount}\n";
                    }
                }
                giveawayList += "\nВведите название розыгрыша для участия или введите 'Отменить участие', если вы хотите выйти из текущего розыгрыша.";
                userStates[userId] = "choose_giveaway";
                await client.SendTextMessageAsync(userId, giveawayList);
            }
            else
            {
                await client.SendTextMessageAsync(userId, "Нет активных розыгрышей.");
            }
        }

        private static async System.Threading.Tasks.Task CancelParticipation(long userId)
        {
            foreach (var giveaway in participants)
            {
                if (giveaway.Value.Contains(userId))
                {
                    giveaway.Value.Remove(userId);
                    await client.SendTextMessageAsync(userId, "Вы успешно отменили участие в розыгрыше.");
                    return;
                }
            }
            await client.SendTextMessageAsync(userId, "Вы не участвуете в ни одном розыгрыше.");
        }

        private static IReplyMarkup GetAdminButtons()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Создать розыгрыш", "Выбрать розыгрыш для редактирования/удаления" },
                new KeyboardButton[] { "Показать розыгрыши" }
            })
            {
                ResizeKeyboard = true
            };
        }
private static IReplyMarkup GetEditButtons()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Редактировать", "Удалить" }
            })
            {
                ResizeKeyboard = true
            };
        }
    }
}