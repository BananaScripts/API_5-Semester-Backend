using LLMChatbotApi.exceptions;
using MySqlConnector;

namespace LLMChatbotApi.Services;

public class DatabaseMySQLService
{
    private readonly MySqlConnection sqlconnection;
    private readonly ILogger<DatabaseMySQLService> mysqllogger;

    public DatabaseMySQLService(MySqlConnection connection, ILogger<DatabaseMySQLService> logger)
    {
        sqlconnection = connection;
        mysqllogger = logger;
    }

    public async Task VerifyConnectionSQL()
    {
        try
        {
            await sqlconnection.OpenAsync();
            mysqllogger.LogInformation("Conexão MySQL estabelecida");
        }
        catch (MySqlException ex)
        {
            mysqllogger.LogError(ex, "Falha na conexão MySQL: {ErrorCode} {ErorMessage}",ex.Number ,ex.Message);
            throw new DatabaseConnectionException("Erro Mysql", ex);
        }
    }

}