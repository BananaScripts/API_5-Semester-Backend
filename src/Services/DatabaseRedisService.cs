using LLMChatbotApi.exceptions;
using StackExchange.Redis;

namespace LLMChatbotApi.Services;

public class DatabaseRedisService
{
    private readonly IConnectionMultiplexer  redisconnection;
    private readonly ILogger<DatabaseRedisService> redisLogger;
    public DatabaseRedisService(string connection, ILogger<DatabaseRedisService> logger )
    {
        redisLogger = logger;
        try
        {
            redisconnection = ConnectionMultiplexer.Connect(connection);
        }
        catch (RedisConnectionException ex)
        {
            redisLogger.LogError(ex, "Falha na conexão com Redis");
            throw new DatabaseConnectionException("Erro Redis", ex);
        }
    }

    public async Task VerifyConnectionRedis()
    {
        try
        {
            var db = redisconnection.GetDatabase();
            await db.PingAsync();
            redisLogger.LogInformation("Conexão Redis Estabelecida");
        }
        catch (RedisConnectionException ex)
        {
            redisLogger.LogError(ex, "Falha na conexão Redis: {Message}", ex.Message);
            throw;
        }
    }

    public IDatabase GetDatabase() => redisconnection.GetDatabase();
}