using LLMChatbotApi.Enums;
using LLMChatbotApi.Interfaces;
using LLMChatbotApi.Models;
using LLMChatbotApi.Services;
using MySqlConnector;

namespace LLMChatbotApi.Repositories;

public class AgentRepository : IAgentRepository
{
    private readonly DatabaseMySQLService _mysqlService;
    private readonly ILogger<AgentRepository> _logger;

    public AgentRepository(DatabaseMySQLService mysqlService, ILogger<AgentRepository> logger)
    {
        _mysqlService = mysqlService;
        _logger = logger;
    }

    public async Task<Agent> Create(Agent agent)
    {
        using var connection = _mysqlService.GetConnection();
        try
        {
            await connection.OpenAsync();

            using var cmd = new MySqlCommand(
                "INSERT INTO agent (agent_name, agent_description, agent_config, agent_status, created_by_user) " +
                "VALUES (@name, @description, @config, @status, @createdBy); " +
                "SELECT LAST_INSERT_ID();", connection);

            cmd.Parameters.AddWithValue("@name", agent.agent_name);
            cmd.Parameters.AddWithValue("@description", agent.agent_description);
            cmd.Parameters.AddWithValue("@config", agent.agent_config);
            cmd.Parameters.AddWithValue("@status", (int)agent.agent_status);
            cmd.Parameters.AddWithValue("@createdBy", agent.created_by_user);

            agent.agent_id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return agent;
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Erro ao criar agente");
            throw;
        }
    }

    public async Task<Agent> Update(Agent agent)
    {
        using(var connection = _mysqlService.GetConnection())
        {
            try
            {
                await connection.OpenAsync();
                using var cmd = new MySqlCommand(
                    "UPDATE agent SET " +
                    "agent_name = @name, " +
                    "agent_description = @description, " +
                    "agent_config = @config, " +
                    "agent_status = @status " +
                    "WHERE agent_id = @id", connection);

                cmd.Parameters.AddWithValue("@name", agent.agent_name);
                cmd.Parameters.AddWithValue("@description", agent.agent_description);
                cmd.Parameters.AddWithValue("@config", agent.agent_config);
                cmd.Parameters.AddWithValue("@status", (int)agent.agent_status);
                cmd.Parameters.AddWithValue("@id", agent.agent_id);

                await cmd.ExecuteNonQueryAsync();
                return agent;
            }
            catch (MySqlException ex)
            {
                _logger.LogError(ex, "Erro ao atualizar agente ID: {AgentId}", agent.agent_id);
                throw;
            }
        }
    }

    public async Task Delete(int agentId)
    {
        using var connection = _mysqlService.GetConnection();
        try
        {
            await connection.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM agent WHERE agent_id = @id", connection);
            cmd.Parameters.AddWithValue("@id", agentId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar agente");
            throw;
        }
    }

    public async Task<Agent?> GetById(int agentId)
    {
        using var connection = _mysqlService.GetConnection();
        try
        {
            await connection.OpenAsync();
            using var cmd = new MySqlCommand("SELECT * FROM agent WHERE agent_id = @id", connection);
            cmd.Parameters.AddWithValue("@id", agentId);

            using var reader = await cmd.ExecuteReaderAsync();
            return await ReadAgent(reader);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar agente");
            throw;
        }
    }

    public async Task<List<Agent>> GetByCreator(int userId)
    {
        using var connection = _mysqlService.GetConnection();
        try
        {
            await connection.OpenAsync();
            using var cmd = new MySqlCommand("SELECT * FROM agent WHERE created_by_user = @userId", connection);
            cmd.Parameters.AddWithValue("@userId", userId);

            var agents = new List<Agent>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                agents.Add(MapAgent(reader));
            }
            return agents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar agentes do usuário {UserId}", userId);
            throw;
        }
    }

    public async Task<(List<Agent> Agents, int TotalCount)> GetAllPaginated(int page, int pageSize)
    {
        using var connection = _mysqlService.GetConnection();
        try
        {
            await connection.OpenAsync();

            var countCmd = new MySqlCommand("SELECT COUNT(*) FROM agent", connection);
            var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            var offset = (page - 1) * pageSize;
            var cmd = new MySqlCommand(
                "SELECT * FROM agent ORDER BY agent_id LIMIT @pageSize OFFSET @offset",
                connection);

            cmd.Parameters.AddWithValue("@pageSize", pageSize);
            cmd.Parameters.AddWithValue("@offset", offset);

            var agents = new List<Agent>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                agents.Add(MapAgent(reader));
            }

            return (agents, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar agentes");
            throw;
        }
    }

    public async Task<bool> UpdateStatus(int agentId, AgentStatus newStatus)
    {
        using var connection = _mysqlService.GetConnection();
        try
        {
            await connection.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE agent SET agent_status = @status WHERE agent_id = @id",
                connection);

            cmd.Parameters.AddWithValue("@status", (int)newStatus);
            cmd.Parameters.AddWithValue("@id", agentId);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar status do agente");
            throw;
        }
    }

    private async Task<Agent?> ReadAgent(MySqlDataReader reader)
    {
        if (await reader.ReadAsync())
        {
            return MapAgent(reader);
        }
        return null;
    }

    private Agent MapAgent(MySqlDataReader reader)
    {
        return new Agent
        {
            agent_id = reader.GetInt32("agent_id"),
            agent_name = reader.GetString("agent_name"),
            agent_description = reader.IsDBNull(reader.GetOrdinal("agent_description"))
                ? null
                : reader.GetString("agent_description"),
            agent_config = reader.GetString("agent_config"),
            agent_status = (AgentStatus)reader.GetInt32("agent_status"),
            created_by_user = reader.GetInt32("created_by_user"),
            agent_created_at = reader.GetDateTime("agent_created_at"),
            agent_updated_at = reader.GetDateTime("agent_updated_at")
        };
    }
}