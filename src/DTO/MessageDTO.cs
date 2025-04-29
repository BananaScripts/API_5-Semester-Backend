using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LLMChatbotApi.Enums;
using LLMChatbotApi.Models;

namespace LLMChatbotApi.DTO;

public class MessageDTO
{
    [Required]
    [Column("chat_id")]
    public string? chatId { get; set; }

    [Required]
    [Column("user_id")]
    public string? userId { get; set; }

    [Required]
    [Column("agent_id")]
    public string? agentId { get; set; }
}