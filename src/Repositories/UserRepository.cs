using System.ComponentModel.DataAnnotations;
using BCrypt.Net;
using LLMChatbotApi.Enums;
using LLMChatbotApi.Interfaces;
using LLMChatbotApi.Models;
using LLMChatbotApi.Services;
using MongoDB.Bson.Serialization;
using MySqlConnector;

namespace LLMChatbotApi.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DatabaseMySQLService _mysqlService;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(DatabaseMySQLService mysqlService, ILogger<UserRepository> logger)
    {
        _mysqlService = mysqlService;
        _logger = logger;
    }

    public async Task<User> Create(User user)
    {
        var connection = _mysqlService.GetConnection();

        try
        {
            await connection.OpenAsync();

            user.user_password = BCrypt.Net.BCrypt.HashPassword(user.user_password);

            using var cmd = new MySqlCommand(
                "INSERT INTO user (user_name, user_email, user_password, user_role) " +
                "VALUES (@name, @email, @password, @role); " +
                "SELECT LAST_INSERT_ID();", connection);

            cmd.Parameters.AddWithValue("@name", user.user_name);
            cmd.Parameters.AddWithValue("@email", user.user_email);
            cmd.Parameters.AddWithValue("@password", user.user_password);
            cmd.Parameters.AddWithValue("@role", (int)user.user_role);

            user.user_id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return user;
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            throw new ValidationException("Email já cadastrado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar o usuário");
            throw;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    public async Task<User> Update(User user)
    {
        var connection = _mysqlService.GetConnection();
        try
        {
            await connection.OpenAsync();

            using var cmd = new MySqlCommand(
                "UPDATE user SET " +
                "user_name = @name, " +
                "user_email = @email, " +
                "user_password = @password, " +
                "user_role = @role " +
                "WHERE user_id = @id", connection);

            cmd.Parameters.AddWithValue("@name", user.user_name);
            cmd.Parameters.AddWithValue("@email", user.user_email);
            cmd.Parameters.AddWithValue("@password", user.user_password);
            cmd.Parameters.AddWithValue("@role", (int)user.user_role);
            cmd.Parameters.AddWithValue("@id", user.user_id);

            await cmd.ExecuteNonQueryAsync();
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar usuário ID: {UserId}", user.user_id);
            throw;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    public async Task DeleteById(int userId)
    {
        var connection = _mysqlService.GetConnection();
        try
        {
            await connection.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM user WHERE user_id = @id", connection);
            cmd.Parameters.AddWithValue("@id", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar usuário");
            throw;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    public async Task DeleteByEmail(string userEmail)
    {
        var connection = _mysqlService.GetConnection();
        try
        {
            await connection.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM user WHERE user_email = @email", connection);
            cmd.Parameters.AddWithValue("@email", userEmail);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar usuário");
            throw;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    public async Task<User?> GetById(int userId)
    {
        var connection = _mysqlService.GetConnection();
        try
        {
            await connection.OpenAsync();
            using var cmd = new MySqlCommand("SELECT * FROM user WHERE user_id = @id", connection);
            cmd.Parameters.AddWithValue("@id", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            return await ReadUser(reader);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar usuário");
            throw;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    public async Task<User?> GetByEmail(string userEmail)
    {
        var connection = _mysqlService.GetConnection();
        try
        {
            await connection.OpenAsync();
            using var cmd = new MySqlCommand("SELECT * FROM user WHERE user_email = @email", connection);
            cmd.Parameters.AddWithValue("@email", userEmail);

            using var reader = await cmd.ExecuteReaderAsync();
            return await ReadUser(reader);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar usuário");
            throw;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    public async Task<(List<User> Users, int TotalCount)> GetAllPaginated(int page, int pageSize)
    {
        var connection = _mysqlService.GetConnection();
        try
        {
            await connection.OpenAsync();

            // Obter total de registros
            var countCmd = new MySqlCommand("SELECT COUNT(*) FROM user", connection);
            var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            // Obter dados paginados
            var offset = (page - 1) * pageSize;
            var cmd = new MySqlCommand(
                "SELECT * FROM user ORDER BY user_id LIMIT @pageSize OFFSET @offset",
                connection);

            cmd.Parameters.AddWithValue("@pageSize", pageSize);
            cmd.Parameters.AddWithValue("@offset", offset);

            var users = new List<User>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(MapUser(reader));
            }

            return (users, totalCount);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private async Task<User?> ReadUser(MySqlDataReader reader)
    {
        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }
        return null;
    }

    private User MapUser(MySqlDataReader reader)
    {
        return new User
        {
            user_id = reader.GetInt32("user_id"),
            user_name = reader.GetString("user_name"),
            user_email = reader.GetString("user_email"),
            user_password = reader.GetString("user_password"),
            user_role = (UserRole)reader.GetInt32("user_role"),
            user_created_at = reader.GetDateTime("user_created_at"),
            user_updated_at = reader.GetDateTime("user_updated_at")
        };
    }
}
