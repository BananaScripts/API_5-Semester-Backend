using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LLMChatbotApi.Enums;
using LLMChatbotApi.Models;

namespace LLMChatbotApi.DTO;

public class WSRequestDTO
{
    [Required]
    [Column("chatId")]
    public string? ChatId { get; set; }

    [Required]
    [Column("userId")]
    public string? UserId { get; set; }

    [Required]
    [Column("agentId")]
    public string? AgentId { get; set; }
    [Required]
    [Column("text")]
    public string? Text { get; set; }

}