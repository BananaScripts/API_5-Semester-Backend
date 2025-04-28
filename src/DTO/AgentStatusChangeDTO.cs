using System.ComponentModel.DataAnnotations;
using LLMChatbotApi.Enums;

namespace LLMChatbotApi.DTO;

public class AgentStatusChangeDTO
{
    [Required]
    [EnumDataType(typeof(AgentStatus))]
    public AgentStatus NewStatus { get; set; }
}

