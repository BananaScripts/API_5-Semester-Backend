using System.Security.Cryptography;
using System.Text;
using Dapper;
using LLMChatbotApi.DTO;
using LLMChatbotApi.Models;
using LLMChatbotApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace LLMChatbotApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly TokenService _tokenService;
    private readonly DatabaseMySQLService _mysqlService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(TokenService tokenService, DatabaseMySQLService mysqlService, ILogger<AuthController> logger)
    {
        _tokenService = tokenService;
        _mysqlService = mysqlService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDTO request)
    {
        try
        {
            await _mysqlService.VerifyConnection();

            var user = await AuthenticateUser(request.Email, request.Password);

            if (user == null)
                return Unauthorized(new { Message = "Credenciais inválidas" });

            var token = _tokenService.GenerateToken(user);

            return Ok(new
            {
                Token = token,
                User = new
                {
                    user.user_id,
                    user.user_name,
                    user.user_email,
                    user.user_role
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no login");
            return StatusCode(500, new { Message = "Erro interno no servidor" });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var token = HttpContext.Request.Headers["Authorization"]
                .ToString()
                .Replace("Bearer ", "");

            await _tokenService.RevokeToken(token);
            return Ok(new { Message = "Logout realizado com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no logout");
            return StatusCode(500, new { Message = "Erro interno no servidor" });
        }
    }

    private async Task<User?> AuthenticateUser(string email, string password)
    {
        try
        {
            var connection = _mysqlService.GetConnection();
            var query = "SELECT * FROM user WHERE user_email = @Email LIMIT 1";

            var user = await connection.QueryFirstOrDefaultAsync<User>(query, new { Email = email });

            if (user == null) return null;

            bool isPasswordValid;

            if (IsValidBCryptHash(user.user_password))
            {
                isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.user_password);
            }
            else
            {
                var inputHash = GetSha256Hash(password);
                isPasswordValid = inputHash == user.user_password;
            }

            return isPasswordValid ? user : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na autenticação");
            return null;
        }
    }

    private bool IsValidBCryptHash(string hash)
    {
        return hash.StartsWith("$2a$")
            || hash.StartsWith("$2b$")
            || hash.StartsWith("$2y$");
    }

    private string GetSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }
}

