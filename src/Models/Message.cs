using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LLMChatbotApi.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LLMChatbotApi.Models;
public class Message
{
	public Message(string _sender, string _text, DateTime _timestamp)
	{
		Sender = _sender;
        Text = _text;
        Timestamp = _timestamp;
	}

    [BsonElement("sender")]
    public string? Sender { get; set; }

    [BsonElement("text")]
    public string Text { get; set; } = "";

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
