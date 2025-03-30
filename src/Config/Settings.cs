namespace LLMChatbotApi.Config;

public class Settings
{
    public required string TokenPrivateKey { get; set; }
    public int TokenExpirationHours { get; set; } = 2;
}