using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LLMChatbotApi.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LLMChatbotApi.Models;
public class Message
{
    [BsonElement("sender")]
    public required string Sender { get; set; }

    [BsonElement("text")]
    public string Text { get; set; } = "";

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
