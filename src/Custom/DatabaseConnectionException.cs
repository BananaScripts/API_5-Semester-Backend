namespace LLMChatbotApi.exceptions;

public class DatabaseConnectionException: Exception
{
    public DatabaseConnectionException(string message, Exception inner)
        : base(message, inner) {}
}