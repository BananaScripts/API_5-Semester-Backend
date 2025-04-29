using LLMChatbotApi.exceptions;
using MySqlConnector;

namespace LLMChatbotApi.Services;

public class DatabaseMySQLService : DatabaseServiceBase<DatabaseMySQLService>
{
    private readonly string _connectionString;

    public DatabaseMySQLService(IConfiguration configuration, ILogger<DatabaseMySQLService> logger)
        : base(logger)
    {
        _connectionString = configuration.GetConnectionString("MySQL") ?? throw new ArgumentNullException("Connection string 'DefaultConnection' not found.");
    }

    public MySqlConnection GetConnection()
    {
        return new MySqlConnection(_connectionString);
    }

    public override async Task VerifyConnection()
    {
        try
        {
            await using var connection = GetConnection();
            await connection.OpenAsync();
            Logger.LogInformation("Conexão MySQL estabelecida");
        }
        catch (MySqlException ex)
        {
            Logger.LogError(ex, "Falha na conexão MySQL: {ErrorCode} {ErrorMessage}", ex.Number, ex.Message);
            throw new DatabaseConnectionException("Erro Mysql", ex);
        }
    }
}
