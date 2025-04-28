using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLMChatbotApi.DTO;

public class AgentCreateDTO
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public required string Name { get; set; }

    public string? Description { get; set; }

    [Required]
    public required AgentConfigDTO? Config { get; set; }
}