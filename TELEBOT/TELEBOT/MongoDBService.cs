using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tele_Bot.Models;

namespace Tele_Bot.Services
{
    public class MongoService
    {
        private readonly IMongoCollection<Giveaway> _giveaways;
        private readonly IMongoCollection<User> _users;

        public MongoService()
        {
            var client = new MongoClient("your-mongodb-connection-string");
            var database = client.GetDatabase("TelegramBotDB");

            _giveaways = database.GetCollection<Giveaway>("Giveaways");
            _users = database.GetCollection<User>("Users");
        }

        public async Task CreateGiveawayAsync(long chatId, string description, DateTime startTime)
        {
            var giveaway = new Giveaway
            {
                ChatId = chatId,
                Description = description,
                StartTime = startTime
            };

            await _giveaways.InsertOneAsync(giveaway);
        }

        public async Task<Giveaway> GetGiveawayByChatIdAsync(long chatId)
        {
            return await _giveaways.Find(g => g.ChatId == chatId).FirstOrDefaultAsync();
        }

        public async Task UpdateGiveawayAsync(long chatId, string newDescription)
        {
            var update = Builders<Giveaway>.Update.Set(g => g.Description, newDescription);
            await _giveaways.UpdateOneAsync(g => g.ChatId == chatId, update);
        }

        public async Task DeleteGiveawayAsync(long chatId)
        {
            await _giveaways.DeleteOneAsync(g => g.ChatId == chatId);
        }

        public async Task<List<Giveaway>> GetAllGiveawaysAsync()
        {
            return await _giveaways.Find(g => true).ToListAsync();
        }
    }
}
