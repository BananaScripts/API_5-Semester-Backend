using LLMChatbotApi.Enums;

namespace LLMChatbotApi.DTO;

public class AgentResponseDTO
{
    public int AgentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AgentConfigDTO? Config { get; set; } = new();
    public AgentStatus Status { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}