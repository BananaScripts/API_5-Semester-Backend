using System.ComponentModel.DataAnnotations;

namespace LLMChatbotApi.DTO;

public class AgentConfigDTO
{
    [Required]
    public string SystemPrompt { get; set; } = string.Empty;

    [Required]
    public string Model { get; set; } = "models/gemini-1.5-flash-latest"; // Modelo Gemini

    public List<string> AllowedFileTypes { get; set; } = new();

    public Dictionary<string, object> ModelParameters { get; set; } = new()
    {
        {"temperature", 0.9},
        {"top_p", 1},
        {"top_k", 40}
    };
}