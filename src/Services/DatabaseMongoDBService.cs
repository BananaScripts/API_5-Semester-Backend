using LLMChatbotApi.exceptions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LLMChatbotApi.Services;

public class DatabaseMongoDBService : DatabaseServiceBase<DatabaseMongoDBService>
{
    private readonly IMongoDatabase mongoDB;

    public DatabaseMongoDBService(IConfiguration configuration, ILogger<DatabaseMongoDBService> logger)
        : base(logger)
    {
        var mongoConnectionString = configuration.GetConnectionString("MongoDB")
            ?? throw new ArgumentNullException("Connection string 'MongoDB' not found.");
        var mongoDatabaseName = configuration["ConnectionStrings:MongoDatabase"]
            ?? throw new ArgumentNullException("Mongo database name not found.");

        var client = new MongoClient(mongoConnectionString);
        mongoDB = client.GetDatabase(mongoDatabaseName);
    }

    public IMongoDatabase GetDatabase() => mongoDB;

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
}
