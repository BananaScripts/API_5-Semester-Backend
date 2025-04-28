using System.ComponentModel.DataAnnotations;

namespace LLMChatbotApi.DTO;

public class AgentConfigDTO
{
    [Required]
    public string SystemPrompt { get; set; } = string.Empty;

    [Required]
    public string Model { get; set; } = "deepseek/deepseek-v3-base:free";

    public List<string> AllowedFileTypes { get; set; } = new();

    public Dictionary<string, object>? ModelParameters { get; set; }
}