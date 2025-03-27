using LLMChatbotApi.exceptions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LLMChatbotApi.Services;

public class DatabaseMongoDBService : DatabaseServiceBase<DatabaseMongoDBService>
{
    private readonly IMongoDatabase mongoDB;

   public DatabaseMongoDBService(IMongoDatabase mongo, ILogger<DatabaseMongoDBService> logger)
        : base(logger)
    {
        mongoDB = mongo;
    }
    public override async Task VerifyConnection()
    {
        try
        {
            var pingCommand = new BsonDocument("ping", 1);
            await mongoDB.RunCommandAsync<BsonDocument>(pingCommand);
            Logger.LogInformation("Conexão MongoDB estabelecida");
        }
        catch (MongoException ex)
        {
            Logger.LogError(ex, "Falha na conexão MongoDB: {Message}", ex.Message);
            throw new DatabaseConnectionException("Erro MongoDB", ex);
        }
    }
};