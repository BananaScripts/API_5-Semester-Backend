using LLMChatbotApi.exceptions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LLMChatbotApi.Services;

public class DatabaseMongoDBService
{
    private readonly IMongoDatabase mongoDB;
    private readonly ILogger<DatabaseMongoDBService> mongoLogger;

    public DatabaseMongoDBService (IMongoDatabase mongo, ILogger<DatabaseMongoDBService> logger)
    {
        mongoDB = mongo;
        mongoLogger = logger;
    }

    public async Task VerifyNoSQLCOnnection()
    {
        try
        {
            var pingCommando = new BsonDocument("ping", 1);
            await mongoDB.RunCommandAsync<BsonDocument>(pingCommando);

            mongoLogger.LogInformation("Conexão MongoDB estabelecida");

        }
        catch (MongoException ex)
        {
            mongoLogger.LogError(ex,"Falha na conexão MongoDB: {Message}", ex.Message);
            throw new DatabaseConnectionException("Erro MongoDB", ex);
        }
    }
};