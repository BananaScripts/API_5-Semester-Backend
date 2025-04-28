using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LLMChatbotApi.Enums;

namespace LLMChatbotApi.DTO;

public class AgentUpdateDTO
{
    [Column("agent_name")]
    [StringLength(100, MinimumLength = 3)]
    public string? Name { get; set; }

    [Column("agent_description")]
    public string? Description { get; set; }

    [Column("agent_config")]
    public AgentConfigDTO? Config { get; set; }

    [Column("agent_status")]
    [EnumDataType(typeof(AgentStatus))]
    public AgentStatus? Status { get; set; }
}