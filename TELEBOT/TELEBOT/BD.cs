using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Tele_Bot.Models
{
    public class Giveaway
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public long ChatId { get; set; } // Telegram chat ID
        public string Description { get; set; }
        public DateTime StartTime { get; set; }
    }

    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public long ChatId { get; set; } // Telegram chat ID
        public string Username { get; set; }
    }
}