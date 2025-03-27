using LLMChatbotApi.Interfaces;

public abstract class DatabaseServiceBase<T> : IDatabaseService
{
    protected readonly ILogger<T> Logger;

    protected DatabaseServiceBase(ILogger<T> logger)
    {
        Logger = logger;
    }

    public abstract Task VerifyConnection();
}
