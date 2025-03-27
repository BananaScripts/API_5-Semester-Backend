using LLMChatbotApi.exceptions;
using MySqlConnector;

namespace LLMChatbotApi.Services;

public class DatabaseMySQLService : DatabaseServiceBase<DatabaseMySQLService>
{
    private readonly MySqlConnection sqlconnection;

    public DatabaseMySQLService(MySqlConnection connection, ILogger<DatabaseMySQLService> logger)
        :base(logger)
    {
        sqlconnection = connection;
    }

    public MySqlConnection GetConnection() => sqlconnection;

    public override async Task VerifyConnection()
    {
        try
        {
            await sqlconnection.OpenAsync();
            Logger.LogInformation("Conexão MySQL estabelecida");
        }
        catch (MySqlException ex)
        {
            Logger.LogError(ex, "Falha na conexão MySQL: {ErrorCode} {ErorMessage}",ex.Number ,ex.Message);
            throw new DatabaseConnectionException("Erro Mysql", ex);
        }
    }

};