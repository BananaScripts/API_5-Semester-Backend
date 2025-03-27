using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LLMChatbotApi.Config;
using LLMChatbotApi.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LLMChatbotApi.Services;

public class TokenService
{
    private readonly Settings _settings;
    private readonly DatabaseRedisService _redisService;
    private readonly ILogger<TokenService> _logger;

    public TokenService(IOptions<Settings> settings, DatabaseRedisService redisService, ILogger<TokenService> logger)
    {
        _settings = settings.Value;
        _redisService = redisService;
        _logger = logger;
    }

    public async Task<string> GenerateToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_settings.TokenPrivateKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.user_id.ToString()),
                new Claim(ClaimTypes.Name, user.user_name),
                new Claim(ClaimTypes.Email, user.user_email),
                new Claim(ClaimTypes.Role, user.user_role.ToString())
            }),
            Expires = DateTime.UtcNow.AddHours(_settings.TokenExpirationHours),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        await StoreTokenInRedis(user, tokenString);

        return tokenString;
    }

    private async Task StoreTokenInRedis(User user, string token)
    {
        try
        {
            var db = _redisService.GetDatabase();
            var key = $"session:{token}";
            var userJson = System.Text.Json.JsonSerializer.Serialize(user);

            _logger.LogInformation("Armazenando no Redis: {Key} → {Value}", key, userJson);

            await db.StringSetAsync(
                key,
                userJson,
                TimeSpan.FromHours(_settings.TokenExpirationHours)
            );

            _logger.LogInformation("Token armazenado com sucesso no Redis");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao armazenar token no Redis");
            throw;
        }
    }

    public async Task RevokeToken(string token)
    {
        var db = _redisService.GetDatabase();
        await db.KeyDeleteAsync($"session:{token}");
    }
}