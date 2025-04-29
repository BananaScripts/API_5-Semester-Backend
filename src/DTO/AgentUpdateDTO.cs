using System.ComponentModel.DataAnnotations;
using LLMChatbotApi.Enums;

namespace LLMChatbotApi.DTO;

public class AgentUpdateDTO
{
    [StringLength(100, MinimumLength = 3)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public AgentConfigDTO? Config { get; set; }

    [EnumDataType(typeof(AgentStatus))]
    public AgentStatus? Status { get; set; }
}