using LLMChatbotApi.exceptions;
using StackExchange.Redis;

namespace LLMChatbotApi.Services;

public class DatabaseRedisService: DatabaseServiceBase<DatabaseRedisService>
{
    private readonly IConnectionMultiplexer  redisconnection;

    public DatabaseRedisService(string connection, ILogger<DatabaseRedisService> logger )
        :base(logger)
    {
        try
        {
            redisconnection = ConnectionMultiplexer.Connect(connection);
        }
        catch (RedisConnectionException ex)
        {
            Logger.LogError(ex, "Falha na conexão com Redis");
            throw new DatabaseConnectionException("Erro Redis", ex);
        }
    }

    public override async Task VerifyConnection()
    {
        try
        {
            var db = redisconnection.GetDatabase();
            await db.PingAsync();
            Logger.LogInformation("Conexão Redis Estabelecida");
        }
        catch (RedisConnectionException ex)
        {
            Logger.LogError(ex, "Falha na conexão Redis: {Message}", ex.Message);
            throw;
        }
    }

    public IDatabase GetDatabase() => redisconnection.GetDatabase();
};